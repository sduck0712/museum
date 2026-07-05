using System;
using System.Collections.Generic;
using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Puzzle
{
    /// <summary>
    /// 파손 시 shardCount만큼 조각을 생성해 유물 주변에 흩어 놓고,
    /// 모든 조각이 스냅되면 완료 콜백을 호출한다.
    ///
    /// - 완료 판정은 ShardDragController가 조각을 놓는 순간에만 수행 (매 프레임 자동 스냅 버그 수정)
    /// - showGhostSilhouette 옵션: 정답 위치에 반투명 고스트 실루엣 표시 (스펙 5.5 힌트 기능)
    /// - shardPrefab을 지정하지 않으면 절차적 큐브 조각을 생성해 씬 배선 없이도 동작
    ///
    /// 데모 단계에서는 조각 정답 위치를 유물 기준 원형 배치로 절차적 생성한다.
    /// 실제 프로덕션에서는 아티스트 제작 파손 메시(fracture mesh)를 사용하는 것을 권장한다.
    /// </summary>
    public class ReassemblyPuzzleManager : MonoBehaviour
    {
        [SerializeField] private GameObject shardPrefab; // 비워두면 절차적 조각 생성
        [SerializeField] private bool showGhostSilhouette = true;
        [SerializeField] private float scatterRadius = 0.7f;
        [SerializeField] private float shardRingRadius = 0.25f;
        [SerializeField] private float snapPositionTolerance = 0.15f;
        [SerializeField] private float snapRotationToleranceDegrees = 30f;

        private readonly List<ArtifactShard> _activeShards = new();
        private readonly List<GameObject> _ghosts = new();
        private Action _onCompleted;
        private Material _proceduralShardMaterial;
        private Material _ghostMaterial;

        public bool IsRunning { get; private set; }
        public Transform PuzzleRoot { get; private set; }
        public int SnappedCount
        {
            get
            {
                int n = 0;
                foreach (var s in _activeShards) if (s != null && s.IsSnapped) n++;
                return n;
            }
        }
        public int TotalCount => _activeShards.Count;

        public void BeginPuzzle(ArtifactData artifact, Transform puzzleRoot, Action onCompleted)
        {
            AbortPuzzle();

            PuzzleRoot = puzzleRoot;
            _onCompleted = onCompleted;

            int count = Mathf.Max(2, artifact.shardCount);
            int shardLayer = LayerMask.NameToLayer("Shard");

            for (int i = 0; i < count; i++)
            {
                Vector3 correctLocalPos = ProceduralShardOffset(i, count);
                Quaternion correctLocalRot = Quaternion.Euler(0f, (360f / count) * i, 0f);

                GameObject shardObj = CreateShardObject(i);
                if (shardLayer >= 0) SetLayerRecursive(shardObj, shardLayer);

                shardObj.transform.position = RandomScatterPosition(puzzleRoot);
                shardObj.transform.rotation = UnityEngine.Random.rotation;

                var shard = shardObj.GetComponent<ArtifactShard>();
                if (shard == null) shard = shardObj.AddComponent<ArtifactShard>();
                shard.Setup(correctLocalPos, correctLocalRot);
                shard.SetTolerances(snapPositionTolerance, snapRotationToleranceDegrees);
                _activeShards.Add(shard);

                if (showGhostSilhouette)
                    CreateGhost(shard);
            }

            IsRunning = true;
        }

        /// <summary>조각을 놓을 때 ShardDragController가 호출. 전부 스냅되면 완료 처리.</summary>
        public void NotifyShardReleased()
        {
            if (!IsRunning || _activeShards.Count == 0) return;

            foreach (var shard in _activeShards)
                if (shard == null || !shard.IsSnapped) return;

            IsRunning = false;
            var callback = _onCompleted;
            _onCompleted = null;
            ClearAll();
            callback?.Invoke();
        }

        /// <summary>세션 중도 이탈 시 조각 정리 (Broken 상태는 유지되어 재진입 시 재개).</summary>
        public void AbortPuzzle()
        {
            IsRunning = false;
            _onCompleted = null;
            ClearAll();
        }

        private GameObject CreateShardObject(int index)
        {
            if (shardPrefab != null)
                return Instantiate(shardPrefab);

            // 절차적 조각: 배선 없이도 데모가 돌아가도록 크기가 조금씩 다른 큐브 생성
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Shard_{index}";
            float s = 0.1f + 0.04f * (index % 3);
            go.transform.localScale = new Vector3(s, s * 0.8f, s * 1.1f);

            if (_proceduralShardMaterial == null)
            {
                _proceduralShardMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(0.72f, 0.5f, 0.38f) // 테라코타 톤
                };
            }
            go.GetComponent<Renderer>().sharedMaterial = _proceduralShardMaterial;
            return go;
        }

        private void CreateGhost(ArtifactShard shard)
        {
            var meshFilter = shard.GetComponentInChildren<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            if (_ghostMaterial == null)
            {
                var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
                if (shader == null) shader = Shader.Find("Standard");
                _ghostMaterial = new Material(shader) { color = new Color(1f, 1f, 1f, 0.22f) };
            }

            shard.GetTargetWorldPose(PuzzleRoot, out Vector3 pos, out Quaternion rot);

            var ghost = new GameObject($"Ghost_{shard.name}");
            ghost.transform.SetPositionAndRotation(pos, rot);
            ghost.transform.localScale = shard.transform.localScale;
            ghost.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;
            ghost.AddComponent<MeshRenderer>().sharedMaterial = _ghostMaterial;
            _ghosts.Add(ghost);
        }

        private Vector3 ProceduralShardOffset(int index, int total)
        {
            float angle = (360f / total) * index * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * shardRingRadius, 0.1f, Mathf.Sin(angle) * shardRingRadius);
        }

        private Vector3 RandomScatterPosition(Transform root)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized *
                                   UnityEngine.Random.Range(scatterRadius * 0.6f, scatterRadius);
            return root.position + new Vector3(randomCircle.x, 0.15f, randomCircle.y);
        }

        private void ClearAll()
        {
            foreach (var shard in _activeShards)
                if (shard != null) Destroy(shard.gameObject);
            _activeShards.Clear();

            foreach (var ghost in _ghosts)
                if (ghost != null) Destroy(ghost);
            _ghosts.Clear();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private void OnDestroy()
        {
            ClearAll();
            if (_proceduralShardMaterial != null) Destroy(_proceduralShardMaterial);
            if (_ghostMaterial != null) Destroy(_ghostMaterial);
        }

        private void OnGUI()
        {
            if (!IsRunning) return;
            GUI.Box(new Rect(Screen.width / 2f - 160, 16, 320, 28),
                $"재조립 퍼즐: {SnappedCount}/{TotalCount} 조각 완료 - 조각을 실루엣 위치로 드래그하세요");
        }
    }
}
