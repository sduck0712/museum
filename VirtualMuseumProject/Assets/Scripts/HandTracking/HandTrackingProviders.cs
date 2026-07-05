using UnityEngine;

namespace VirtualMuseum.HandTracking
{
    /// <summary>
    /// 손 랜드마크 21개 좌표(정규화 0~1) 결과.
    /// MediaPipe Hands 랜드마크 인덱스 규격을 따른다 (4=엄지 끝, 8=검지 끝).
    /// </summary>
    public struct HandLandmarkResult
    {
        public bool IsHandDetected;
        public Vector2[] Landmarks; // length 21
    }

    public interface IHandLandmarkProvider
    {
        /// <returns>캡처 시작 성공 여부 (웹캠 미검출 등 실패 시 false)</returns>
        bool StartCapture();
        void StopCapture();
        HandLandmarkResult GetLatestResult();
        string DisplayName { get; }
    }

    /// <summary>
    /// MediaPipe Unity Plugin이 아직 설치되지 않았거나 웹캠 없이 데모를 체험하고 싶을 때 쓰는
    /// 목업 프로바이더. 마우스 커서 위치를 검지 끝 좌표로, 좌클릭을 핀치로 간주한다.
    /// InputModeManager 이하 전체 흐름(F키 토글 포함)을 별도 장비 없이 로컬에서 검증할 수 있다.
    /// </summary>
    public class MockHandLandmarkProvider : IHandLandmarkProvider
    {
        private bool _capturing;

        public string DisplayName => "Mock (마우스 시뮬레이션)";

        public bool StartCapture()
        {
            _capturing = true;
            return true;
        }

        public void StopCapture() => _capturing = false;

        public HandLandmarkResult GetLatestResult()
        {
            if (!_capturing)
                return new HandLandmarkResult { IsHandDetected = false, Landmarks = new Vector2[21] };

            Vector3 screenPos = UnityEngine.Input.mousePosition;
            Vector2 viewportPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            var landmarks = new Vector2[21];
            landmarks[8] = viewportPos; // 검지 끝

            // 좌클릭 중이면 엄지 끝을 검지에 근접시켜 핀치 상태를 시뮬레이션
            landmarks[4] = UnityEngine.Input.GetMouseButton(0)
                ? viewportPos
                : viewportPos + new Vector2(0.1f, 0.1f);

            return new HandLandmarkResult { IsHandDetected = true, Landmarks = landmarks };
        }
    }

    /// <summary>
    /// 실제 웹캠 + MediaPipe Hands 연동 프로바이더.
    /// MediaPipe Unity Plugin(예: homuler/MediaPipeUnityPlugin) 설치 후,
    /// 해당 플러그인의 그래프 실행 콜백에서 SetLatestResult()를 호출하도록 연결해야 한다.
    /// 콜백 시그니처는 플러그인 버전마다 다르므로 여기서는 연동 지점(WebCamTexture 준비,
    /// 결과 저장소, 좌우반전 보정 유틸)까지만 구현했다.
    /// </summary>
    public class MediaPipeHandLandmarkProvider : IHandLandmarkProvider
    {
        private WebCamTexture _webCamTexture;
        private HandLandmarkResult _latestResult;

        public string DisplayName => "Webcam + MediaPipe";

        public WebCamTexture WebCamTexture => _webCamTexture;

        public bool StartCapture()
        {
            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                Debug.LogWarning("[MediaPipeHandLandmarkProvider] 사용 가능한 웹캠이 없습니다.");
                return false;
            }

            _webCamTexture = new WebCamTexture(WebCamTexture.devices[0].name, 640, 480, 30);
            _webCamTexture.Play();

            // TODO(MediaPipe 연동): 설치한 MediaPipe Unity Plugin의 그래프에 _webCamTexture 프레임을
            // 매 프레임 전달하고, 결과 콜백에서 SetLatestResult()를 호출하도록 연결할 것.
            return _webCamTexture.isPlaying;
        }

        public void StopCapture()
        {
            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying)
                    _webCamTexture.Stop();
                Object.Destroy(_webCamTexture); // WebCamTexture 리소스 해제
                _webCamTexture = null;
            }
        }

        /// <summary>
        /// MediaPipe 결과 콜백에서 호출. 웹캠 영상은 거울상이므로 x 좌표를 반전해 넘길 것:
        /// viewportX = 1f - mediaPipeNormalizedX
        /// </summary>
        public void SetLatestResult(HandLandmarkResult result) => _latestResult = result;

        public HandLandmarkResult GetLatestResult() => _latestResult;
    }
}
