using UnityEngine;
using VirtualMuseum.HandTracking;

namespace VirtualMuseum.Input
{
    public enum InteractionMode
    {
        FPSExploration,
        InfoPanel,
        ExcavationMouse,
        ExcavationHandTracking,
        HandTrackingExploration
    }

    /// <summary>
    /// 전역 입력 모드 관리자. F키(핸드트래킹 토글)를 어디서나(메인 탐험/발굴 미니게임 중) 감지하고,
    /// 현재 활성 IPointerInputSource를 제공한다.
    /// </summary>
    public class InputModeManager : MonoBehaviour
    {
        public static InputModeManager Instance { get; private set; }

        [SerializeField] private HandTrackingManager handTrackingManager;

        public InteractionMode CurrentMode { get; private set; } = InteractionMode.FPSExploration;
        public bool IsHandTrackingEnabled { get; private set; }

        private IPointerInputSource _mouseSource;
        private IPointerInputSource _handSource;

        public IPointerInputSource ActivePointerSource => IsHandTrackingEnabled ? _handSource : _mouseSource;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _mouseSource = new MouseInputSource();
            _handSource = new HandTrackingInputSource(handTrackingManager);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                ToggleHandTracking();
        }

        private void ToggleHandTracking()
        {
            IsHandTrackingEnabled = !IsHandTrackingEnabled;
            handTrackingManager?.SetActive(IsHandTrackingEnabled);

            // 발굴 미니게임 중이면 모드는 유지하고 입력 소스만 스왑
            if (CurrentMode == InteractionMode.ExcavationMouse || CurrentMode == InteractionMode.ExcavationHandTracking)
                CurrentMode = IsHandTrackingEnabled ? InteractionMode.ExcavationHandTracking : InteractionMode.ExcavationMouse;
            else if (CurrentMode == InteractionMode.FPSExploration || CurrentMode == InteractionMode.HandTrackingExploration)
                CurrentMode = IsHandTrackingEnabled ? InteractionMode.HandTrackingExploration : InteractionMode.FPSExploration;
        }

        public void EnterInfoPanel()
        {
            CurrentMode = InteractionMode.InfoPanel;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void EnterExcavation()
        {
            CurrentMode = IsHandTrackingEnabled ? InteractionMode.ExcavationHandTracking : InteractionMode.ExcavationMouse;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ReturnToExploration()
        {
            CurrentMode = IsHandTrackingEnabled ? InteractionMode.HandTrackingExploration : InteractionMode.FPSExploration;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
