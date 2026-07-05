using UnityEngine;
using VirtualMuseum.Data;
using VirtualMuseum.Excavation;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 2m 반경 SphereCollider(Trigger). 플레이어 진입 시 "Q/E" 힌트를 표시하고
    /// StudentInteractionManager에 현재 근접 유물을 알린다.
    /// 유물 모델은 비동기 로드되므로 DurabilityController는 스포너가 로드 완료 후 등록한다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PedestalProximityTrigger : MonoBehaviour
    {
        [SerializeField] private GameObject interactionHintUI;
        [SerializeField] private float triggerRadius = 2f;

        private ArtifactData _artifactData;

        public ArtifactData CurrentArtifact => _artifactData;
        public ArtifactDurabilityController DurabilityController { get; private set; }

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = triggerRadius;
        }

        public void Initialize(ArtifactData data)
        {
            _artifactData = data;
        }

        /// <summary>모델 비동기 로드 완료 후 스포너가 호출.</summary>
        public void RegisterDurabilityController(ArtifactDurabilityController controller)
        {
            DurabilityController = controller;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (interactionHintUI != null) interactionHintUI.SetActive(true);
            StudentInteractionManager.Instance?.SetNearbyArtifact(_artifactData, this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (interactionHintUI != null) interactionHintUI.SetActive(false);
            StudentInteractionManager.Instance?.ClearNearbyArtifact(this);
        }
    }
}
