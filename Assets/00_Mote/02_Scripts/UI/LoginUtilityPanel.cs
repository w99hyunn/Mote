using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class LoginUtilityPanel : MonoBehaviour
    {
        [SerializeField] private GameObject settings;
        [SerializeField] private Slider volume;
        [SerializeField] private Canvas[] underlayCanvases;
        private int transition;
        private void Update()
        {
            if (!SystemUI.PopupCapturesInput && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                CloseTopPopup();
        }

        public bool CloseTopPopup()
        {
            if(settings.activeSelf){CloseSettings();return true;}
            return false;
        }
        public void OpenSettings()
        {
            volume.SetValueWithoutNotify(AudioListener.volume);
            ++transition;
            foreach (var canvas in underlayCanvases) canvas.enabled = false;
            if (settings.activeSelf) settings.GetComponent<UiPanelReveal>().Replay();
            else settings.SetActive(true);
        }

        public void OpenNotices() => SystemUI.ShowNotices();
        public void OpenLanguage() => SystemUI.ShowLanguage();
        public void CloseSettings() => ClosePanel(settings);
        private async void ClosePanel(GameObject panel)
        {
            int current = ++transition;
            if(panel.TryGetComponent<UiPanelReveal>(out var reveal)) await reveal.HideAsync();
            if(panel && current == transition)
            {
                panel.SetActive(false);
                foreach(var canvas in underlayCanvases)canvas.enabled=true;
            }
        }
        public void SetVolume(float value) => AudioListener.volume = value;
    }
}
