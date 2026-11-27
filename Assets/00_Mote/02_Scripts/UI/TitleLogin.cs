using System;
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
        private bool pending;

        private const string LastProviderKey = "Mote.Auth.LastProvider";

        private async void Start()
        {
            SetChoicesVisible(false);
            try
            {
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
                if (this) ShowEntry(provider);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Login restore failed: {ex.GetType().Name}");
            }
            finally
            {
                if (this && gameObject.activeSelf) SetChoicesVisible(true);
            }
        }

        private void SetChoicesVisible(bool visible)
        {
            googleButton.gameObject.SetActive(visible);
            gamesButton.gameObject.SetActive(visible);
            guestButton.gameObject.SetActive(visible);
        }

        private void ShowEntry(string provider)
        {
            PlayerPrefs.SetString(LastProviderKey, provider);
            PlayerPrefs.Save();
            entry.Show();
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
            pending = true;
            googleButton.interactable = gamesButton.interactable = guestButton.interactable = false;
            try
            {
                await UnityServices.InitializeAsync();
                await authenticate();
                if (!this) return;
                ShowEntry(provider);
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
                if (this && guestButton.gameObject.activeSelf)
                {
                    pending = false;
                    googleButton.interactable = gamesButton.interactable = guestButton.interactable = true;
                }
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
