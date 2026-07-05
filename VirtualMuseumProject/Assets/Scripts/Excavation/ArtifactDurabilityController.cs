using System;
using UnityEngine;
using VirtualMuseum.Data;

namespace VirtualMuseum.Excavation
{
    /// <summary>
    /// 유물 인스턴스 하나의 내구도/상태를 관리한다.
    /// StudentArtifactSpawner가 각 유물 오브젝트에 부착한다.
    ///
    /// 데미지는 "안전 임계 속도를 초과한 드래그"에서만 발생하며(스펙 5.2),
    /// 프레임 단위 소수점 데미지를 버퍼에 누적했다가 정수 단위로 차감한다.
    /// 주의: 데미지 양은 maxDurability와 무관하다 — maxDurability가 클수록
    /// 오래 버티는 것이지, 더 많이 깎이는 것이 아니다.
    /// </summary>
    public class ArtifactDurabilityController : MonoBehaviour
    {
        public ArtifactData ArtifactInfo { get; private set; }
        public ArtifactRuntimeState RuntimeState { get; private set; }

        public event Action OnBroken;
        public event Action OnFullyCleared;

        private float _damageBuffer;

        public void Initialize(ArtifactData data)
        {
            ArtifactInfo = data;
            RuntimeState = new ArtifactRuntimeState(data);
        }

        public void BeginInteracting()
        {
            if (RuntimeState.state == ExcavationState.Hidden)
                RuntimeState.state = ExcavationState.Interacting;
        }

        /// <param name="damageAmount">이번 프레임에 가해진 데미지(소수 허용, 내구도 단위)</param>
        public void ApplyDamage(float damageAmount)
        {
            if (RuntimeState == null || RuntimeState.state != ExcavationState.Interacting)
                return;
            if (damageAmount <= 0f) return;

            _damageBuffer += damageAmount;
            int whole = Mathf.FloorToInt(_damageBuffer);
            if (whole <= 0) return;

            _damageBuffer -= whole;
            RuntimeState.currentDurability -= whole;

            if (RuntimeState.currentDurability <= 0)
            {
                RuntimeState.currentDurability = 0;
                RuntimeState.state = ExcavationState.Broken;
                OnBroken?.Invoke();
            }
        }

        public void NotifyMaskFullyCleared()
        {
            if (RuntimeState == null) return;
            if (RuntimeState.state != ExcavationState.Interacting)
                return;

            RuntimeState.state = ExcavationState.Revealed;
            OnFullyCleared?.Invoke();
        }

        /// <summary>퍼즐 재조립 시작 시 호출 (Broken → ReassemblyPuzzle)</summary>
        public void NotifyPuzzleStarted()
        {
            if (RuntimeState != null && RuntimeState.state == ExcavationState.Broken)
                RuntimeState.state = ExcavationState.ReassemblyPuzzle;
        }

        /// <summary>
        /// 퍼즐 완료 시 호출. isRestored 플래그를 남기고 다시 Interacting으로 돌아가
        /// 남은 흙 제거(발굴)를 이어간다. (스펙 5.3: Repaired → Revealed 진행)
        /// </summary>
        public void NotifyRepaired()
        {
            if (RuntimeState == null) return;
            RuntimeState.isRestored = true;
            RuntimeState.currentDurability = Mathf.Max(1, ArtifactInfo.maxDurability / 2); // 복원 후 절반 내구도로 재개
            RuntimeState.state = ExcavationState.Interacting;
        }
    }
}
