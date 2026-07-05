using VirtualMuseum.Backend;

namespace VirtualMuseum.Core
{
    /// <summary>
    /// 단순한 서비스 로케이터. 씬 어디서든 ServiceLocator.CurrentBackend로 접근한다.
    /// 클라우드로 전환할 때는 Bootstrap 씬에서 Register()에 새 구현체(FirebaseBackendService 등)를
    /// 등록하기만 하면 다른 코드는 전혀 수정할 필요가 없다.
    /// </summary>
    public static class ServiceLocator
    {
        private static IBackendService _backend;

        public static IBackendService CurrentBackend
        {
            get
            {
                if (_backend == null)
                    _backend = new LocalJsonBackendService(); // 기본값: 로컬 JSON 목업
                return _backend;
            }
        }

        public static void Register(IBackendService backend)
        {
            _backend = backend;
        }
    }
}
