using System;
using UnityEngine;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class TitleEntry : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup input;
        [SerializeField] private Image confetti;
        [SerializeField] private GameObject homeNavigation;
        private Material effect;
        private bool entering;

        private void Awake()
        {
            effect = new Material(confetti.material);
            confetti.material = effect;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            homeNavigation.SetActive(true);
        }

        public async void Enter()
        {
            if (entering || SystemUI.PopupCapturesInput) return;
            entering = true;
            input.interactable = input.blocksRaycasts = false;
            animator.enabled = true;
            confetti.gameObject.SetActive(true);
            effect.SetFloat("_Clicked", 1f);
            animator.Play("Commit", 0, 0f);
            try
            {
                do
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                while (animator.GetCurrentAnimatorStateInfo(0).IsName("Commit") &&
                       animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f);
                await SystemUI.LoadSceneAsync("InGame");
            }
            catch (OperationCanceledException) { }
        }

        private void OnDestroy() => Destroy(effect);
    }
}
