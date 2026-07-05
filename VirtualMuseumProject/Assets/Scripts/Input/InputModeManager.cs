using UnityEngine;
using VirtualMuseum.Core;
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
    /// 웹캠/제공자 활성화에 실패하면 스펙 7.2에 따라 마우스 모드를 강제 유지한다.
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

            if (handTrackingManager == null)
                handTrackingManager = SceneQuery.Find<HandTrackingManager>();

            _mouseSource = new MouseInputSource();
            _handSource = new HandTrackingInputSource(handTrackingManager);
        }

        private void Start()
        {
            ApplyCursorState();
        }

        private void Update()
        {
            // 정보 패널이 떠 있는 동안에는 입력 소스 전환을 막는다 (UI 조작과 충돌 방지)
            if (UnityEngine.Input.GetKeyDown(KeyCode.F) && CurrentMode != InteractionMode.InfoPanel)
                ToggleHandTracking();
        }

        private void ToggleHandTracking()
        {
            if (!IsHandTrackingEnabled)
            {
                // 활성화 실패(웹캠 미검출 등) 시 마우스 모드 강제 유지
                if (handTrackingManager == null || !handTrackingManager.TryActivate())
                {
                    Debug.LogWarning("[InputModeManager] 핸드트래킹 활성화 실패 - 마우스 모드를 유지합니다.");
                    return;
                }
                IsHandTrackingEnabled = true;
            }
            else
            {
                handTrackingManager?.Deactivate();
                IsHandTrackingEnabled = false;
            }

            // 발굴 미니게임 중이면 모드는 유지하고 입력 소스만 스왑
            if (CurrentMode == InteractionMode.ExcavationMouse || CurrentMode == InteractionMode.ExcavationHandTracking)
                CurrentMode = IsHandTrackingEnabled ? InteractionMode.ExcavationHandTracking : InteractionMode.ExcavationMouse;
            else if (CurrentMode == InteractionMode.FPSExploration || CurrentMode == InteractionMode.HandTrackingExploration)
                CurrentMode = IsHandTrackingEnabled ? InteractionMode.HandTrackingExploration : InteractionMode.FPSExploration;

            ApplyCursorState();
        }

        public void EnterInfoPanel()
        {
            CurrentMode = InteractionMode.InfoPanel;
            ApplyCursorState();
        }

        public void EnterExcavation()
        {
            CurrentMode = IsHandTrackingEnabled ? InteractionMode.ExcavationHandTracking : InteractionMode.ExcavationMouse;
            ApplyCursorState();
        }

        public void ReturnToExploration()
        {
            CurrentMode = IsHandTrackingEnabled ? InteractionMode.HandTrackingExploration : InteractionMode.FPSExploration;
            ApplyCursorState();
        }

        private void ApplyCursorState()
        {
            // 마우스 FPS 탐험 중에만 커서를 잠근다.
            // 핸드트래킹 탐험은 포인터(검지 좌표)가 화면을 자유롭게 움직여야 하므로 잠그지 않는다.
            bool shouldLock = CurrentMode == InteractionMode.FPSExploration;
            Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shouldLock;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
