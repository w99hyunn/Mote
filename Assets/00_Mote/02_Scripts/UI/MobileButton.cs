using UnityEngine;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class MobileButton : Button
    {
        [SerializeField] private Animation pressFeedback;
        private const string Hold = "Hold";
        private const int PressedFrame = 4;
        private bool wasPressed;

        protected override void Awake()
        {
            base.Awake();
            if (!pressFeedback) return;
            var clip = pressFeedback.clip;
            // The prefix holds its last pose while the pointer stays down.
            pressFeedback.AddClip(clip, Hold, 0, PressedFrame);
            pressFeedback[Hold].wrapMode = WrapMode.ClampForever;
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (!pressFeedback || !pressFeedback.isActiveAndEnabled || pressFeedback[Hold] == null) return;
            bool pressed = state == SelectionState.Pressed;
            if (pressed == wasPressed) return;
            wasPressed = pressed;
            if (state == SelectionState.Disabled)
            {
                ResetFeedback();
                return;
            }

            var clip = pressFeedback.clip;
            string name = pressed ? Hold : clip.name;
            pressFeedback.Play(name);
            var playback = pressFeedback[name];
            playback.time = pressed ? 0f : PressedFrame / clip.frameRate;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetFeedback();
        }

        private void ResetFeedback()
        {
            wasPressed = false;
            if (!pressFeedback || !pressFeedback.isPlaying) return;
            pressFeedback.Play(pressFeedback.clip.name);
            pressFeedback.Rewind();
            pressFeedback.Sample();
            pressFeedback.Stop();
        }
    }
}
