using UnityEngine;
using UnityEngine.EventSystems;

namespace DoHwa.UI
{
    public abstract class SceneScreen : MonoBehaviour
    {
        [SerializeField] private EventSystem inputSystem;
        [SerializeField] private AudioListener audioListener;
        internal static SceneScreen Current { get; private set; }

        protected virtual void OnEnable() => Current = this;
        protected virtual void OnDisable()
        {
            if (Current == this) Current = null;
        }

        internal (bool input, bool audio) Suspend()
        {
            var previous = (inputSystem.enabled, audioListener.enabled);
            inputSystem.enabled = false;
            audioListener.enabled = false;
            return previous;
        }

        internal void Restore((bool input, bool audio) previous)
        {
            inputSystem.enabled = previous.input;
            audioListener.enabled = previous.audio;
        }

        internal virtual async Awaitable ReleaseContentAsync() => await Awaitable.MainThreadAsync();

        internal virtual async Awaitable PrepareForRevealAsync() => await Awaitable.NextFrameAsync(destroyCancellationToken);
    }
}
