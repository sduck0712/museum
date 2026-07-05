using System.Collections.Generic;
using System.Threading.Tasks;
using VirtualMuseum.Data;

namespace VirtualMuseum.Backend
{
    /// <summary>
    /// 백엔드 추상화 인터페이스.
    /// 1단계는 LocalJsonBackendService로 구현하고, 추후 FirebaseBackendService /
    /// AwsBackendService로 교체 가능하도록 설계한다.
    /// Admin/Student 스크립트는 반드시 이 인터페이스만 참조해야 한다.
    /// </summary>
    public interface IBackendService
    {
        Task<string> UploadArtifactFileAsync(string localFilePath, string artifactID);
        Task SaveArtifactDataAsync(ArtifactData data);
        Task<List<ArtifactData>> FetchMuseumLayoutAsync();
        Task<bool> ValidateAdminCredentialAsync(string id, string password);
        Task DeleteArtifactAsync(string artifactID);
    }
}
