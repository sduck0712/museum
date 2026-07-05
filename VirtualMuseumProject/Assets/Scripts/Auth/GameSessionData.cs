namespace VirtualMuseum.Auth
{
    public enum UserRole { Guest, Student, Admin }

    /// <summary>
    /// 로그인 성공 후 세션 정보를 보관하는 씬 간 유지 싱글톤(순수 C# 클래스).
    /// </summary>
    public class GameSessionData
    {
        private static GameSessionData _instance;
        public static GameSessionData Instance => _instance ??= new GameSessionData();

        public UserRole Role { get; private set; } = UserRole.Guest;
        public string DisplayName { get; private set; } = "";

        public void SetStudentSession(string studentName)
        {
            Role = UserRole.Student;
            DisplayName = studentName;
        }

        public void SetAdminSession(string adminId)
        {
            Role = UserRole.Admin;
            DisplayName = adminId;
        }

        public void ClearSession()
        {
            Role = UserRole.Guest;
            DisplayName = "";
        }
    }
}
