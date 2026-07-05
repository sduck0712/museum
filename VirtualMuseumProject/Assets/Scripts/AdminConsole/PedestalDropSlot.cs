using UnityEngine;
using VirtualMuseum.Core;
using VirtualMuseum.Data;

namespace VirtualMuseum.AdminConsole
{
    /// <summary>
    /// 좌대 하나를 나타내며, 유물이 드롭되면 PlacementData(위치/회전)를 갱신해
    /// IBackendService를 통해 저장한다.
    /// </summary>
    public class PedestalDropSlot : MonoBehaviour
    {
        [SerializeField] private string pedestalID;

        public async void AssignArtifact(string artifactID, Quaternion rotation)
        {
            var artifacts = await ServiceLocator.CurrentBackend.FetchMuseumLayoutAsync();
            var artifact = artifacts.Find(a => a.artifactID == artifactID);
            if (artifact == null)
            {
                Debug.LogWarning($"[PedestalDropSlot] artifactID를 찾을 수 없음: {artifactID}");
                return;
            }

            artifact.placementData = new PlacementData
            {
                pedestalID = pedestalID,
                position = transform.position,
                rotation = rotation.eulerAngles,
                scale = Vector3.one
            };

            await ServiceLocator.CurrentBackend.SaveArtifactDataAsync(artifact);
            Debug.Log($"[PedestalDropSlot] {artifactID} → {pedestalID}에 배치 저장 완료");
        }
    }
}
