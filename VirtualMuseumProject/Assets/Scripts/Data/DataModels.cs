using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualMuseum.Data
{
    public enum RarityTier { Common, Uncommon, Rare, Legendary }

    public enum ExcavationToolType { AirBlower, SoftBrush, PickTool, ChiselTool }

    public enum ExcavationDifficulty { Easy, Medium, Hard }

    public enum ExcavationState { Hidden, Interacting, Broken, ReassemblyPuzzle, Repaired, Revealed }

    [Serializable]
    public class PlacementData
    {
        public string pedestalID;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
    }

    [Serializable]
    public class ArtifactData
    {
        public string artifactID;
        public string artifactName;
        [TextArea] public string historicalDescription;
        public string videoURL;
        public RarityTier rarityTier;
        public string stlFileURL;        // 1단계(로컬 목업)에서는 로컬 파일 경로
        public int maxDurability = 50;
        public ExcavationToolType recommendedToolType = ExcavationToolType.SoftBrush;
        public ExcavationDifficulty excavationDifficulty = ExcavationDifficulty.Medium;
        public int shardCount = 5;       // 파손 시 생성되는 재조립 조각 개수
        public PlacementData placementData = new PlacementData();
    }

    // JsonUtility는 최상위 배열 직렬화를 지원하지 않으므로 wrapper 사용
    [Serializable]
    public class ArtifactDataListWrapper
    {
        public List<ArtifactData> artifacts = new List<ArtifactData>();
    }

    // 런타임 중에만 존재하는 상태(내구도/복원 여부) - DB에는 영구 저장하지 않음
    public class ArtifactRuntimeState
    {
        public string artifactID;
        public int currentDurability;
        public bool isRestored;
        public ExcavationState state = ExcavationState.Hidden;

        public ArtifactRuntimeState(ArtifactData data)
        {
            artifactID = data.artifactID;
            currentDurability = data.maxDurability;
            isRestored = false;
            state = ExcavationState.Hidden;
        }
    }
}
