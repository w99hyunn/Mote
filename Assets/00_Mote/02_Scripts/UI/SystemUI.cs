using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class SystemUI : MonoBehaviour
    {
        [SerializeField] private UiSceneTransition loadingScreen;
        [SerializeField] private UiPanelReveal notice;
        [SerializeField] private TMP_Text noticeTitle;
        [SerializeField] private TMP_Text noticeBody;
        [SerializeField] private Button noticeButton;
        [SerializeField] private UiPanelReveal confirmation;
        [SerializeField] private TMP_Text confirmationTitle;
        [SerializeField] private TMP_Text confirmationBody;
        [SerializeField] private TMP_Text confirmationLabel;
        [SerializeField] private Button cancelButton;

        [SerializeField] private UiPanelReveal language;
        [SerializeField] private Button languageButton;

        private static SystemUI current;
        private static Action<SystemUI> pendingPopup;
        private UiPanelReveal visible;
        private GameObject previousSelection;
        private Action confirmed;
        private int revision;
        private int closedFrame = -1;
        private string defaultTitle;
        private string defaultBody;
        public static bool PopupCapturesInput => current && (current.visible || current.closedFrame == Time.frameCount);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            current = null;
            pendingPopup = null;
        }

        private void Awake()
        {
            current = this;
            defaultTitle = noticeTitle.text;
            defaultBody = noticeBody.text;
            var request = pendingPopup;
            pendingPopup = null;
            request?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        public static async Awaitable LoadSceneAsync(string scene)
        {
            pendingPopup = null;
            await GameSupport.WaitUntilLoadedAsync();
            if (current.visible) current.Dismiss();
            await current.loadingScreen.LoadAsync(scene);
        }

        private static void Present(Action<SystemUI> request)
        {
            if (current)
            {
                request(current);
                return;
            }
            pendingPopup = request;
            GameSupport.EnsureLoaded();
        }

        public static void ShowLanguage() => Present(ui => ui.Open(ui.language, ui.languageButton, null));

        public static void ShowNotices() =>
            Present(ui => ui.OpenNotice(ui.defaultTitle, ui.defaultBody));

        public static void ShowNotice(string title, string message) =>
            Present(ui => ui.OpenNotice(title, message));

        private void OpenNotice(string title, string message)
        {
            noticeTitle.text = title;
            noticeBody.text = message;
            Open(notice, noticeButton, null);
        }

        public static void Confirm(string title, string message, string label, Action action) =>
            Present(ui => ui.OpenConfirmation(title, message, label, action));

        private void OpenConfirmation(string title, string message, string label, Action action)
        {
            confirmationTitle.text = title;
            confirmationBody.text = message;
            confirmationLabel.text = label;
            Open(confirmation, cancelButton, action);
        }

        private void Open(UiPanelReveal panel, Button firstButton, Action action)
        {
            revision++;
            var events = EventSystem.current;
            if (!visible && events) previousSelection = events.currentSelectedGameObject;
            if (visible && visible != panel) visible.gameObject.SetActive(false);
            visible = panel;
            confirmed = action;
            if (panel.gameObject.activeSelf) panel.Replay();
            else panel.gameObject.SetActive(true);
            if (events) events.SetSelectedGameObject(firstButton.gameObject);
        }

        private void Update()
        {
            if (visible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ClosePopup();
        }

        public async void ClosePopup()
        {
            if (!visible || visible.IsLeaving) return;
            int closing = ++revision;
            confirmed = null;
            await visible.HideAsync();
            if (this && closing == revision) Dismiss();
        }

        public void Accept()
        {
            if (!visible || visible.IsLeaving) return;
            var action = confirmed;
            Dismiss();
            action?.Invoke();
        }

        private void Dismiss()
        {
            revision++;
            visible.gameObject.SetActive(false);
            visible = null;
            confirmed = null;
            closedFrame = Time.frameCount;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(previousSelection ? previousSelection : null);
            previousSelection = null;
        }

    }
}
