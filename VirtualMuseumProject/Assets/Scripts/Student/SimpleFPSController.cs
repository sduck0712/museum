using UnityEngine;
using VirtualMuseum.Input;

namespace VirtualMuseum.Student
{
    /// <summary>
    /// 데모용 1인칭 컨트롤러 (WASD 이동 + 마우스 시야 + 중력).
    /// InputModeManager의 모드가 탐험 상태일 때만 동작한다:
    /// - FPSExploration: 이동 + 마우스 시야 (커서 잠금)
    /// - HandTrackingExploration: 이동만 (포인터가 화면을 자유롭게 움직여야 하므로 시야 회전 없음)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimpleFPSController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [SerializeField] private float gravity = -18f;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
        }

        private void Update()
        {
            var modeManager = InputModeManager.Instance;
            InteractionMode mode = modeManager != null ? modeManager.CurrentMode : InteractionMode.FPSExploration;

            bool canMove = mode == InteractionMode.FPSExploration || mode == InteractionMode.HandTrackingExploration;
            bool canLook = mode == InteractionMode.FPSExploration;

            if (canLook) HandleLook();
            HandleMove(canMove);
        }

        private void HandleLook()
        {
            float mx = UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = UnityEngine.Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up, mx);

            _pitch = Mathf.Clamp(_pitch - my, -85f, 85f);
            if (cameraTransform != null)
                cameraTransform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        private void HandleMove(bool canMove)
        {
            Vector3 input = Vector3.zero;
            if (canMove)
            {
                input = new Vector3(UnityEngine.Input.GetAxisRaw("Horizontal"), 0f,
                                    UnityEngine.Input.GetAxisRaw("Vertical"));
                input = Vector3.ClampMagnitude(input, 1f);
            }

            float speed = UnityEngine.Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
            Vector3 horizontal = transform.TransformDirection(input) * speed;

            if (_controller.isGrounded)
                _verticalVelocity = -1f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = horizontal + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
