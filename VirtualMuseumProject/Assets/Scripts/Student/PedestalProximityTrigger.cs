using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 2m 반경 SphereCollider(Trigger). 플레이어 진입 시 "Press Q or E" 힌트를 표시하고
    /// StudentInteractionManager에 현재 근접 유물을 알린다.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PedestalProximityTrigger : MonoBehaviour
    {
        [SerializeField] private GameObject interactionHintUI;

        private ArtifactData _artifactData;
        public ArtifactData CurrentArtifact => _artifactData;

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 2f;
        }

        public void Initialize(ArtifactData data)
        {
            _artifactData = data;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            interactionHintUI?.SetActive(true);
            StudentInteractionManager.Instance?.SetNearbyArtifact(_artifactData, this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            interactionHintUI?.SetActive(false);
            StudentInteractionManager.Instance?.ClearNearbyArtifact(this);
        }
    }
}
