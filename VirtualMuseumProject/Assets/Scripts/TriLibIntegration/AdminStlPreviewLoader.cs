using TriLibCore;
using UnityEngine;

namespace VirtualMuseum.AdminConsole
{
    /// <summary>
    /// 관리자 콘솔에서 로컬 STL 파일을 런타임에 불러와 프리뷰 씬에 표시한다.
    /// TriLib 2 (TriLibCore.AssetLoader) 사용.
    /// 참고: TriLib 2의 정확한 콜백 시그니처는 설치한 버전에 따라 약간 다를 수 있으므로,
    /// 설치 후 TriLibCore.AssetLoader의 실제 API와 대조해 콜백 파라미터를 맞출 것.
    /// </summary>
    public class AdminStlPreviewLoader : MonoBehaviour
    {
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private Material defaultPreviewMaterial;

        private GameObject _currentPreview;

        public void LoadStlFromPath(string filePath)
        {
            if (_currentPreview != null)
            {
                Destroy(_currentPreview);
                _currentPreview = null;
            }

            var options = AssetLoader.CreateDefaultLoaderOptions();

            AssetLoader.LoadModelFromFile(
                filePath,
                OnLoad,
                OnMaterialsLoad,
                OnProgress,
                OnError,
                null,
                options
            );
        }

        private void OnLoad(AssetLoaderContext context)
        {
            _currentPreview = context.RootGameObject;
            if (_currentPreview == null) return;

            _currentPreview.transform.SetParent(previewSpawnPoint, false);
            _currentPreview.transform.localPosition = Vector3.zero;
            _currentPreview.transform.localRotation = Quaternion.identity;

            ApplyDefaultMaterial(_currentPreview);
        }

        private void OnMaterialsLoad(AssetLoaderContext context)
        {
            // STL은 재질 정보가 없으므로 기본 재질을 다시 한 번 강제 적용
            if (_currentPreview != null)
                ApplyDefaultMaterial(_currentPreview);
        }

        private void ApplyDefaultMaterial(GameObject root)
        {
            if (defaultPreviewMaterial == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = defaultPreviewMaterial;
        }

        private void OnProgress(AssetLoaderContext context, float progress)
        {
            // 필요 시 로딩 프로그레스 바 UI에 연결
        }

        private void OnError(IContextualizedError error)
        {
            Debug.LogError($"[AdminStlPreviewLoader] STL 로드 실패: {error.GetInnerException()}");
        }
    }
}
