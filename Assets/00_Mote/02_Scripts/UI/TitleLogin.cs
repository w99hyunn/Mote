using System;
using System.Collections;
using TMPro;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.Scripting;
#endif

namespace DoHwa.UI
{
    public sealed class TitleLogin : MonoBehaviour
    {
        [SerializeField] private TitleEntry entry;
        [SerializeField] private Button googleButton;
        [SerializeField] private Button gamesButton;
        [SerializeField] private string googleProvider = "oidc-google";
        [SerializeField] private Button guestButton;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private CanvasGroup menuGroup;
        private Coroutine dotAnimation;
        private bool pending;

        private const string LastProviderKey = "Mote.Auth.LastProvider";

        private void Awake() => menuGroup.alpha = 0f;

        private async void Start()
        {
            try
            {
                await SetProgressAsync(true);
                if (!this) return;
                await UnityServices.InitializeAsync();
                if (!this) return;
                var auth = AuthenticationService.Instance;
                var provider = PlayerPrefs.GetString(LastProviderKey, "");
                bool legacy = string.IsNullOrEmpty(provider);
                if (legacy)
                {
                    // Older installations only recorded the SDK session, per profile.
                    SelectProfile("default");
                    bool external = auth.SessionTokenExists;
                    SelectProfile("guest");
                    bool guest = auth.SessionTokenExists;
                    if (guest == external) return;
                    provider = guest ? "Guest" : "External";
                }

                string profile = provider == "Guest" ? "guest" : "default";
                if (auth.Profile != profile) SelectProfile(profile);
                if (!auth.IsAuthorized)
                {
                    if (!auth.SessionTokenExists) return;
                    await auth.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
                }
                if (provider == "External")
                {
                    var info = await auth.GetPlayerInfoAsync();
                    bool hasGoogle = info.Identities.Exists(i => i.TypeId == googleProvider);
                    bool hasGames = info.Identities.Exists(i => i.TypeId == "google-play-games");
                    if (hasGoogle == hasGames) return;
                    provider = hasGoogle ? "Google" : "GPGS";
                }
                if (this) await ShowEntryAsync(provider);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Login restore failed: {ex.GetType().Name}");
            }
            finally
            {
                if (this && gameObject.activeSelf) await SetProgressAsync(false);
            }
        }

        private async Task SetProgressAsync(bool loading)
        {
            pending = true;
            AllowMenuInput(false);
            await FadeTo(0f, 0.12f);
            if (!this) return;
            googleButton.gameObject.SetActive(!loading);
            gamesButton.gameObject.SetActive(!loading);
            guestButton.gameObject.SetActive(!loading);
            if (dotAnimation != null)
            {
                StopCoroutine(dotAnimation);
                dotAnimation = null;
            }
            progressLabel.gameObject.SetActive(loading);
            if (loading)
            {
                const string message = "로그인 중";
                progressLabel.text = message + "...";
                dotAnimation = StartCoroutine(AnimateDots(message.Length));
            }
            await FadeTo(1f, 0.20f);
            if (!this) return;
            pending = loading;
            AllowMenuInput(!loading);
        }

        private async Task FadeTo(float target, float duration)
        {
            float from = menuGroup.alpha;
            if (Mathf.Approximately(from, target)) return;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                await Awaitable.NextFrameAsync();
                if (!this) return;
                elapsed += Time.unscaledDeltaTime;
                menuGroup.alpha = Mathf.SmoothStep(from, target, elapsed / duration);
            }
        }

        private void AllowMenuInput(bool allowed)
        {
            menuGroup.interactable = allowed;
            menuGroup.blocksRaycasts = allowed;
        }

        private IEnumerator AnimateDots(int labelLength)
        {
            var delay = new WaitForSecondsRealtime(0.32f);
            int dots = 1;
            while (true)
            {
                progressLabel.maxVisibleCharacters = labelLength + dots;
                yield return delay;
                dots = dots % 3 + 1;
            }
        }

