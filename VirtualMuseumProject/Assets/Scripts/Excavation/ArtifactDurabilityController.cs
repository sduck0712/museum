using System;
using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Excavation
{
    /// <summary>
    /// 유물 인스턴스 하나의 내구도/상태를 관리한다.
    /// StudentArtifactSpawner가 각 유물 오브젝트에 부착한다.
    /// </summary>
    public class ArtifactDurabilityController : MonoBehaviour
    {
        public ArtifactData ArtifactInfo { get; private set; }
        public ArtifactRuntimeState RuntimeState { get; private set; }

        public event Action OnBroken;
        public event Action OnFullyCleared;

        public void Initialize(ArtifactData data)
        {
            ArtifactInfo = data;
            RuntimeState = new ArtifactRuntimeState(data);
        }

        public void ApplyPressure(float pressureEstimate, float toolBreakRisk)
        {
            if (RuntimeState.state == ExcavationState.Broken || RuntimeState.state == ExcavationState.ReassemblyPuzzle)
                return;

            int damage = Mathf.CeilToInt(pressureEstimate * toolBreakRisk * ArtifactInfo.maxDurability);
            if (damage <= 0) return;

            RuntimeState.currentDurability -= damage;

            if (RuntimeState.currentDurability <= 0)
            {
                RuntimeState.currentDurability = 0;
                RuntimeState.state = ExcavationState.Broken;
                OnBroken?.Invoke();
            }
        }

        public void NotifyMaskFullyCleared()
        {
            if (RuntimeState.state == ExcavationState.Broken || RuntimeState.state == ExcavationState.ReassemblyPuzzle)
                return;

            RuntimeState.state = ExcavationState.Revealed;
            OnFullyCleared?.Invoke();
        }

        public void NotifyRepaired()
        {
            RuntimeState.isRestored = true;
            RuntimeState.state = ExcavationState.Revealed;
        }
    }
}
