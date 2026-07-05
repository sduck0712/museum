using UnityEngine;

namespace VirtualMuseum.Core
{
    /// <summary>
    /// FindObjectOfType 계열 API가 Unity 2023+에서 이름이 바뀐 것을 흡수하는 헬퍼.
    /// </summary>
    public static class SceneQuery
    {
        public static T Find<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }
}
