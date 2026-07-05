using System.IO;
using System.Threading.Tasks;
using UnityEngine;

#if TRILIB
using TriLibCore;
#endif

namespace VirtualMuseum.ModelLoading
{
    /// <summary>
    /// 런타임 3D 모델 로딩의 단일 진입점.
    /// - .stl: 내장 SimpleStlLoader 사용 (외부 에셋 불필요 → 데모가 TriLib 없이 동작)
    /// - 기타 포맷: TriLib 2 설치 + Scripting Define Symbols에 TRILIB 추가 시 지원
    /// 반환된 GameObject에는 발굴 브러시(UV 레이캐스트)용 MeshCollider가 부착된다.
    /// </summary>
    public static class ArtifactModelLoader
    {
        public static async Task<GameObject> LoadAsync(string filePath, Material material = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".stl")
                return await LoadStlAsync(filePath, material);

#if TRILIB
            return await LoadWithTriLibAsync(filePath);
#else
            Debug.LogWarning($"[ArtifactModelLoader] '{ext}' 포맷은 TriLib 설치 + TRILIB 심볼 정의 후 지원됩니다: {filePath}");
            return null;
#endif
        }

        private static async Task<GameObject> LoadStlAsync(string filePath, Material material)
        {
            Mesh mesh = await SimpleStlLoader.LoadMeshAsync(filePath);
            if (mesh == null)
            {
                Debug.LogError($"[ArtifactModelLoader] STL 파싱 실패: {filePath}");
                return null;
            }

            var go = new GameObject(Path.GetFileNameWithoutExtension(filePath));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material != null ? material : new Material(Shader.Find("Standard"));

            // hit.textureCoord 기반 발굴 페인팅에는 MeshCollider가 필수
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

#if TRILIB
        private static Task<GameObject> LoadWithTriLibAsync(string filePath)
        {
            var tcs = new TaskCompletionSource<GameObject>();
            var options = AssetLoader.CreateDefaultLoaderOptions();

            AssetLoader.LoadModelFromFile(
                filePath,
                onLoad: context =>
                {
                    GameObject root = context.RootGameObject;
                    if (root != null)
                    {
                        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                        {
                            var col = mf.gameObject.GetComponent<MeshCollider>();
                            if (col == null) col = mf.gameObject.AddComponent<MeshCollider>();
                            col.sharedMesh = mf.sharedMesh;
                        }
                    }
                    tcs.TrySetResult(root);
                },
                onMaterialsLoad: null,
                onProgress: null,
                onError: error =>
                {
                    Debug.LogError($"[ArtifactModelLoader] TriLib 로드 실패: {error.GetInnerException()}");
                    tcs.TrySetResult(null);
                },
                wrapperGameObject: null,
                assetLoaderOptions: options
            );

            return tcs.Task;
        }
#endif
    }
}
