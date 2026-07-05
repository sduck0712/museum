using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Puzzle;
using VirtualMuseum.Student;
using VirtualMuseum.Vault;

namespace VirtualMuseum.Excavation
{
    /// <summary>
    /// 발굴 미니게임 오버레이의 최상위 컨트롤러.
    /// 카메라 줌(휠), 도구 선택(1~4키), 상태 전이(파손→퍼즐→복원→공개), 종료(ESC)를 총괄한다.
    /// UI 배선 부담을 없애기 위해 HUD는 IMGUI(OnGUI)로 그린다.
    /// </summary>
    public class ExcavationSessionController : MonoBehaviour
    {
        [Header("Camera Zoom")]
        [SerializeField] private float minFov = 25f;
        [SerializeField] private float maxFov = 70f;
        [SerializeField] private float zoomSpeed = 25f;

        [Header("Gameplay")]
        [SerializeField] private ExcavationBrush brush;
        [SerializeField] private ReassemblyPuzzleManager puzzleManager;
        [SerializeField] private ParticleSystem successParticles;
        [SerializeField] private ParticleSystem breakParticles; // 파손 시 파편 연출 (스펙 6절)

        private Camera _camera;
        private float _defaultFov;
        private ArtifactDurabilityController _target;
        private ArtifactData _artifact;
        private Renderer[] _targetRenderers;
        private Collider[] _targetColliders;
        private string _statusMessage = "";

        public bool HasActiveSession => _target != null;

        public void BeginSession(ArtifactData artifact, ArtifactDurabilityController target)
        {
            if (target == null)
            {
                Debug.LogWarning("[ExcavationSessionController] 발굴 대상 ArtifactDurabilityController가 없습니다.");
                StudentInteractionManager.Instance?.CloseExcavationOverlay();
                return;
            }

            EndSessionInternal(); // 이전 세션 구독/상태 정리 (중복 구독 방지)

            _target = target;
            _artifact = artifact;
            _targetRenderers = target.GetComponentsInChildren<Renderer>();
            _targetColliders = target.GetComponentsInChildren<Collider>();

            _camera = Camera.main;
            if (_camera != null) _defaultFov = _camera.fieldOfView;

            _target.OnBroken += HandleBroken;
            _target.OnFullyCleared += HandleFullyCleared;

            brush.SetTarget(_target, _camera);
            brush.SetTool(artifact.recommendedToolType);

            var state = _target.RuntimeState.state;
            if (state == ExcavationState.Broken || state == ExcavationState.ReassemblyPuzzle)
            {
                // 이전에 파손된 채 나갔던 유물 → 퍼즐부터 이어서 진행
                _statusMessage = "유물이 파손된 상태입니다. 조각을 맞춰 복원하세요!";
                StartPuzzle();
            }
            else if (state == ExcavationState.Revealed)
            {
                _statusMessage = "이미 발굴이 완료된 유물입니다. (ESC: 나가기)";
                brush.enabled = false;
            }
            else
            {
                _target.BeginInteracting();
                brush.enabled = true;
                _statusMessage = "드래그로 흙을 조심스럽게 제거하세요. 너무 빠르면 유물이 파손됩니다!";
            }
        }

        private void Update()
        {
            if (_target == null) return;

            // 줌: 마우스 휠 (핀치 인/아웃은 MediaPipe 실연동 시 두 손 거리로 확장 가능)
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f && _camera != null)
            {
                _camera.fieldOfView = Mathf.Clamp(
                    _camera.fieldOfView - scroll * zoomSpeed, minFov, maxFov);
            }

            // 도구 선택 단축키 1~4
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(ExcavationToolType.AirBlower);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(ExcavationToolType.SoftBrush);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(ExcavationToolType.ChiselTool);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(ExcavationToolType.PickTool);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                StudentInteractionManager.Instance?.CloseExcavationOverlay();
        }

        public void SelectTool(ExcavationToolType toolType) => brush.SetTool(toolType);

        private void HandleBroken()
        {
            _statusMessage = "유물이 파손되었습니다! 흩어진 조각을 드래그해 실루엣에 맞추세요.";

            if (breakParticles != null)
            {
                breakParticles.transform.position = _target.transform.position;
                breakParticles.Play();
            }

            StartPuzzle();
        }

        private void StartPuzzle()
        {
            brush.enabled = false;
            SetModelVisible(false);
            _target.NotifyPuzzleStarted();
            puzzleManager.BeginPuzzle(_artifact, _target.transform, OnPuzzleCompleted);
        }

