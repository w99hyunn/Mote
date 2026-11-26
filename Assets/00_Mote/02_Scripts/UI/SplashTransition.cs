using System;
using UnityEngine;
namespace DoHwa.UI
{
    public sealed class SplashTransition : MonoBehaviour
    {
        private bool loading;
        public async void OnSplashFinished()
        {
            if(loading)return;
            loading=true;
            try {await SystemUI.LoadSceneAsync("Main");}
            catch(OperationCanceledException){}
        }
    }
}

