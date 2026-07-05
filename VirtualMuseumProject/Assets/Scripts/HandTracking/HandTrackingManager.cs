using UnityEngine;

namespace VirtualMuseum.HandTracking
{
    /// <summary>
    /// 손 추적 온/오프를 관리하고 핀치 판정 및 뷰포트 좌표를 계산해 제공한다.
    /// useMockProvider = true(기본값)면 웹캠/MediaPipe 없이도 마우스로 시뮬레이션되어
    /// 별도 플러그인 설치 없이 전체 플로우를 로컬에서 데모/테스트할 수 있다.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        [SerializeField] private bool useMockProvider = true;
        [SerializeField] private float pinchThreshold = 0.05f;

        private IHandLandmarkProvider _provider;
        private HandLandmarkResult _lastResult;

        public bool IsPinching { get; private set; }
        public Vector2 IndexFingerViewportPosition { get; private set; }
        public bool IsActive { get; private set; }

        private void Awake()
        {
            _provider = useMockProvider
                ? new MockHandLandmarkProvider()
                : new MediaPipeHandLandmarkProvider();
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            if (active) _provider.StartCapture();
            else _provider.StopCapture();
        }

        private void Update()
        {
            if (!IsActive) return;

            _lastResult = _provider.GetLatestResult();
            if (!_lastResult.IsHandDetected) return;

            Vector2 thumbTip = _lastResult.Landmarks[4];
            Vector2 indexTip = _lastResult.Landmarks[8];

            IndexFingerViewportPosition = indexTip;
            IsPinching = Vector2.Distance(thumbTip, indexTip) <= pinchThreshold;
        }

        private void OnDestroy()
        {
            _provider?.StopCapture();
        }
    }
}
