using UnityEngine;

namespace VirtualMuseum.Core
{
    /// <summary>
    /// 월드 스페이스 라벨(TextMesh 등)이 항상 카메라를 향하도록 회전시킨다.
    /// </summary>
    public class BillboardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 forward = transform.position - cam.transform.position;
            if (forward.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
