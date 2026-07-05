using UnityEngine;

namespace VirtualMuseum.Puzzle
{
    /// <summary>
    /// 파손된 유물의 조각 하나. CorrectLocalPosition/Rotation은
    /// puzzleRoot(원본 유물 배치 위치) 기준 로컬 좌표계에서의 정답 값이다.
    /// </summary>
    public class ArtifactShard : MonoBehaviour
    {
        [SerializeField] private float snapPositionTolerance = 0.12f;
        [SerializeField] private float snapRotationToleranceDegrees = 25f;

        public Vector3 CorrectLocalPosition { get; private set; }
        public Quaternion CorrectLocalRotation { get; private set; }
        public bool IsSnapped { get; private set; }

        public void Setup(Vector3 correctLocalPosition, Quaternion correctLocalRotation)
        {
            CorrectLocalPosition = correctLocalPosition;
            CorrectLocalRotation = correctLocalRotation;
            IsSnapped = false;
        }

        public void SetTolerances(float positionTolerance, float rotationToleranceDegrees)
        {
            snapPositionTolerance = positionTolerance;
            snapRotationToleranceDegrees = rotationToleranceDegrees;
        }

        public void GetTargetWorldPose(Transform puzzleRoot, out Vector3 position, out Quaternion rotation)
        {
            position = puzzleRoot.TransformPoint(CorrectLocalPosition);
            rotation = puzzleRoot.rotation * CorrectLocalRotation;
        }

        /// <summary>드래그를 놓았을 때 한 번만 호출된다 (매 프레임 자동 스냅 방지).</summary>
        public bool TryEvaluateSnap(Transform puzzleRoot)
        {
            if (IsSnapped) return true;

            GetTargetWorldPose(puzzleRoot, out Vector3 targetWorldPos, out Quaternion targetWorldRot);

            float posDelta = Vector3.Distance(transform.position, targetWorldPos);
            float rotDelta = Quaternion.Angle(transform.rotation, targetWorldRot);

            if (posDelta <= snapPositionTolerance && rotDelta <= snapRotationToleranceDegrees)
            {
                transform.position = targetWorldPos;
                transform.rotation = targetWorldRot;
                IsSnapped = true;
            }

            return IsSnapped;
        }
    }
}