        private async Task ShowEntryAsync(string provider)
        {
            PlayerPrefs.SetString(LastProviderKey, provider);
            PlayerPrefs.Save();
            await FadeTo(0f, 0.14f);
            if (!this) return;
            progressLabel.gameObject.SetActive(false);
            entry.Show();
            await FadeTo(1f, 0.24f);
            if (!this) return;
            AllowMenuInput(true);
            gameObject.SetActive(false);
        }

        public void GuestLogin() => SignIn("Guest", SignInGuest);

        public void GoogleLogin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            SignIn("Google", async () =>
            {
                string token = await GetGoogleToken();
                SelectProfile("default");
                await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(googleProvider, token);
            });
#else
            ShowAndroidNotice();
#endif
        }

        public void PlayGamesLogin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            SignIn("GPGS", async () =>
            {
                string code = await GetGamesCode();
                SelectProfile("default");
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code);
            });
#else
            ShowAndroidNotice();
#endif
        }

        private static void ShowAndroidNotice() =>
            SystemUI.ShowNotice("로그인", "Android 기기에서 로그인할 수 있어요.");

        private static async Task SignInGuest()
        {
            var auth = AuthenticationService.Instance;
            if (auth.Profile == "guest" && auth.IsAuthorized) return;
            SelectProfile("guest");
            await auth.SignInAnonymouslyAsync();
        }

        private static void SelectProfile(string profile)
        {
            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn) auth.SignOut();
            if (auth.Profile != profile) auth.SwitchProfile(profile);
        }

        private async void SignIn(string provider, Func<Task> authenticate)
        {
            if (pending || SystemUI.PopupCapturesInput) return;
            try
            {
                await SetProgressAsync(true);
                if (!this) return;
                await UnityServices.InitializeAsync();
                await authenticate();
                if (!this) return;
                await ShowEntryAsync(provider);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!this) return;
                Debug.LogWarning($"Login failed ({provider}): {ex.GetType().Name}");
                SystemUI.ShowNotice("로그인 실패", "로그인을 완료하지 못했어요. 다시 시도해 주세요.");
            }
            finally
            {
                if (this && gameObject.activeSelf) await SetProgressAsync(false);
            }
        }

#if UNITY_ANDROID
        private static async Task<string> GetGoogleToken()
        {
            var callback = new TokenCallback();
            using var bridge = new AndroidJavaClass("com.openframe.mote.auth.MoteGoogleLogin");
            bridge.CallStatic("signIn", UnityEngine.Android.AndroidApplication.currentActivity,
                PlayGamesSettings.LoadInstance().WebClientId, callback);
            try { return await callback.Result.Task; }
            finally { GC.KeepAlive(callback); }
        }

        [Preserve]
        private sealed class TokenCallback : AndroidJavaProxy
        {
            internal readonly TaskCompletionSource<string> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TokenCallback() : base("com.openframe.mote.auth.MoteGoogleLogin$Callback") { }
            [Preserve] public void success(string token) => Result.TrySetResult(token);
            [Preserve] public void failure(string reason)
            {
                if (reason == "Canceled") Result.TrySetCanceled();
                else Result.TrySetException(new InvalidOperationException(reason));
            }
        }

        private static async Task<string> GetGamesCode()
        {
            var games = PlayGamesPlatform.Instance;
            if (!games.IsAuthenticated())
            {
                var login = new TaskCompletionSource<SignInStatus>();
                games.ManuallyAuthenticate(status => login.TrySetResult(status));
                var status = await login.Task;
                if (status == SignInStatus.Canceled) throw new OperationCanceledException();
                if (status != SignInStatus.Success) throw new InvalidOperationException(status.ToString());
            }
            var access = new TaskCompletionSource<string>();
            games.RequestServerSideAccess(false, code => access.TrySetResult(code));
            string result = await access.Task;
            if (string.IsNullOrEmpty(result)) throw new InvalidOperationException("Missing authorization code.");
            return result;
        }
#endif
    }
}
