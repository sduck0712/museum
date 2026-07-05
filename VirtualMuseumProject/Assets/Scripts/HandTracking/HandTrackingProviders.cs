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
        void StartCapture();
        void StopCapture();
        HandLandmarkResult GetLatestResult();
    }

    /// <summary>
    /// MediaPipe Unity Plugin이 아직 설치되지 않았거나 웹캠 없이 데모를 체험하고 싶을 때 쓰는
    /// 목업 프로바이더. 마우스 커서 위치를 검지 끝 좌표로, 좌클릭을 핀치로 간주한다.
    /// InputModeManager 이하 전체 흐름(F키 토글 포함)을 별도 장비 없이 로컬에서 검증할 수 있다.
    /// </summary>
    public class MockHandLandmarkProvider : IHandLandmarkProvider
    {
        private bool _capturing;

        public void StartCapture() => _capturing = true;
        public void StopCapture() => _capturing = false;

        public HandLandmarkResult GetLatestResult()
        {
            if (!_capturing || Camera.main == null)
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
    /// 결과 저장소)까지만 구현했다.
    /// </summary>
    public class MediaPipeHandLandmarkProvider : IHandLandmarkProvider
    {
        private WebCamTexture _webCamTexture;
        private HandLandmarkResult _latestResult;

        public void StartCapture()
        {
            _webCamTexture = new WebCamTexture();
            _webCamTexture.Play();
            // 설치한 MediaPipe Unity Plugin의 그래프에 _webCamTexture 프레임을 매 프레임 전달하고,
            // 결과 콜백에서 SetLatestResult()를 호출하도록 연결할 것.
        }

        public void StopCapture()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying)
                _webCamTexture.Stop();
        }

        public void SetLatestResult(HandLandmarkResult result) => _latestResult = result;

        public HandLandmarkResult GetLatestResult() => _latestResult;
    }
}
