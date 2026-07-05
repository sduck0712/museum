using System.Threading.Tasks;
using VirtualMuseum.Core;

namespace VirtualMuseum.Auth
{
    /// <summary>
    /// 로그인 화면의 실제 인증 로직. UI(LoginUIManager)와 분리되어 있어
    /// 백엔드 구현체가 바뀌어도 이 클래스는 변경되지 않는다.
    /// </summary>
    public class AuthController
    {
        public void LoginAsStudent(string studentNameOrId)
        {
            // 학생 입장은 별도 비밀번호 없이 세션만 생성 (데모 단계)
            string name = string.IsNullOrEmpty(studentNameOrId) ? "Guest" : studentNameOrId;
            GameSessionData.Instance.SetStudentSession(name);
        }

        public async Task<bool> TryLoginAsAdminAsync(string id, string password)
        {
            bool isValid = await ServiceLocator.CurrentBackend.ValidateAdminCredentialAsync(id, password);

            if (isValid)
                GameSessionData.Instance.SetAdminSession(id);

            return isValid;
        }
    }
}
