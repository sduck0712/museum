using System.Collections.Generic;
using TriLibCore;
using UnityEngine;
using VirtualMuseum.Core;
using VirtualMuseum.Data;
using VirtualMuseum.Excavation;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 학생 씬 로드 시 ArtifactData 목록을 가져와 좌대를 생성하고,
    /// TriLib으로 STL 모델을 비동기 로드해 각 좌대 위에 배치한다.
    /// </summary>
    public class StudentArtifactSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject pedestalPrefab;
        [SerializeField] private Material excavationDirtMaterialTemplate; // ExcavationSurface 셰이더 적용 머티리얼

        private readonly Dictionary<string, GameObject> _spawnedPedestals = new();

        private async void Start()
        {
            List<ArtifactData> artifacts = await ServiceLocator.CurrentBackend.FetchMuseumLayoutAsync();
            foreach (var artifact in artifacts)
                SpawnPedestalAndArtifact(artifact);
        }

        private void SpawnPedestalAndArtifact(ArtifactData artifact)
        {
            var placement = artifact.placementData;
            GameObject pedestal = Instantiate(pedestalPrefab, placement.position, Quaternion.Euler(placement.rotation));
            pedestal.transform.localScale = placement.scale;
            pedestal.name = $"Pedestal_{placement.pedestalID}";

            var trigger = pedestal.GetComponentInChildren<PedestalProximityTrigger>();
            trigger?.Initialize(artifact);

            _spawnedPedestals[artifact.artifactID] = pedestal;
            LoadArtifactModelAsync(artifact, pedestal.transform);
        }

        private void LoadArtifactModelAsync(ArtifactData artifact, Transform parent)
        {
            var options = AssetLoader.CreateDefaultLoaderOptions();

            // stlFileURL은 1단계(로컬 목업)에서는 로컬 파일 경로.
            // 추후 클라우드 전환 시 원격 URL을 그대로 넘기면 TriLib이 다운로드 후 로드한다.
            AssetLoader.LoadModelFromFile(
                artifact.stlFileURL,
                context => OnArtifactLoaded(context, artifact, parent),
                null,
                null,
                error => Debug.LogError($"[StudentArtifactSpawner] {artifact.artifactID} 로드 실패: {error.GetInnerException()}"),
                null,
                options
            );
        }

        private void OnArtifactLoaded(AssetLoaderContext context, ArtifactData artifact, Transform parent)
        {
            GameObject model = context.RootGameObject;
            if (model == null) return;

            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.up * 0.5f; // 좌대 상단 기준 오프셋

            if (excavationDirtMaterialTemplate != null)
            {
                var instanceMaterial = new Material(excavationDirtMaterialTemplate);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.material = instanceMaterial;
            }

            var durabilityController = model.AddComponent<ArtifactDurabilityController>();
            durabilityController.Initialize(artifact);
        }
    }
}
