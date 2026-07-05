using System;
using System.Collections.Generic;
using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Puzzle
{
    /// <summary>
    /// 파손 시 shardCount만큼 조각을 생성해 트레이에 흩어 놓고,
    /// 모든 조각이 스냅되면 완료 콜백을 호출한다.
    /// 데모 단계에서는 조각의 정답 위치를 유물 바운딩 박스 기준 원형 배치로 절차적 생성한다.
    /// 실제 프로덕션에서는 아티스트가 제작한 파손 메시(fracture mesh)를 사용하는 것을 권장한다.
    /// </summary>
    public class ReassemblyPuzzleManager : MonoBehaviour
    {
        [SerializeField] private GameObject shardPrefab; // Collider + MeshRenderer + ArtifactShard 포함
        [SerializeField] private Transform trayScatterArea;
        [SerializeField] private Transform puzzleRoot;
        [SerializeField] private float scatterAreaRadius = 0.5f;
        [SerializeField] private float shardRingRadius = 0.3f;

        private readonly List<ArtifactShard> _activeShards = new();
        private Action _onCompleted;
        private bool _isRunning;

        public void BeginPuzzle(ArtifactData artifact, Action onCompleted)
        {
            _onCompleted = onCompleted;
            ClearShards();

            for (int i = 0; i < artifact.shardCount; i++)
            {
                Vector3 correctLocalPos = ProceduralShardOffset(i, artifact.shardCount);
                Quaternion correctLocalRot = Quaternion.Euler(0f, (360f / artifact.shardCount) * i, 0f);

                GameObject shardObj = Instantiate(shardPrefab, RandomScatterPosition(), UnityEngine.Random.rotation, trayScatterArea);
                var shard = shardObj.GetComponent<ArtifactShard>();
                shard.Setup(correctLocalPos, correctLocalRot);

                _activeShards.Add(shard);
            }

            _isRunning = true;
        }

        private void Update()
        {
            if (!_isRunning || _activeShards.Count == 0) return;

            bool allSnapped = true;
            foreach (var shard in _activeShards)
            {
                if (!shard.TryEvaluateSnap(puzzleRoot))
                    allSnapped = false;
            }

            if (allSnapped)
            {
                _isRunning = false;
                _onCompleted?.Invoke();
                ClearShards();
            }
        }

        private Vector3 ProceduralShardOffset(int index, int total)
        {
            float angle = (360f / total) * index * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * shardRingRadius, 0f, Mathf.Sin(angle) * shardRingRadius);
        }

        private Vector3 RandomScatterPosition()
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * scatterAreaRadius;
            return trayScatterArea.position + new Vector3(randomCircle.x, 0.1f, randomCircle.y);
        }

        private void ClearShards()
        {
            foreach (var shard in _activeShards)
                if (shard != null) Destroy(shard.gameObject);
            _activeShards.Clear();
        }
    }
}
