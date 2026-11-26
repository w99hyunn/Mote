using UnityEngine;
using UnityEngine.SceneManagement;
namespace DoHwa.UI
{
    public sealed class UiSceneTransition : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private UnityEngine.UI.Image[] progressMarks;
        [SerializeField] private TMPro.TMP_Text tipTitle;
        [SerializeField] private TMPro.TMP_Text tipBody;
        [SerializeField] private string[] tipTitles;
        [SerializeField, TextArea] private string[] tipBodies;
        private int tipIndex;
        [SerializeField] private Animator tipAnimator;
        private bool changingTip;
        public void NextTip()
        {
            if(changingTip)return;
            changingTip=true;
            tipAnimator.Play("ChangeTip",tipAnimator.GetLayerIndex("TipTransition"),0);
        }
        public void ApplyNextTip()
        {
            tipIndex=(tipIndex+1)%tipTitles.Length;
            tipTitle.text=tipTitles[tipIndex];
            tipBody.text=tipBodies[tipIndex];
        }
        public void FinishTipChange()=>changingTip=false;
        private bool finished;
        private bool loading;
        private void SetProgress(float value)
        {
            for(int i=0;i<progressMarks.Length;i++)
                progressMarks[i].fillAmount=Mathf.Clamp01(value*progressMarks.Length-i);
        }
        public void OnFadeFinished()=>finished=true;
        private async Awaitable PlayAsync(string state)
        {
            finished=false;animator.Play(state,0,0);
            while(!finished)await Awaitable.NextFrameAsync(destroyCancellationToken);
        }
        internal async Awaitable LoadAsync(string scene)
        {
            if(loading)return;
            loading=true;
            gameObject.SetActive(true);
            var outgoing = SceneScreen.Current;
            var previous = outgoing ? outgoing.gameObject.scene : SceneManager.GetActiveScene();
            (bool input, bool audio) suspended = default;
            bool didSuspend = false;
            try
            {
                SetProgress(0);
                await PlayAsync("Cover");
                var operation=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Additive);
                operation.allowSceneActivation=false;
                while(operation.progress<.9f)
                {
                    SetProgress(operation.progress/.9f);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                SetProgress(1);
                // Additive activation overlaps both screens for a frame; hand input/audio over first.
                if (outgoing)
                {
                    suspended = outgoing.Suspend();
                    didSuspend = true;
                }
                operation.allowSceneActivation=true;
                await Awaitable.FromAsyncOperation(operation,destroyCancellationToken);
                for(int i=0;i<SceneManager.sceneCount;i++)
                {
                    var loaded=SceneManager.GetSceneAt(i);
                    if(loaded.name==scene && loaded.handle!=previous.handle)
                    { SceneManager.SetActiveScene(loaded); break; }
                }
                if (outgoing) await outgoing.ReleaseContentAsync();
                if(previous.name!="System")
                    await Awaitable.FromAsyncOperation(SceneManager.UnloadSceneAsync(previous),destroyCancellationToken);
                var incoming = SceneScreen.Current;
                if (incoming && incoming.gameObject.scene == SceneManager.GetActiveScene())
                    await incoming.PrepareForRevealAsync();
                await Awaitable.NextFrameAsync(destroyCancellationToken);
                await PlayAsync("Reveal");
            }
            finally {
                if (didSuspend && outgoing) outgoing.Restore(suspended);
                loading=false;gameObject.SetActive(false);
            }
        }
    }
}
