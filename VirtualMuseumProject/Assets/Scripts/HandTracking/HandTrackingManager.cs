using UnityEngine;

namespace VirtualMuseum.HandTracking
{
    /// <summary>
    /// 손 추적 온/오프를 관리하고 핀치 판정 및 뷰포트 좌표를 계산해 제공한다.
    /// useMockProvider = true(기본값)면 웹캠/MediaPipe 없이도 마우스로 시뮬레이션되어
    /// 별도 플러그인 설치 없이 전체 플로우를 로컬에서 데모/테스트할 수 있다.
    /// 활성화 중에는 화면 우측 하단에 상태 표시 + 포인터 오버레이를 그려 피드백을 준다.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        [SerializeField] private bool useMockProvider = true;
        [SerializeField] private float pinchThreshold = 0.05f;

        private IHandLandmarkProvider _provider;

        public bool IsPinching { get; private set; }
        public Vector2 IndexFingerViewportPosition { get; private set; } = new Vector2(0.5f, 0.5f);
        public bool IsActive { get; private set; }

        private void Awake()
        {
            _provider = useMockProvider
                ? (IHandLandmarkProvider)new MockHandLandmarkProvider()
                : new MediaPipeHandLandmarkProvider();
        }

        /// <returns>활성화 성공 여부. 실패(웹캠 미검출 등) 시 false를 반환하고 비활성 상태를 유지한다.</returns>
        public bool TryActivate()
        {
            if (IsActive) return true;

            if (_provider == null || !_provider.StartCapture())
            {
                IsActive = false;
                return false;
            }

            IsActive = true;
            return true;
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            IsPinching = false;
            _provider?.StopCapture();
        }

        private void Update()
        {
            if (!IsActive)
            {
                IsPinching = false;
                return;
            }

            HandLandmarkResult result = _provider.GetLatestResult();
            if (!result.IsHandDetected || result.Landmarks == null || result.Landmarks.Length < 21)
            {
                IsPinching = false; // 손을 잃으면 핀치 상태가 눌린 채로 남지 않도록 리셋
                return;
            }

            Vector2 thumbTip = result.Landmarks[4];
            Vector2 indexTip = result.Landmarks[8];

            IndexFingerViewportPosition = indexTip;
            IsPinching = Vector2.Distance(thumbTip, indexTip) <= pinchThreshold;
        }

        private void OnGUI()
        {
            if (!IsActive) return;

            // 우측 하단 상태 라벨 (스펙 7.2: 활성화 피드백 오버레이)
            string label = $"핸드트래킹 ON [{_provider.DisplayName}]  {(IsPinching ? "● 핀치" : "○")}";
            GUI.Box(new Rect(Screen.width - 320, Screen.height - 34, 312, 26), label);

            // 검지 포인터 시각화 (뷰포트 → GUI 좌표는 y축 반전)
            Vector2 vp = IndexFingerViewportPosition;
            float px = vp.x * Screen.width;
            float py = (1f - vp.y) * Screen.height;
            float size = IsPinching ? 22f : 14f;
            GUI.Box(new Rect(px - size / 2f, py - size / 2f, size, size), IsPinching ? "●" : "○");
        }

        private void OnDestroy()
        {
            _provider?.StopCapture();
        }
    }
}
