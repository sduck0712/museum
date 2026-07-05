using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VirtualMuseum.Auth
{
    public enum LoginScreenState { StudentEntry, AdminLoginTab, Authenticating, Error }

    /// <summary>
    /// 로그인 씬의 UI 상태 머신. 탭 전환(학생 입장 / 운영자 로그인)과
    /// 인증 성공 시 씬 전환을 담당한다.
    ///
    /// 참고: 한글 렌더링 호환성(TMP 기본 폰트는 한글 글리프 미포함) 때문에
    /// 데모 단계에서는 uGUI 레거시 Text/InputField(OS 다이나믹 폰트)를 사용한다.
    /// </summary>
    public class LoginUIManager : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private GameObject studentEntryPanel;
        [SerializeField] private GameObject adminLoginPanel;
        [SerializeField] private Text errorText;

        [Header("Student Entry")]
        [SerializeField] private InputField studentNameInput;
        [SerializeField] private Button studentEnterButton;

        [Header("Admin Login")]
        [SerializeField] private InputField adminIdInput;
        [SerializeField] private InputField adminPasswordInput;
        [SerializeField] private Button adminLoginButton;
        [SerializeField] private Button showAdminTabButton;
        [SerializeField] private Button showStudentTabButton;

        [Header("Scene Names")]
        [SerializeField] private string studentSceneName = "StudentPerspective";
        [SerializeField] private string adminSceneName = "AdminConsole";

        private readonly AuthController _authController = new AuthController();
        private LoginScreenState _state;

        private void Awake()
        {
            studentEnterButton.onClick.AddListener(OnStudentEnterClicked);
            adminLoginButton.onClick.AddListener(OnAdminLoginClicked);
            showAdminTabButton.onClick.AddListener(() => SetState(LoginScreenState.AdminLoginTab));
            showStudentTabButton.onClick.AddListener(() => SetState(LoginScreenState.StudentEntry));

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SetState(LoginScreenState.StudentEntry);
        }

        private void SetState(LoginScreenState newState)
        {
            _state = newState;

            studentEntryPanel.SetActive(newState == LoginScreenState.StudentEntry);

            // Error 상태에서도 운영자 패널을 그대로 띄워 재시도가 가능해야 한다.
            bool adminVisible = newState == LoginScreenState.AdminLoginTab
                                || newState == LoginScreenState.Authenticating
                                || newState == LoginScreenState.Error;
            adminLoginPanel.SetActive(adminVisible);

            if (errorText != null)
                errorText.gameObject.SetActive(newState == LoginScreenState.Error);

            bool busy = newState == LoginScreenState.Authenticating;
            adminLoginButton.interactable = !busy;
            showStudentTabButton.interactable = !busy;
            adminIdInput.interactable = !busy;
            adminPasswordInput.interactable = !busy;
        }

        private void OnStudentEnterClicked()
        {
            _authController.LoginAsStudent(studentNameInput != null ? studentNameInput.text : "Guest");
            SceneManager.LoadScene(studentSceneName);
        }

        private async void OnAdminLoginClicked()
        {
            if (_state == LoginScreenState.Authenticating) return;

            SetState(LoginScreenState.Authenticating);

            try
            {
                bool success = await _authController.TryLoginAsAdminAsync(adminIdInput.text, adminPasswordInput.text);

                if (success)
                {
                    SceneManager.LoadScene(adminSceneName);
                }
                else
                {
                    if (errorText != null) errorText.text = "아이디 또는 비밀번호가 올바르지 않습니다.";
                    SetState(LoginScreenState.Error);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginUIManager] 인증 중 오류: {e}");
                if (errorText != null) errorText.text = "인증 중 오류가 발생했습니다. 다시 시도해 주세요.";
                SetState(LoginScreenState.Error);
            }
        }
    }
}
