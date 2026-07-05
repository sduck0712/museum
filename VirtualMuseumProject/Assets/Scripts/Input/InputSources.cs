using UnityEngine;
using VirtualMuseum.HandTracking;

namespace VirtualMuseum.Input
{
    /// <summary>
    /// 마우스와 핸드트래킹을 동일한 인터페이스로 다루기 위한 추상화.
    /// ExcavationBrush, ShardDragController 등은 이 인터페이스만 참조하므로
    /// 입력 소스가 바뀌어도(F키 토글) 로직 변경이 필요 없다.
    /// </summary>
    public interface IPointerInputSource
    {
        Vector2 GetPointerViewportPosition(); // (0~1, 0~1) 정규화된 뷰포트 좌표
        bool IsPrimaryActionHeld();           // 마우스 좌클릭 또는 핀치 상태
    }

    public class MouseInputSource : IPointerInputSource
    {
        public Vector2 GetPointerViewportPosition()
        {
            Vector3 screenPos = UnityEngine.Input.mousePosition;
            return new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);
        }

        public bool IsPrimaryActionHeld() => UnityEngine.Input.GetMouseButton(0);
    }

    public class HandTrackingInputSource : IPointerInputSource
    {
        private readonly HandTrackingManager _handTrackingManager;

        public HandTrackingInputSource(HandTrackingManager handTrackingManager)
        {
            _handTrackingManager = handTrackingManager;
        }

        public Vector2 GetPointerViewportPosition() =>
            _handTrackingManager != null ? _handTrackingManager.IndexFingerViewportPosition : Vector2.one * 0.5f;

        public bool IsPrimaryActionHeld() =>
            _handTrackingManager != null && _handTrackingManager.IsPinching;
    }
}