        private void OnPuzzleCompleted()
        {
            SetModelVisible(true);
            ApplyRestoredCrackTint();

            _target.NotifyRepaired();
            _statusMessage = "복원 완료! 남은 흙을 마저 제거하세요. (복원 흔적이 남습니다)";

            brush.enabled = true;
            brush.ForceRecheckClearedRatio(); // 파손 전 이미 거의 다 판 상태였다면 즉시 공개 판정
        }

        private void HandleFullyCleared()
        {
            brush.enabled = false;
            bool restored = _target.RuntimeState.isRestored;
            _statusMessage = restored
                ? "발굴 완료! [복원됨] 배지와 함께 내 유물함에 등록되었습니다. (ESC: 나가기)"
                : "발굴 완료! 내 유물함에 등록되었습니다. (ESC: 나가기)";

            if (successParticles != null)
            {
                successParticles.transform.position = _target.transform.position;
                successParticles.Play();
            }

            VaultManager.Instance.AddArtifact(_artifact, restored);
        }

        private void SetModelVisible(bool visible)
        {
            if (_targetRenderers != null)
                foreach (var r in _targetRenderers) if (r != null) r.enabled = visible;
            if (_targetColliders != null)
                foreach (var c in _targetColliders) if (c != null) c.enabled = visible;
        }

        private void ApplyRestoredCrackTint()
        {
            // 복원 흔적: 옅은 균열 느낌의 어두운 틴트를 영구 적용 (스펙 5.5/6절)
            if (_targetRenderers == null) return;
            foreach (var r in _targetRenderers)
            {
                if (r == null) continue;
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_Tint"))
                        mat.SetColor("_Tint", new Color(0.78f, 0.73f, 0.70f));
                }
            }
        }

        /// <summary>오버레이가 닫힐 때(StudentInteractionManager) 정리.</summary>
        public void EndSession()
        {
            // 퍼즐 도중 나가면 조각은 정리하되 Broken 상태는 유지 → 재진입 시 퍼즐부터 재개
            puzzleManager?.AbortPuzzle();
            if (_camera != null) _camera.fieldOfView = _defaultFov;
            EndSessionInternal();
        }

        private void EndSessionInternal()
        {
            if (_target != null)
            {
                _target.OnBroken -= HandleBroken;
                _target.OnFullyCleared -= HandleFullyCleared;
            }
            if (brush != null)
            {
                brush.enabled = false;
                brush.ClearTarget();
            }
            _target = null;
            _artifact = null;
            _targetRenderers = null;
            _targetColliders = null;
        }

        private void OnDisable()
        {
            EndSessionInternal();
        }

        private void OnGUI()
        {
            if (_target == null || _artifact == null) return;

            const float w = 340f;
            GUILayout.BeginArea(new Rect(12, 12, w, 240), GUI.skin.box);

            GUILayout.Label($"<b>{_artifact.artifactName}</b>  [{_artifact.rarityTier}]",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 });

            var tool = brush.CurrentTool;
            GUILayout.Label($"도구: {tool.displayName}   (1 에어블로워 / 2 브러시 / 3 조각칼 / 4 피크)");

            // 내구도 바
            float durability01 = _artifact.maxDurability > 0
                ? (float)_target.RuntimeState.currentDurability / _artifact.maxDurability : 0f;
            GUILayout.Label($"내구도: {_target.RuntimeState.currentDurability}/{_artifact.maxDurability}");
            DrawBar(durability01, durability01 > 0.35f ? new Color(0.3f, 0.75f, 0.35f) : new Color(0.85f, 0.3f, 0.25f));

            GUILayout.Label($"발굴 진행률: {(brush.ClearedRatio * 100f):0}%");
            DrawBar(brush.ClearedRatio, new Color(0.85f, 0.7f, 0.3f));

            GUILayout.Space(4);
            GUILayout.Label(_statusMessage, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("휠: 줌 / ESC: 나가기 / F: 핸드트래킹 전환",
                new GUIStyle(GUI.skin.label) { fontSize = 11 });

            GUILayout.EndArea();
        }

        private static void DrawBar(float value01, Color color)
        {
            Rect r = GUILayoutUtility.GetRect(300, 10);
            GUI.Box(r, GUIContent.none);
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(value01), r.height - 2),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
