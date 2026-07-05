using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VirtualMuseum.Core;
using VirtualMuseum.Data;
using VirtualMuseum.Excavation;
using VirtualMuseum.ModelLoading;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 학생 씬 로드 시 ArtifactData 목록을 가져와 좌대를 생성하고,
    /// 모델을 비동기 로드해 각 좌대 위에 배치한다.
    ///
    /// STL 경로가 비어 있거나 파일이 없으면(초기 더미 데이터 상태) 유물별로 구분되는
    /// 절차적 플레이스홀더 메시를 생성해 데모가 항상 동작하도록 보장한다.
    /// </summary>
    public class StudentArtifactSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject pedestalPrefab;
        [SerializeField] private Material excavationDirtMaterialTemplate; // ExcavationSurface 셰이더 적용 머티리얼
        [SerializeField] private float pedestalTopHeight = 1f;
        [SerializeField] private float artifactTargetHeight = 0.7f;

        private readonly Dictionary<string, GameObject> _spawnedPedestals = new();

        private static readonly PrimitiveType[] PlaceholderShapes =
        {
            PrimitiveType.Capsule, PrimitiveType.Cylinder, PrimitiveType.Cube,
            PrimitiveType.Sphere, PrimitiveType.Capsule
        };

        private async void Start()
        {
            try
            {
                List<ArtifactData> artifacts = await ServiceLocator.CurrentBackend.FetchMuseumLayoutAsync();
                if (this == null) return; // 로드 중 씬 전환 대비

                for (int i = 0; i < artifacts.Count; i++)
                    await SpawnPedestalAndArtifactAsync(artifacts[i], i);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StudentArtifactSpawner] 박물관 레이아웃 로드 실패: {e}");
            }
        }

        private async Task SpawnPedestalAndArtifactAsync(ArtifactData artifact, int index)
        {
            var placement = artifact.placementData;
            GameObject pedestal = Instantiate(pedestalPrefab, placement.position, Quaternion.Euler(placement.rotation));
            pedestal.transform.localScale = placement.scale;
            pedestal.name = $"Pedestal_{placement.pedestalID}";

            var trigger = pedestal.GetComponentInChildren<PedestalProximityTrigger>();
            trigger?.Initialize(artifact);

            _spawnedPedestals[artifact.artifactID] = pedestal;

            GameObject model = await ArtifactModelLoader.LoadAsync(artifact.stlFileURL);
            if (this == null) { if (model != null) Destroy(model); return; }

            if (model == null)
                model = CreatePlaceholderModel(artifact, index);

            AttachModelToPedestal(model, pedestal.transform, artifact, trigger);
        }

        private GameObject CreatePlaceholderModel(ArtifactData artifact, int index)
        {
            var shape = PlaceholderShapes[index % PlaceholderShapes.Length];
            var go = GameObject.CreatePrimitive(shape);
            go.name = $"Placeholder_{artifact.artifactID}";

            // hit.textureCoord 기반 발굴 페인팅에는 MeshCollider가 필요하므로 기본 콜라이더 교체
            var primitiveCollider = go.GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);
            var meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;

            return go;
        }

        private void AttachModelToPedestal(GameObject model, Transform pedestal, ArtifactData artifact,
            PedestalProximityTrigger trigger)
        {
            model.transform.SetParent(pedestal, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // 발굴 마스킹 머티리얼을 인스턴스로 분리 적용 (유물별 마스크 독립)
            if (excavationDirtMaterialTemplate != null)
            {
                var instanceMaterial = new Material(excavationDirtMaterialTemplate);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.material = instanceMaterial;
            }

            NormalizeAndPlaceOnTop(model, pedestal);

            var durabilityController = model.AddComponent<ArtifactDurabilityController>();
            durabilityController.Initialize(artifact);
            trigger?.RegisterDurabilityController(durabilityController);
        }

        private void NormalizeAndPlaceOnTop(GameObject model, Transform pedestal)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                model.transform.localPosition = Vector3.up * pedestalTopHeight;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // 목표 높이에 맞춰 스케일 정규화
            if (bounds.size.y > 0.0001f)
                model.transform.localScale *= artifactTargetHeight / bounds.size.y;

            // 스케일 반영 후 바운즈 재계산 → 바닥이 좌대 상판에 닿도록 배치
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float desiredBottomY = pedestal.position.y + pedestalTopHeight;
            model.transform.position += Vector3.up * (desiredBottomY - bounds.min.y);

            // 좌대 중심축 정렬
            Vector3 centerOffset = bounds.center - pedestal.position;
            model.transform.position -= new Vector3(centerOffset.x, 0f, centerOffset.z);
        }
    }
}
