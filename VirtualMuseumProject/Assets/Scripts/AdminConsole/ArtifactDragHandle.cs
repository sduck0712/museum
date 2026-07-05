using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualMuseum.AdminConsole
{
    /// <summary>
    /// 관리자가 탑다운 뷰에서 유물 아이콘을 드래그해 좌대에 배치할 때 사용.
    /// 좌대(PedestalDropSlot) 1m 이내에서 손을 떼면 자동으로 배치 데이터가 저장된다.
    /// </summary>
    public class ArtifactDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string artifactID;
        [SerializeField] private float snapDistance = 1f;

        private Camera _topDownCamera;
        private float _dragPlaneHeight;

        private void Awake()
        {
            _topDownCamera = Camera.main;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragPlaneHeight = transform.position.y;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Ray ray = _topDownCamera.ScreenPointToRay(eventData.position);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, _dragPlaneHeight, 0f));

            if (plane.Raycast(ray, out float distance))
                transform.position = ray.GetPoint(distance);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var slot = FindClosestPedestalSlot();
            if (slot != null)
                slot.AssignArtifact(artifactID, transform.rotation);
        }

        private PedestalDropSlot FindClosestPedestalSlot()
        {
            PedestalDropSlot closest = null;
            float closestDist = float.MaxValue;

            foreach (var slot in FindObjectsOfType<PedestalDropSlot>())
            {
                float dist = Vector3.Distance(transform.position, slot.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = slot;
                }
            }

            return closestDist <= snapDistance ? closest : null;
        }
    }
}
