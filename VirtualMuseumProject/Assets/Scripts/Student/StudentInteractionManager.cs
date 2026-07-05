using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Excavation;
using VirtualMuseum.Input;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 1인칭 학생 플레이어의 Q(정보)/E(발굴) 상호작용을 총괄한다.
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
            if (_nearbyArtifact == null) return;
            if (InputModeManager.Instance.CurrentMode != InteractionMode.FPSExploration &&
                InputModeManager.Instance.CurrentMode != InteractionMode.HandTrackingExploration)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Q))
                OpenInfoPanel();

            if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                OpenExcavationOverlay();
        }

        private void OpenInfoPanel()
        {
            InputModeManager.Instance.EnterInfoPanel();
            if (backgroundDimOverlay != null) backgroundDimOverlay.alpha = 0.6f;
            infoPanelUI.SetActive(true);
            // infoPanelUI 내부 바인딩 스크립트에서 _nearbyArtifact.videoURL / historicalDescription 표시
        }

        public void CloseInfoPanel()
        {
            infoPanelUI.SetActive(false);
            if (backgroundDimOverlay != null) backgroundDimOverlay.alpha = 0f;
            InputModeManager.Instance.ReturnToExploration();
        }

        private void OpenExcavationOverlay()
        {
            InputModeManager.Instance.EnterExcavation();
            excavationOverlayUI.SetActive(true);
            excavationOverlayUI.GetComponent<ExcavationSessionController>()?.BeginSession(_nearbyArtifact, _nearbyTrigger.transform);
        }

        public void CloseExcavationOverlay()
        {
            excavationOverlayUI.SetActive(false);
            InputModeManager.Instance.ReturnToExploration();
        }
    }
}
