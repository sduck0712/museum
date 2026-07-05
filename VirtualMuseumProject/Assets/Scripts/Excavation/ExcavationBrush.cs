using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Input;

namespace VirtualMuseum.Excavation
{
    /// <summary>
    /// 마우스 드래그 또는 핸드트래킹 핀치-드래그로 RenderTexture 마스크를 칠해 흙을 제거한다.
    /// ExcavationSurface 셰이더가 이 RenderTexture를 알파 마스크로 샘플링한다.
    /// </summary>
    public class ExcavationBrush : MonoBehaviour
    {
        [SerializeField] private Camera excavationCamera;
        [SerializeField] private Renderer targetArtifactRenderer;
        [SerializeField] private int maskResolution = 512;
        [SerializeField] private Texture2D brushShapeTexture; // 부드러운 원형 그라디언트 텍스처
        [SerializeField] private int clearedRatioCheckIntervalFrames = 5; // 매 프레임 풀 리드백은 비용이 크므로 주기 조절

        private RenderTexture _dirtMaskRT; // 흰색=흙 있음, 검정=제거됨
        private Material _brushBlitMaterial;
        private ArtifactDurabilityController _durabilityController;
        private ExcavationToolStats _currentTool;

        private Vector2 _lastUv;
        private bool _hasLastUv;
        private int _frameCounter;
        private float _clearedRatioCache;

        public float ClearedRatio => _clearedRatioCache;

        private void Awake()
        {
            _dirtMaskRT = new RenderTexture(maskResolution, maskResolution, 0, RenderTextureFormat.R8)
            {
                name = "ExcavationDirtMask"
            };

            var initTexture = new Texture2D(1, 1);
            initTexture.SetPixel(0, 0, Color.white); // 초기값: 흙 100%
            initTexture.Apply();
            Graphics.Blit(initTexture, _dirtMaskRT);
            Destroy(initTexture);

            _brushBlitMaterial = new Material(Shader.Find("Hidden/VirtualMuseum/BrushBlit"));

            _durabilityController = targetArtifactRenderer.GetComponent<ArtifactDurabilityController>();
            SetTool(ExcavationToolType.SoftBrush);

            if (targetArtifactRenderer != null)
                targetArtifactRenderer.material.SetTexture("_DirtMaskTex", _dirtMaskRT);
        }

        public void SetTool(ExcavationToolType toolType)
        {
            _currentTool = ExcavationToolLibrary.Tools[toolType];
        }

        private void Update()
        {
            var pointerSource = InputModeManager.Instance.ActivePointerSource;
            if (pointerSource == null || !pointerSource.IsPrimaryActionHeld())
            {
                _hasLastUv = false;
                return;
            }

            Vector2 viewportPos = pointerSource.GetPointerViewportPosition();
            Ray ray = excavationCamera.ViewportPointToRay(viewportPos);

            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == targetArtifactRenderer.gameObject)
            {
                Vector2 uv = hit.textureCoord;
                PaintAt(uv);

                if (_hasLastUv)
                {
                    float dragDistance = Vector2.Distance(_lastUv, uv);
                    float pressureEstimate = Mathf.Clamp01(dragDistance * 10f); // 드래그 이동량 기반 근사치
                    _durabilityController?.ApplyPressure(pressureEstimate, _currentTool.breakRisk);
                }

                _lastUv = uv;
                _hasLastUv = true;
            }
            else
            {
                _hasLastUv = false;
            }
        }

        private void PaintAt(Vector2 uv)
        {
            _brushBlitMaterial.SetVector("_BrushUV", uv);
            _brushBlitMaterial.SetFloat("_BrushRadius", _currentTool.brushRadius);
            _brushBlitMaterial.SetFloat("_Strength", _currentTool.strength);
            _brushBlitMaterial.SetTexture("_BrushShape", brushShapeTexture);

            var temp = RenderTexture.GetTemporary(_dirtMaskRT.width, _dirtMaskRT.height, 0, _dirtMaskRT.format);
            Graphics.Blit(_dirtMaskRT, temp, _brushBlitMaterial);
            Graphics.Blit(temp, _dirtMaskRT);
            RenderTexture.ReleaseTemporary(temp);

            _frameCounter++;
            if (_frameCounter >= clearedRatioCheckIntervalFrames)
            {
                _frameCounter = 0;
                UpdateClearedRatio();
            }
        }

        private void UpdateClearedRatio()
        {
            // 32x32로 다운샘플링한 평균 밝기로 근사치 산출 (풀 해상도 리드백은 비용이 크므로 지양)
            RenderTexture.active = _dirtMaskRT;
            var readTex = new Texture2D(32, 32, TextureFormat.R8, false);
            readTex.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            readTex.Apply();
            RenderTexture.active = null;

            var pixels = readTex.GetPixels32();
            float sum = 0f;
            foreach (var p in pixels) sum += p.r / 255f;
            float remainingDirt = sum / pixels.Length;
            _clearedRatioCache = 1f - remainingDirt;
            Destroy(readTex);

            if (_clearedRatioCache >= 0.98f)
                _durabilityController?.NotifyMaskFullyCleared();
        }

        private void OnDestroy()
        {
            if (_dirtMaskRT != null)
            {
                _dirtMaskRT.Release();
                Destroy(_dirtMaskRT);
            }

            if (_brushBlitMaterial != null)
                Destroy(_brushBlitMaterial);
        }
    }
}
