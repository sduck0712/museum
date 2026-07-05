// MediaPipe Unity Plugin(homuler/MediaPipeUnityPlugin) 실연동 어댑터.
//
// 활성화 방법 (자세한 단계는 Docs/MediaPipe_Integration.md 참고):
//  1. MediaPipeUnityPlugin 설치 (UPM git URL 또는 릴리스 .unitypackage)
//  2. hand_landmarker.task 모델을 Assets/StreamingAssets/mediapipe/ 에 배치
//  3. Player Settings > Scripting Define Symbols 에 MEDIAPIPE 추가
//  4. HandTrackingManager 인스펙터에서 useMockProvider = false
//
// 주의: 플러그인 버전에 따라 Tasks API 네임스페이스/시그니처가 다를 수 있다.
// 이 파일은 v0.14+ Tasks API 기준으로 작성했으며, 컴파일 오류 발생 시
// Docs/MediaPipe_Integration.md의 버전별 체크리스트를 따라 조정할 것.
#if MEDIAPIPE
using System;
using System.IO;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;

namespace VirtualMuseum.HandTracking
{
    /// <summary>
    /// 웹캠 프레임을 MediaPipe HandLandmarker(LIVE_STREAM 모드)에 전달하고,
    /// 결과 콜백에서 21개 랜드마크를 HandLandmarkResult로 변환한다.
    /// 웹캠은 거울상이므로 x 좌표를 반전(1 - x)해 화면 포인터와 방향을 일치시킨다.
    /// </summary>
    public class MediaPipeTasksHandProvider : IHandLandmarkProvider
    {
        private WebCamTexture _webCamTexture;
        private HandLandmarker _landmarker;
        private Color32[] _pixelBuffer;
        private HandLandmarkResult _latestResult;
        private long _lastTimestampMs;

        public string DisplayName => "Webcam + MediaPipe Tasks";

        public bool StartCapture()
        {
            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                Debug.LogWarning("[MediaPipeTasksHandProvider] 사용 가능한 웹캠이 없습니다.");
                return false;
            }

            string modelPath = Path.Combine(Application.streamingAssetsPath, "mediapipe", "hand_landmarker.task");
            if (!File.Exists(modelPath))
            {
                Debug.LogWarning($"[MediaPipeTasksHandProvider] 모델 파일이 없습니다: {modelPath}");
                return false;
            }

            try
            {
                var options = new HandLandmarkerOptions(
                    baseOptions: new BaseOptions(modelAssetPath: modelPath),
                    runningMode: RunningMode.LIVE_STREAM,
                    numHands: 1,
                    resultCallback: OnResult);
                _landmarker = HandLandmarker.CreateFromOptions(options);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MediaPipeTasksHandProvider] HandLandmarker 생성 실패: {e}");
                return false;
            }

            _webCamTexture = new WebCamTexture(WebCamTexture.devices[0].name, 640, 480, 30);
            _webCamTexture.Play();
            return _webCamTexture.isPlaying;
        }

        public void StopCapture()
        {
            _landmarker?.Close();
            _landmarker = null;

            if (_webCamTexture != null)
            {
                if (_webCamTexture.isPlaying) _webCamTexture.Stop();
                UnityEngine.Object.Destroy(_webCamTexture);
                _webCamTexture = null;
            }
        }

        public HandLandmarkResult GetLatestResult()
        {
            PumpFrame(); // HandTrackingManager.Update 주기에 맞춰 프레임 공급
            return _latestResult;
        }

        private void PumpFrame()
        {
            if (_landmarker == null || _webCamTexture == null || !_webCamTexture.didUpdateThisFrame)
                return;

            long timestampMs = (long)(Time.realtimeSinceStartup * 1000f);
            if (timestampMs <= _lastTimestampMs) return; // MediaPipe는 단조 증가 타임스탬프 요구
            _lastTimestampMs = timestampMs;

            if (_pixelBuffer == null || _pixelBuffer.Length != _webCamTexture.width * _webCamTexture.height)
                _pixelBuffer = new Color32[_webCamTexture.width * _webCamTexture.height];
            _webCamTexture.GetPixels32(_pixelBuffer);

            using var image = new Image(
                ImageFormat.Types.Format.Srgba,
                _webCamTexture.width, _webCamTexture.height,
                _webCamTexture.width * 4,
                MemoryMarshalUtil.ToByteArray(_pixelBuffer)); // 플러그인 버전에 따라 Image 생성자 조정

            _landmarker.DetectAsync(image, timestampMs);
        }

        private void OnResult(HandLandmarkerResult result, Image image, long timestampMs)
        {
            var landmarks = new Vector2[21];
            bool detected = result.handLandmarks != null && result.handLandmarks.Count > 0;

            if (detected)
            {
                var hand = result.handLandmarks[0];
                int count = Mathf.Min(21, hand.landmarks.Count);
                for (int i = 0; i < count; i++)
                {
                    var lm = hand.landmarks[i];
                    // 좌우반전 보정(거울상) + MediaPipe y(위=0) → Unity 뷰포트 y(아래=0)
                    landmarks[i] = new Vector2(1f - lm.x, 1f - lm.y);
                }
            }

            // 콜백은 워커 스레드에서 올 수 있으나 struct 대입은 원자적으로 안전한 수준
            _latestResult = new HandLandmarkResult { IsHandDetected = detected, Landmarks = landmarks };
        }
    }

    /// <summary>Color32[] → byte[] 변환 헬퍼 (플러그인에 동등 유틸이 있으면 그것을 사용할 것).</summary>
    internal static class MemoryMarshalUtil
    {
        public static byte[] ToByteArray(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }
            return bytes;
        }
    }
}
#endif
