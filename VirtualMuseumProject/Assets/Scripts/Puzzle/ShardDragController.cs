using UnityEngine;
using VirtualMuseum.Input;

namespace VirtualMuseum.Puzzle
{
    /// <summary>
    /// 마우스(또는 핸드트래킹) 드래그로 조각을 옮기고, 놓았을 때 스냅 여부를 판정한다.
    /// </summary>
    public class ShardDragController : MonoBehaviour
    {
        [SerializeField] private Camera puzzleCamera;
        [SerializeField] private Transform puzzleRoot;
        [SerializeField] private LayerMask shardLayerMask;

        private ArtifactShard _draggingShard;
        private float _dragDepth;
        private bool _wasHeld;

        private void Update()
        {
            var pointerSource = InputModeManager.Instance.ActivePointerSource;
            if (pointerSource == null) return;

            bool isHeld = pointerSource.IsPrimaryActionHeld();
            Vector2 viewportPos = pointerSource.GetPointerViewportPosition();

            if (isHeld && !_wasHeld)
                TryBeginDrag(viewportPos);
            else if (isHeld && _draggingShard != null)
                UpdateDrag(viewportPos);
            else if (!isHeld && _draggingShard != null)
                EndDrag();

            _wasHeld = isHeld;
        }

        private void TryBeginDrag(Vector2 viewportPos)
        {
            Ray ray = puzzleCamera.ViewportPointToRay(viewportPos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, shardLayerMask))
            {
                var shard = hit.collider.GetComponent<ArtifactShard>();
                if (shard != null && !shard.IsSnapped)
                {
                    _draggingShard = shard;
                    _dragDepth = Vector3.Distance(puzzleCamera.transform.position, shard.transform.position);
                }
            }
        }

        private void UpdateDrag(Vector2 viewportPos)
        {
            Vector3 viewportPoint = new Vector3(viewportPos.x, viewportPos.y, _dragDepth);
            _draggingShard.transform.position = puzzleCamera.ViewportToWorldPoint(viewportPoint);
        }

        private void EndDrag()
        {
            _draggingShard.TryEvaluateSnap(puzzleRoot);
            _draggingShard = null;
        }
    }
}
