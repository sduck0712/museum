using System.Collections.Generic;
using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Input;

namespace VirtualMuseum.Excavation
{
    /// <summary>
    /// 마우스 드래그 또는 핸드트래킹 핀치-드래그로 RenderTexture 마스크를 칠해 흙을 제거한다.
    /// ExcavationSurface 셰이더가 이 RenderTexture를 알파 마스크로 샘플링한다.
    ///
    /// - 대상 유물은 씬에 고정되어 있지 않고 런타임에 스폰되므로 SetTarget()으로 주입받는다.
    /// - 마스크 RT는 artifactID별로 캐싱되어 세션을 나갔다 들어와도 진행 상태가 유지된다.
    /// - 줌(FOV)에 따라 브러시 판정 반경을 자동 보정한다 (스펙 5.1).
    /// - 안전 임계 속도를 초과한 드래그만 내구도를 깎는다 (스펙 5.2/5.4).
    /// </summary>
    public class ExcavationBrush : MonoBehaviour
    {
        [Header("Mask")]
        [SerializeField] private int maskResolution = 512;
        [SerializeField] private Texture2D brushShapeTexture; // 비워두면 소프트 원형 텍스처를 자동 생성
        [SerializeField, Range(0.5f, 1f)] private float revealThreshold = 0.93f;
        [SerializeField] private int clearedRatioCheckIntervalFrames = 10;

        [Header("Damage Tuning")]
        [SerializeField] private float safeDragSpeed = 0.6f;   // UV/sec - 이 속도 이하 드래그는 파손 없음
        [SerializeField] private float damageScale = 25f;      // 초과 속도 → 내구도 데미지 변환 계수
        [SerializeField] private float strengthTimeScale = 6f; // 프레임레이트 독립 제거 속도 계수
        [SerializeField] private float referenceFov = 60f;     // 줌 보정 기준 FOV

        private readonly Dictionary<string, RenderTexture> _maskCache = new();

        private Camera _camera;
        private ArtifactDurabilityController _target;
        private Transform _targetRoot;
        private RenderTexture _activeMask;
        private Material _brushBlitMaterial;
        private Texture2D _generatedBrushTexture;
        private ExcavationToolStats _currentTool;

        private Vector2 _lastUv;
        private float _lastPaintTime;
        private bool _hasLastUv;
        private int _frameCounter;
        private float _clearedRatioCache;

        public float ClearedRatio => _clearedRatioCache;
        public ExcavationToolStats CurrentTool => _currentTool;

        private void Awake()
        {
            var blitShader = Shader.Find("Hidden/VirtualMuseum/BrushBlit");
            if (blitShader == null)
            {
                Debug.LogError("[ExcavationBrush] BrushBlit 셰이더를 찾을 수 없습니다. Assets/Shaders/BrushBlit.shader 존재 여부를 확인하세요.");
                enabled = false;
                return;
            }
            _brushBlitMaterial = new Material(blitShader);

            if (brushShapeTexture == null)
            {
                _generatedBrushTexture = CreateSoftCircleTexture(64);
                brushShapeTexture = _generatedBrushTexture;
            }

            SetTool(ExcavationToolType.SoftBrush);
        }

        /// <summary>발굴 세션 시작 시 대상 유물을 주입한다.</summary>
        public void SetTarget(ArtifactDurabilityController target, Camera excavationCamera)
        {
            _target = target;
            _targetRoot = target != null ? target.transform : null;
            _camera = excavationCamera != null ? excavationCamera : Camera.main;
            _hasLastUv = false;
            _clearedRatioCache = 0f;

            if (_target == null) return;

            _activeMask = GetOrCreateMask(_target.ArtifactInfo.artifactID);

            // 유물의 모든 렌더러에 마스크 주입 (머티리얼 인스턴스는 스포너가 이미 분리해 둠)
            foreach (var renderer in _targetRoot.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    if (mat.HasProperty("_DirtMaskTex"))
                        mat.SetTexture("_DirtMaskTex", _activeMask);
                }
            }

            UpdateClearedRatio(); // 이어하기 시 진행률 즉시 반영
        }

        public void ClearTarget()
        {
            _target = null;
            _targetRoot = null;
            _activeMask = null;
            _hasLastUv = false;
        }

        public void SetTool(ExcavationToolType toolType)
        {
            _currentTool = ExcavationToolLibrary.Tools[toolType];
        }

        private RenderTexture GetOrCreateMask(string artifactID)
        {
            if (_maskCache.TryGetValue(artifactID, out var cached) && cached != null)
                return cached;

            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;

            var rt = new RenderTexture(maskResolution, maskResolution, 0, format)
            {
                name = $"ExcavationDirtMask_{artifactID}"
            };
            rt.Create();

            Graphics.Blit(Texture2D.whiteTexture, rt); // 초기값: 흙 100%
            _maskCache[artifactID] = rt;
            return rt;
        }

