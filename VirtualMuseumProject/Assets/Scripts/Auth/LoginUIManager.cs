using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VirtualMuseum.Auth
{
    public enum LoginScreenState { StudentEntry, AdminLoginTab, Authenticating, Error }

    /// <summary>
    /// 로그인 씬의 UI 상태 머신. 탭 전환(학생 입장 / 운영자 로그인)과
    /// 인증 성공 시 씬 전환을 담당한다.
    /// </summary>
    public class LoginUIManager : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private GameObject studentEntryPanel;
        [SerializeField] private GameObject adminLoginPanel;
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("Student Entry")]
        [SerializeField] private TMP_InputField studentNameInput;
        [SerializeField] private Button studentEnterButton;

        [Header("Admin Login")]
        [SerializeField] private TMP_InputField adminIdInput;
        [SerializeField] private TMP_InputField adminPasswordInput;
        [SerializeField] private Button adminLoginButton;
        [SerializeField] private Button showAdminTabButton;
        [SerializeField] private Button showStudentTabButton;

        [Header("Scene Names")]
        [SerializeField] private string studentSceneName = "StudentPerspective";
        [SerializeField] private string adminSceneName = "AdminConsole";

        private readonly AuthController _authController = new AuthController();

        private void Awake()
        {
            studentEnterButton.onClick.AddListener(OnStudentEnterClicked);
            adminLoginButton.onClick.AddListener(OnAdminLoginClicked);
            showAdminTabButton.onClick.AddListener(() => SetState(LoginScreenState.AdminLoginTab));
            showStudentTabButton.onClick.AddListener(() => SetState(LoginScreenState.StudentEntry));

            SetState(LoginScreenState.StudentEntry);
        }

        private void SetState(LoginScreenState newState)
        {
            studentEntryPanel.SetActive(newState == LoginScreenState.StudentEntry);
            adminLoginPanel.SetActive(newState == LoginScreenState.AdminLoginTab || newState == LoginScreenState.Authenticating);
            if (errorText != null) errorText.gameObject.SetActive(newState == LoginScreenState.Error);
        }

        private void OnStudentEnterClicked()
        {
            _authController.LoginAsStudent(studentNameInput != null ? studentNameInput.text : "Guest");
            SceneManager.LoadScene(studentSceneName);
        }

        private async void OnAdminLoginClicked()
        {
            SetState(LoginScreenState.Authenticating);

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
    }
}
