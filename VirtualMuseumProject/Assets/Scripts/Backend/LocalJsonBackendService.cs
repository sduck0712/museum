using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Backend
{
    /// <summary>
    /// 1단계 로컬 목업 백엔드. Application.persistentDataPath 아래에
    /// museum_data.json / admin_accounts.json / StlFiles 폴더를 사용한다.
    ///
    /// 스레딩 규칙: 파일 I/O(느릴 수 있음)만 Task.Run으로 백그라운드에서 수행하고,
    /// JsonUtility 직렬화/역직렬화는 await 이후(Unity SynchronizationContext에 의해
    /// 메인 스레드로 복귀한 시점)에 수행한다. 읽기-수정-쓰기 경합은 세마포어로 직렬화한다.
    ///
    /// 클라우드 전환 시 이 클래스 대신 새 IBackendService 구현체를 ServiceLocator에
    /// 등록하기만 하면 나머지 코드는 수정할 필요가 없다.
    /// </summary>
    public class LocalJsonBackendService : IBackendService
    {
        private readonly string _dataFilePath;
        private readonly string _adminFilePath;
        private readonly string _stlFolderPath;
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);

        public LocalJsonBackendService()
        {
            // Application.persistentDataPath는 메인 스레드에서만 접근 가능하므로 생성자에서 캐싱
            _dataFilePath = Path.Combine(Application.persistentDataPath, "museum_data.json");
            _adminFilePath = Path.Combine(Application.persistentDataPath, "admin_accounts.json");
            _stlFolderPath = Path.Combine(Application.persistentDataPath, "StlFiles");

            if (!Directory.Exists(_stlFolderPath))
                Directory.CreateDirectory(_stlFolderPath);

            EnsureDefaultAdminAccount();
            EnsureSeedDataCopiedFromStreamingAssets();
        }

        public async Task<string> UploadArtifactFileAsync(string localFilePath, string artifactID)
        {
            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
                throw new FileNotFoundException($"STL 파일을 찾을 수 없습니다: {localFilePath}");

            string extension = Path.GetExtension(localFilePath);
            string destPath = Path.Combine(_stlFolderPath, $"{artifactID}{extension}");

            await Task.Run(() => File.Copy(localFilePath, destPath, overwrite: true));

            // 클라우드 전환 시 여기서 실제 원격 URL을 반환하면 됨. 지금은 로컬 경로 반환.
            return destPath;
        }

        public async Task SaveArtifactDataAsync(ArtifactData data)
        {
            await _ioLock.WaitAsync();
            try
            {
                var wrapper = await LoadWrapperAsync();
                int existingIndex = wrapper.artifacts.FindIndex(a => a.artifactID == data.artifactID);

                if (existingIndex >= 0)
                    wrapper.artifacts[existingIndex] = data;
                else
                    wrapper.artifacts.Add(data);

                await SaveWrapperAsync(wrapper);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        public async Task<List<ArtifactData>> FetchMuseumLayoutAsync()
        {
            var wrapper = await LoadWrapperAsync();
            return wrapper.artifacts;
        }

        public async Task DeleteArtifactAsync(string artifactID)
        {
            await _ioLock.WaitAsync();
            try
            {
                var wrapper = await LoadWrapperAsync();
                wrapper.artifacts.RemoveAll(a => a.artifactID == artifactID);
                await SaveWrapperAsync(wrapper);
            }
            finally
            {
                _ioLock.Release();
            }

            string possibleStl = Directory.GetFiles(_stlFolderPath, $"{artifactID}.*").FirstOrDefault();
            if (possibleStl != null)
                await Task.Run(() => File.Delete(possibleStl));
        }

        public async Task<bool> ValidateAdminCredentialAsync(string id, string password)
        {
            if (!File.Exists(_adminFilePath)) return false;

            string json = await Task.Run(() => File.ReadAllText(_adminFilePath));
            string hashed = await Task.Run(() => HashPassword(password));

            var accounts = JsonUtility.FromJson<AdminAccountListWrapper>(json);
            if (accounts?.accounts == null) return false;

            return accounts.accounts.Any(a => a.id == id && a.passwordHash == hashed);
        }

        private async Task<ArtifactDataListWrapper> LoadWrapperAsync()
        {
            if (!File.Exists(_dataFilePath))
                return new ArtifactDataListWrapper();

            string json = await Task.Run(() => File.ReadAllText(_dataFilePath));
            var wrapper = JsonUtility.FromJson<ArtifactDataListWrapper>(json);
            return wrapper ?? new ArtifactDataListWrapper();
        }

        private async Task SaveWrapperAsync(ArtifactDataListWrapper wrapper)
        {
            string json = JsonUtility.ToJson(wrapper, true);
            await Task.Run(() => File.WriteAllText(_dataFilePath, json));
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
            if (!File.Exists(seedPath)) return;

            File.Copy(seedPath, _dataFilePath);
            TryAttachBundledSampleStl();
        }

        /// <summary>
        /// StreamingAssets/DummyData/SampleStl/*.stl 샘플이 번들되어 있으면 StlFiles로 복사하고,
        /// stlFileURL이 비어 있는 시드 유물들에 연결한다. (데모에서 실제 STL 발굴 체험용 —
        /// 여러 유물이 같은 파일을 참조해도 ArtifactModelLoader의 메시 캐시 덕분에 1회만 파싱된다.)
        /// </summary>
        private void TryAttachBundledSampleStl()
        {
            string sampleDir = Path.Combine(Application.streamingAssetsPath, "DummyData", "SampleStl");
            if (!Directory.Exists(sampleDir)) return;

            string sample = Directory.GetFiles(sampleDir, "*.stl").FirstOrDefault();
            if (sample == null) return;

            string destPath = Path.Combine(_stlFolderPath, "sample_" + Path.GetFileName(sample));
            if (!File.Exists(destPath))
                File.Copy(sample, destPath);

            string json = File.ReadAllText(_dataFilePath);
            var wrapper = JsonUtility.FromJson<ArtifactDataListWrapper>(json);
            if (wrapper?.artifacts == null) return;

            foreach (var artifact in wrapper.artifacts)
            {
                if (string.IsNullOrEmpty(artifact.stlFileURL))
                    artifact.stlFileURL = destPath;
            }

            File.WriteAllText(_dataFilePath, JsonUtility.ToJson(wrapper, true));
            Debug.Log($"[LocalJsonBackendService] 번들 샘플 STL 연결 완료: {destPath}");
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password ?? string.Empty));
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
