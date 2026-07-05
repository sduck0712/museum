using System;
using System.Collections.Generic;
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
    ///
    /// 대용량 파일 대응:
    /// - 메시 캐시: 여러 좌대가 같은 STL을 참조해도 파싱/메모리는 1회분만 사용
    ///   (경로 + 파일 수정시각 키 → 관리자가 같은 경로에 재업로드하면 자동 무효화)
    /// - MeshCollider 쿠킹(Physics.BakeMesh)을 백그라운드 스레드에서 선처리해
    ///   수십만 삼각형 모델도 메인 스레드 멈춤 없이 콜라이더를 생성
    /// </summary>
    public static class ArtifactModelLoader
    {
        private static readonly Dictionary<string, Mesh> _meshCache = new();

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
            Mesh mesh = await GetOrLoadStlMeshAsync(filePath);
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
            // (물리 데이터는 GetOrLoadStlMeshAsync에서 이미 백그라운드 베이크됨 → 여기서는 재쿠킹 없음)
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        private static async Task<Mesh> GetOrLoadStlMeshAsync(string filePath)
        {
            string cacheKey;
            try
            {
                cacheKey = $"{Path.GetFullPath(filePath)}|{File.GetLastWriteTimeUtc(filePath).Ticks}";
            }
            catch (Exception)
            {
                cacheKey = filePath;
            }

            if (_meshCache.TryGetValue(cacheKey, out Mesh cached) && cached != null)
                return cached;

            Mesh mesh = await SimpleStlLoader.LoadMeshAsync(filePath);
            if (mesh == null) return null;

            // MeshCollider 물리 데이터를 백그라운드에서 선쿠킹 (50만 삼각형급에서 메인 스레드 수 초 멈춤 방지)
            int meshId = mesh.GetInstanceID(); // InstanceID 조회는 메인 스레드에서
            await Task.Run(() =>
            {
                try { Physics.BakeMesh(meshId, false); }
                catch (Exception e) { Debug.LogWarning($"[ArtifactModelLoader] 백그라운드 콜라이더 베이크 실패(메인 스레드로 폴백): {e.Message}"); }
            });

            _meshCache[cacheKey] = mesh;
            return mesh;
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
