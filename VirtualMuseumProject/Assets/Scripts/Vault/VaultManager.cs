using System.Collections.Generic;
using VirtualMuseum.Data;

namespace VirtualMuseum.Vault
{
    public struct VaultEntry
    {
        public ArtifactData Artifact;
        public bool IsRestored;
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

        public void AddArtifact(ArtifactData artifact, bool isRestored)
        {
            _collected.Add(new VaultEntry { Artifact = artifact, IsRestored = isRestored });
        }
    }
}
