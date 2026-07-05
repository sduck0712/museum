using UnityEngine;
using UnityEngine.EventSystems;
using VirtualMuseum.Core;

namespace VirtualMuseum.AdminConsole
{
    /// <summary>
    /// 관리자가 탑다운 뷰에서 유물 토큰을 드래그해 좌대에 배치할 때 사용.
    /// 좌대(PedestalDropSlot) snapDistance 이내에서 손을 떼면 자동으로 배치 데이터가 저장된다.
    /// 카메라에 PhysicsRaycaster + 씬에 EventSystem이 있어야 드래그 이벤트가 발생한다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ArtifactDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private string artifactID;
        [SerializeField] private float snapDistance = 1.5f;

        private Camera _topDownCamera;
        private float _dragPlaneHeight;

        public string ArtifactID => artifactID;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_topDownCamera == null) _topDownCamera = Camera.main;
            _dragPlaneHeight = transform.position.y;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_topDownCamera == null) return;

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

            foreach (var slot in SceneQuery.FindAll<PedestalDropSlot>())
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