        private void Update()
        {
            if (_target == null || _activeMask == null || _camera == null) return;

            var modeManager = InputModeManager.Instance;
            if (modeManager == null) return;
            if (modeManager.CurrentMode != InteractionMode.ExcavationMouse &&
                modeManager.CurrentMode != InteractionMode.ExcavationHandTracking)
                return;

            // 파손/퍼즐 상태에서는 페인팅 중단
            if (_target.RuntimeState.state != ExcavationState.Interacting)
            {
                _hasLastUv = false;
                return;
            }

            // IMGUI(도구 HUD) 조작 중에는 페인팅하지 않음
            if (GUIUtility.hotControl != 0) return;

            var pointerSource = modeManager.ActivePointerSource;
            if (pointerSource == null || !pointerSource.IsPrimaryActionHeld())
            {
                _hasLastUv = false;
                return;
            }

            Vector2 viewportPos = pointerSource.GetPointerViewportPosition();
            Ray ray = _camera.ViewportPointToRay(viewportPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f) && hit.transform.IsChildOf(_targetRoot))
            {
                Vector2 uv = hit.textureCoord;
                PaintAt(uv);

                if (_hasLastUv)
                {
                    float dt = Mathf.Max(Time.time - _lastPaintTime, 0.0001f);
                    float uvSpeed = Vector2.Distance(_lastUv, uv) / dt;
                    float safeSpeed = safeDragSpeed * DifficultySpeedFactor(_target.ArtifactInfo.excavationDifficulty);
                    float excess = Mathf.Max(0f, uvSpeed - safeSpeed);

                    if (excess > 0f && _currentTool.breakRisk > 0f)
                        _target.ApplyDamage(excess * _currentTool.breakRisk * damageScale * dt);
                }

                _lastUv = uv;
                _lastPaintTime = Time.time;
                _hasLastUv = true;
            }
            else
            {
                _hasLastUv = false;
            }
        }

        private static float DifficultySpeedFactor(ExcavationDifficulty difficulty) => difficulty switch
        {
            ExcavationDifficulty.Easy => 1.5f,
            ExcavationDifficulty.Hard => 0.7f,
            _ => 1f
        };

        private void PaintAt(Vector2 uv)
        {
            // 줌인(FOV 작음)할수록 화면상 동일 픽셀이 좁은 UV를 가리키므로 반경을 축소 보정
            float zoomScale = _camera.fieldOfView / referenceFov;
            float radius = _currentTool.brushRadius * Mathf.Clamp(zoomScale, 0.3f, 2f);

            _brushBlitMaterial.SetVector("_BrushUV", uv);
            _brushBlitMaterial.SetFloat("_BrushRadius", radius);
            _brushBlitMaterial.SetFloat("_Strength", _currentTool.strength * strengthTimeScale * Time.deltaTime);
            _brushBlitMaterial.SetTexture("_BrushShape", brushShapeTexture);

            var temp = RenderTexture.GetTemporary(_activeMask.width, _activeMask.height, 0, _activeMask.format);
            Graphics.Blit(_activeMask, temp, _brushBlitMaterial);
            Graphics.Blit(temp, _activeMask);
            RenderTexture.ReleaseTemporary(temp);

            _frameCounter++;
            if (_frameCounter >= clearedRatioCheckIntervalFrames)
            {
                _frameCounter = 0;
                UpdateClearedRatio();
            }
        }

        /// <summary>퍼즐 복원 직후 등, 즉시 진행률을 재판정하고 싶을 때 호출.</summary>
        public void ForceRecheckClearedRatio() => UpdateClearedRatio();

        private void UpdateClearedRatio()
        {
            if (_activeMask == null) return;

            // 마스크 전체를 32x32로 다운샘플 Blit한 뒤 리드백 → 전체 평균을 저비용으로 근사.
            // (원본 RT의 좌하단 32x32 픽셀만 읽으면 "구석 진행률"이 되므로 반드시 Blit으로 축소해야 함)
            var small = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(_activeMask, small);

            var prevActive = RenderTexture.active;
            RenderTexture.active = small;
            var readTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            readTex.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            readTex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(small);

            var pixels = readTex.GetPixels32();
            float sum = 0f;
            foreach (var p in pixels) sum += p.r / 255f;
            float remainingDirt = sum / pixels.Length;
            _clearedRatioCache = 1f - remainingDirt;
            Destroy(readTex);

            if (_clearedRatioCache >= revealThreshold)
                _target?.NotifyMaskFullyCleared();
        }

        private static Texture2D CreateSoftCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "GeneratedSoftBrush",
                wrapMode = TextureWrapMode.Clamp
            };

            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            foreach (var rt in _maskCache.Values)
            {
                if (rt != null)
                {
                    rt.Release();
                    Destroy(rt);
                }
            }
            _maskCache.Clear();

            if (_brushBlitMaterial != null) Destroy(_brushBlitMaterial);
            if (_generatedBrushTexture != null) Destroy(_generatedBrushTexture);
        }
    }
}
