using UnityEngine;

namespace VirtualMuseum.Puzzle
{
    /// <summary>
    /// 파손된 유물의 조각 하나. CorrectLocalPosition/Rotation은
    /// puzzleRoot(원본 유물 배치 위치) 기준 로컬 좌표계에서의 정답 값이다.
    /// </summary>
    public class ArtifactShard : MonoBehaviour
    {
        [SerializeField] private float snapPositionTolerance = 0.15f;
        [SerializeField] private float snapRotationToleranceDegrees = 15f;

        public Vector3 CorrectLocalPosition { get; private set; }
        public Quaternion CorrectLocalRotation { get; private set; }
        public bool IsSnapped { get; private set; }

        public void Setup(Vector3 correctLocalPosition, Quaternion correctLocalRotation)
        {
            CorrectLocalPosition = correctLocalPosition;
            CorrectLocalRotation = correctLocalRotation;
        }

        public bool TryEvaluateSnap(Transform puzzleRoot)
        {
            if (IsSnapped) return true;

            Vector3 targetWorldPos = puzzleRoot.TransformPoint(CorrectLocalPosition);
            Quaternion targetWorldRot = puzzleRoot.rotation * CorrectLocalRotation;

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
