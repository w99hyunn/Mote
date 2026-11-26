using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoHwa
{
    public static class GameSupport
    {
        private static AsyncOperation systemLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            systemLoad = null;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureLoaded();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void EnsureLoaded()
        {
            if (SceneManager.GetSceneByName("System").isLoaded || systemLoad is { isDone: false }) return;
            systemLoad = SceneManager.LoadSceneAsync("System", LoadSceneMode.Additive);
        }

        internal static async Awaitable WaitUntilLoadedAsync()
        {
            EnsureLoaded();
            if (systemLoad is { isDone: false })
                await Awaitable.FromAsyncOperation(systemLoad);
        }
    }
}
