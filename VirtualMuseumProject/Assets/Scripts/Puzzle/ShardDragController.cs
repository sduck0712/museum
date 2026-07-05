using UnityEngine;
using VirtualMuseum.Core;
using VirtualMuseum.Input;

namespace VirtualMuseum.Puzzle
{
    /// <summary>
    /// 마우스(또는 핸드트래킹 핀치) 드래그로 조각을 옮기고, 놓았을 때 스냅 여부를 판정한다.
    ///
    /// 조각은 파손 연출로 랜덤 회전 상태로 흩어지는데, 마우스 드래그만으로는 회전을
    /// 제어할 수 없으므로 드래그 중 정답 회전으로 서서히 정렬해 주는 보조(rotation assist)를
    /// 적용한다 — 이것이 없으면 회전 허용 오차 판정을 사실상 통과할 수 없다.
    /// </summary>
    public class ShardDragController : MonoBehaviour
    {
        [SerializeField] private ReassemblyPuzzleManager puzzleManager;
        [SerializeField] private LayerMask shardLayerMask; // 0(Nothing)이면 Everything으로 대체
        [SerializeField] private float rotationAssistSpeed = 5f;

        private Camera _camera;
        private ArtifactShard _draggingShard;
        private float _dragDepth;
        private bool _wasHeld;

        private void Awake()
        {
            if (puzzleManager == null)
                puzzleManager = GetComponent<ReassemblyPuzzleManager>() ?? SceneQuery.Find<ReassemblyPuzzleManager>();
        }

        private void Update()
        {
            if (puzzleManager == null || !puzzleManager.IsRunning) return;

            var modeManager = InputModeManager.Instance;
            if (modeManager == null) return;

            var pointerSource = modeManager.ActivePointerSource;
            if (pointerSource == null) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            bool isHeld = pointerSource.IsPrimaryActionHeld() && GUIUtility.hotControl == 0;
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
            Ray ray = _camera.ViewportPointToRay(viewportPos);
            int mask = shardLayerMask.value == 0 ? ~0 : shardLayerMask.value;

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, mask))
            {
                var shard = hit.collider.GetComponentInParent<ArtifactShard>();
                if (shard != null && !shard.IsSnapped)
                {
                    _draggingShard = shard;
                    _dragDepth = Vector3.Distance(_camera.transform.position, shard.transform.position);
                }
            }
        }

        private void UpdateDrag(Vector2 viewportPos)
        {
            Vector3 viewportPoint = new Vector3(viewportPos.x, viewportPos.y, _dragDepth);
            _draggingShard.transform.position = _camera.ViewportToWorldPoint(viewportPoint);

            // 회전 보조: 드래그하는 동안 정답 방향으로 부드럽게 정렬
            _draggingShard.GetTargetWorldPose(puzzleManager.PuzzleRoot, out _, out Quaternion targetRot);
            _draggingShard.transform.rotation = Quaternion.Slerp(
                _draggingShard.transform.rotation, targetRot, Time.deltaTime * rotationAssistSpeed);
        }

        private void EndDrag()
        {
            _draggingShard.TryEvaluateSnap(puzzleManager.PuzzleRoot);
            _draggingShard = null;
            puzzleManager.NotifyShardReleased(); // 완료 판정은 놓는 순간에만 수행
        }
    }
}
