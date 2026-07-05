using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using VirtualMuseum.Data;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 정보 패널(Q키) 내용 바인딩: 유물 이름/등급/설명 + videoURL이 있으면 영상 재생.
    /// VideoPlayer용 RenderTexture는 패널이 닫힐 때 해제한다 (메모리 관리).
    /// </summary>
    public class InfoPanelView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text rarityText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private Button closeButton;

        private VideoPlayer _videoPlayer;
        private RenderTexture _videoRT;

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(() => StudentInteractionManager.Instance?.CloseInfoPanel());
        }

        public void Bind(ArtifactData artifact)
        {
            if (artifact == null) return;

            if (nameText != null) nameText.text = artifact.artifactName;
            if (rarityText != null) rarityText.text = $"희귀도: {artifact.rarityTier}  |  발굴 난이도: {artifact.excavationDifficulty}";
            if (descriptionText != null) descriptionText.text = artifact.historicalDescription;

            SetupVideo(artifact.videoURL);
        }

        private void SetupVideo(string url)
        {
            bool hasVideo = !string.IsNullOrWhiteSpace(url) && videoImage != null;
            if (videoImage != null) videoImage.gameObject.SetActive(hasVideo);
            if (!hasVideo) return;

            if (_videoPlayer == null)
            {
                _videoPlayer = gameObject.AddComponent<VideoPlayer>();
                _videoPlayer.playOnAwake = false;
                _videoPlayer.isLooping = true;
                _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                _videoPlayer.errorReceived += (_, message) =>
                    Debug.LogWarning($"[InfoPanelView] 영상 재생 실패: {message}");
            }

            if (_videoRT == null)
            {
                _videoRT = new RenderTexture(640, 360, 0);
                _videoRT.Create();
            }

            _videoPlayer.targetTexture = _videoRT;
            videoImage.texture = _videoRT;
            _videoPlayer.url = url;
            _videoPlayer.Play();
        }

        private void OnDisable()
        {
            if (_videoPlayer != null && _videoPlayer.isPlaying)
                _videoPlayer.Stop();

            if (_videoRT != null)
            {
                _videoRT.Release();
                Destroy(_videoRT);
                _videoRT = null;
            }
        }
    }
}
