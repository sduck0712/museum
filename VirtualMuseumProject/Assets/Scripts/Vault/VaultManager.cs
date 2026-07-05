using System.Collections.Generic;
using System.Linq;
using VirtualMuseum.Data;

namespace VirtualMuseum.Vault
{
    public struct VaultEntry
    {
        public ArtifactData Artifact;
        public bool IsRestored; // true면 UI에 "복원됨" 페널티 배지 표시
    }

    /// <summary>
    /// "내 유물함" - 발굴 완료된 유물을 세션 동안 보관한다 (순수 C# 클래스, 씬 전환에 영향받지 않음).
    /// </summary>
    public class VaultManager
    {
        private static VaultManager _instance;
        public static VaultManager Instance => _instance ??= new VaultManager();

        private readonly List<VaultEntry> _collected = new();
        public IReadOnlyList<VaultEntry> CollectedArtifacts => _collected;

        /// <returns>새로 등록되면 true, 이미 있으면 false (중복 등록 방지)</returns>
        public bool AddArtifact(ArtifactData artifact, bool isRestored)
        {
            if (artifact == null) return false;
            if (_collected.Any(e => e.Artifact.artifactID == artifact.artifactID))
                return false;

            _collected.Add(new VaultEntry { Artifact = artifact, IsRestored = isRestored });
            return true;
        }

        public bool Contains(string artifactID) =>
            _collected.Any(e => e.Artifact.artifactID == artifactID);
    }
}
