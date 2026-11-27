using Unity.Services.Authentication;
using UnityEngine;

namespace DoHwa.UI
{
    public sealed class AccountMenu : MonoBehaviour
    {
        internal const string SignedOut = "SignedOut";
        internal const string LastProviderKey = "Mote.Auth.LastProvider";

        public void ConfirmLogout() =>
            SystemUI.Confirm("로그아웃", "로그아웃하시겠습니까?", "로그아웃", Logout);

        private async void Logout()
        {
            if (Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized)
            {
                var auth = AuthenticationService.Instance;
                if (auth.IsSignedIn) auth.SignOut(clearCredentials: auth.Profile != "guest");
            }
            PlayerPrefs.SetString(LastProviderKey, SignedOut);
            PlayerPrefs.Save();
            await SystemUI.LoadSceneAsync("Main");
        }
    }
}
