using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Excavation;
using VirtualMuseum.Input;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 1인칭 학생 플레이어의 Q(정보)/E(발굴) 상호작용을 총괄한다.
    /// 패널 열기뿐 아니라 닫기 경로(Q/ESC)도 여기서 처리한다.
    /// </summary>
    public class StudentInteractionManager : MonoBehaviour
    {
        public static StudentInteractionManager Instance { get; private set; }

        [SerializeField] private GameObject infoPanelUI;
        [SerializeField] private GameObject excavationOverlayUI;
        [SerializeField] private CanvasGroup backgroundDimOverlay;

        private ArtifactData _nearbyArtifact;
        private PedestalProximityTrigger _nearbyTrigger;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetNearbyArtifact(ArtifactData data, PedestalProximityTrigger trigger)
        {
            _nearbyArtifact = data;
            _nearbyTrigger = trigger;
        }

        public void ClearNearbyArtifact(PedestalProximityTrigger trigger)
        {
            if (_nearbyTrigger != trigger) return;
            _nearbyArtifact = null;
            _nearbyTrigger = null;
        }

        private void Update()
        {
            if (InputModeManager.Instance == null) return;
            var mode = InputModeManager.Instance.CurrentMode;

            if (mode == InteractionMode.FPSExploration || mode == InteractionMode.HandTrackingExploration)
            {
                if (_nearbyArtifact == null) return;

                if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
                    OpenInfoPanel();
                else if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                    OpenExcavationOverlay();
            }
            else if (mode == InteractionMode.InfoPanel)
            {
                // 닫기 경로: Q 재입력 또는 ESC
                if (UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                    CloseInfoPanel();
            }
            // 발굴 모드의 ESC 종료는 ExcavationSessionController가 담당
        }

        private void OpenInfoPanel()
        {
            InputModeManager.Instance.EnterInfoPanel();
            if (backgroundDimOverlay != null) backgroundDimOverlay.alpha = 0.6f;
            infoPanelUI.SetActive(true);
            infoPanelUI.GetComponent<InfoPanelView>()?.Bind(_nearbyArtifact);
        }

        public void CloseInfoPanel()
        {
            infoPanelUI.SetActive(false);
            if (backgroundDimOverlay != null) backgroundDimOverlay.alpha = 0f;
            InputModeManager.Instance.ReturnToExploration();
        }

        private void OpenExcavationOverlay()
        {
            if (_nearbyTrigger == null) return;

            var durability = _nearbyTrigger.DurabilityController;
            if (durability == null)
            {
                Debug.LogWarning("[StudentInteractionManager] 유물 모델이 아직 로드되지 않았습니다.");
                return;
            }

            // 이미 발굴 완료된 유물은 발굴 대신 정보 패널을 띄운다
            if (durability.RuntimeState.state == ExcavationState.Revealed)
            {
                OpenInfoPanel();
                return;
            }

            InputModeManager.Instance.EnterExcavation();
            excavationOverlayUI.SetActive(true);
            excavationOverlayUI.GetComponent<ExcavationSessionController>()?.BeginSession(_nearbyArtifact, durability);
        }

        public void CloseExcavationOverlay()
        {
            excavationOverlayUI.GetComponent<ExcavationSessionController>()?.EndSession();
            excavationOverlayUI.SetActive(false);
            InputModeManager.Instance.ReturnToExploration();
        }

        private void OnGUI()
        {
            if (InputModeManager.Instance == null) return;
            var mode = InputModeManager.Instance.CurrentMode;
            if (mode != InteractionMode.FPSExploration && mode != InteractionMode.HandTrackingExploration) return;

            // 십자선
            GUI.Label(new Rect(Screen.width / 2f - 4, Screen.height / 2f - 10, 8, 20), "·",
                new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter });

            // 조작 안내 (좌측 하단)
            GUI.Label(new Rect(12, Screen.height - 30, 600, 24),
                "WASD 이동 / Shift 달리기 / 마우스 시야 / F 핸드트래킹 토글" +
                (_nearbyArtifact != null ? $"   |   [{_nearbyArtifact.artifactName}]  Q: 정보  E: 발굴" : ""));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
