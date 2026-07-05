using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Puzzle;

namespace VirtualMuseum.Excavation
{
    /// <summary>
    /// 발굴 미니게임 오버레이의 최상위 컨트롤러.
    /// 카메라 줌, 도구 선택, 상태 전이(파손→퍼즐→복원)를 총괄한다.
    /// </summary>
    public class ExcavationSessionController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera excavationCamera;
        [SerializeField] private float minFov = 30f;
        [SerializeField] private float maxFov = 90f;
        [SerializeField] private float zoomSpeed = 15f;

        [Header("Gameplay")]
        [SerializeField] private ExcavationBrush brush;
        [SerializeField] private ReassemblyPuzzleManager puzzleManager;
        [SerializeField] private ParticleSystem successParticles;

        private ArtifactDurabilityController _durabilityController;

        public void BeginSession(ArtifactData artifact, Transform pedestalTransform)
        {
            _durabilityController = pedestalTransform.GetComponentInChildren<ArtifactDurabilityController>();
            if (_durabilityController == null)
            {
                Debug.LogWarning("[ExcavationSessionController] ArtifactDurabilityController를 찾을 수 없습니다.");
                return;
            }

            _durabilityController.RuntimeState.state = ExcavationState.Interacting;
            _durabilityController.OnBroken += HandleBroken;
            _durabilityController.OnFullyCleared += HandleFullyCleared;

            brush.enabled = true;
            brush.SetTool(artifact.recommendedToolType);
        }

        private void Update()
        {
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f && excavationCamera != null)
            {
                excavationCamera.fieldOfView = Mathf.Clamp(
                    excavationCamera.fieldOfView - scroll * zoomSpeed, minFov, maxFov);
            }
        }

        public void SelectTool(Data.ExcavationToolType toolType) => brush.SetTool(toolType);

        private void HandleBroken()
        {
            brush.enabled = false;
            puzzleManager.BeginPuzzle(_durabilityController.ArtifactInfo, OnPuzzleCompleted);
        }

        private void OnPuzzleCompleted()
        {
            _durabilityController.NotifyRepaired();
            brush.enabled = true; // 복원 후 발굴 계속 진행 가능 (마스크는 이미 대부분 제거된 상태)
        }

        private void HandleFullyCleared()
        {
            successParticles?.Play();
            Vault.VaultManager.Instance.AddArtifact(_durabilityController.ArtifactInfo, _durabilityController.RuntimeState.isRestored);
        }

        private void OnDisable()
        {
            if (_durabilityController != null)
            {
                _durabilityController.OnBroken -= HandleBroken;
                _durabilityController.OnFullyCleared -= HandleFullyCleared;
            }
        }
    }
}
