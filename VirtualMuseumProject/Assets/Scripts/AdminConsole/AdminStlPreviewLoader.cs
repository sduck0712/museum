using System.Threading.Tasks;
using UnityEngine;
using VirtualMuseum.ModelLoading;

namespace VirtualMuseum.AdminConsole
{
    /// <summary>
    /// 관리자 콘솔에서 로컬 STL 파일을 런타임에 불러와 프리뷰 지점에 표시한다.
    /// ArtifactModelLoader를 경유하므로 TriLib 유무와 무관하게 STL 프리뷰가 동작한다.
    /// </summary>
    public class AdminStlPreviewLoader : MonoBehaviour
    {
        [SerializeField] private Transform previewSpawnPoint;
        [SerializeField] private Material defaultPreviewMaterial;
        [SerializeField] private float previewTargetSize = 2f;

        private GameObject _currentPreview;

        public async Task<bool> LoadPreviewAsync(string filePath)
        {
            if (_currentPreview != null)
            {
                Destroy(_currentPreview);
                _currentPreview = null;
            }

            GameObject model = await ArtifactModelLoader.LoadAsync(filePath, defaultPreviewMaterial);
            if (model == null) return false;

            _currentPreview = model;
            Transform parent = previewSpawnPoint != null ? previewSpawnPoint : transform;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            NormalizeSize(model);
            ApplyDefaultMaterial(model);
            return true;
        }

        private void NormalizeSize(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim > 0.0001f)
                root.transform.localScale *= previewTargetSize / maxDim;
        }

        private void ApplyDefaultMaterial(GameObject root)
        {
            if (defaultPreviewMaterial == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = defaultPreviewMaterial;
        }
    }
}
