using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Backend
{
    /// <summary>
    /// 1단계 로컬 목업 백엔드. Application.persistentDataPath 아래에
    /// museum_data.json / admin_accounts.json / StlFiles 폴더를 사용한다.
    /// 클라우드 전환 시 이 클래스 대신 새 IBackendService 구현체를 ServiceLocator에
    /// 등록하기만 하면 나머지 코드는 수정할 필요가 없다.
    /// </summary>
    public class LocalJsonBackendService : IBackendService
    {
        private readonly string _dataFilePath;
        private readonly string _adminFilePath;
        private readonly string _stlFolderPath;

        public LocalJsonBackendService()
        {
            _dataFilePath = Path.Combine(Application.persistentDataPath, "museum_data.json");
            _adminFilePath = Path.Combine(Application.persistentDataPath, "admin_accounts.json");
            _stlFolderPath = Path.Combine(Application.persistentDataPath, "StlFiles");

            if (!Directory.Exists(_stlFolderPath))
                Directory.CreateDirectory(_stlFolderPath);

            EnsureDefaultAdminAccount();
            EnsureSeedDataCopiedFromStreamingAssets();
        }

        public Task<string> UploadArtifactFileAsync(string localFilePath, string artifactID)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(localFilePath))
                    throw new FileNotFoundException($"STL 파일을 찾을 수 없습니다: {localFilePath}");

                string extension = Path.GetExtension(localFilePath);
                string destPath = Path.Combine(_stlFolderPath, $"{artifactID}{extension}");
                File.Copy(localFilePath, destPath, overwrite: true);

                // 클라우드 전환 시 여기서 실제 원격 URL을 반환하면 됨. 지금은 로컬 경로 반환.
                return destPath;
            });
        }

        public async Task SaveArtifactDataAsync(ArtifactData data)
        {
            var wrapper = await LoadWrapperAsync();
            int existingIndex = wrapper.artifacts.FindIndex(a => a.artifactID == data.artifactID);

            if (existingIndex >= 0)
                wrapper.artifacts[existingIndex] = data;
            else
                wrapper.artifacts.Add(data);

            await SaveWrapperAsync(wrapper);
        }

        public async Task<List<ArtifactData>> FetchMuseumLayoutAsync()
        {
            var wrapper = await LoadWrapperAsync();
            return wrapper.artifacts;
        }

        public async Task DeleteArtifactAsync(string artifactID)
        {
            var wrapper = await LoadWrapperAsync();
            wrapper.artifacts.RemoveAll(a => a.artifactID == artifactID);
            await SaveWrapperAsync(wrapper);

            string possibleStl = Directory.GetFiles(_stlFolderPath, $"{artifactID}.*").FirstOrDefault();
            if (possibleStl != null)
                File.Delete(possibleStl);
        }

        public Task<bool> ValidateAdminCredentialAsync(string id, string password)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(_adminFilePath)) return false;

                string json = File.ReadAllText(_adminFilePath);
                var accounts = JsonUtility.FromJson<AdminAccountListWrapper>(json);
                string hashed = HashPassword(password);

                return accounts.accounts.Any(a => a.id == id && a.passwordHash == hashed);
            });
        }

        private Task<ArtifactDataListWrapper> LoadWrapperAsync()
        {
            return Task.Run(() =>
            {
                if (!File.Exists(_dataFilePath))
                    return new ArtifactDataListWrapper();

                string json = File.ReadAllText(_dataFilePath);
                var wrapper = JsonUtility.FromJson<ArtifactDataListWrapper>(json);
                return wrapper ?? new ArtifactDataListWrapper();
            });
        }

        private Task SaveWrapperAsync(ArtifactDataListWrapper wrapper)
        {
            return Task.Run(() =>
            {
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(_dataFilePath, json);
            });
        }

        private void EnsureDefaultAdminAccount()
        {
            if (File.Exists(_adminFilePath)) return;

            // 데모용 기본 계정: admin / admin1234 (실서비스 배포 전 반드시 변경할 것)
            var defaultAccounts = new AdminAccountListWrapper
            {
                accounts = new List<AdminAccount>
                {
                    new AdminAccount { id = "admin", passwordHash = HashPassword("admin1234") }
                }
            };

            File.WriteAllText(_adminFilePath, JsonUtility.ToJson(defaultAccounts, true));
        }

        private void EnsureSeedDataCopiedFromStreamingAssets()
        {
            // 최초 실행 시 StreamingAssets/DummyData/museum_data_seed.json이 있으면
            // persistentDataPath로 복사해 더미 데이터를 자동 채워준다 (데모 편의용).
            if (File.Exists(_dataFilePath)) return;

            string seedPath = Path.Combine(Application.streamingAssetsPath, "DummyData", "museum_data_seed.json");
            if (File.Exists(seedPath))
                File.Copy(seedPath, _dataFilePath);
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        [Serializable]
        private class AdminAccount { public string id; public string passwordHash; }

        [Serializable]
        private class AdminAccountListWrapper { public List<AdminAccount> accounts = new List<AdminAccount>(); }
    }
}
