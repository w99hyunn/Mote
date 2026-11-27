using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoHwa.UI
{
    public sealed class DisplaySettingsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] values;
        [SerializeField] private Button[] previous, next;
        [SerializeField] private Button apply, revert;
        [SerializeField] private TMP_Text applyLabel, status;
        [SerializeField] private Slider volume;

        private const string DisplayKey = "Settings.MobileDisplay";
        private const string VolumeKey = "Settings.Volume";
        private enum Row { FrameRate, Texture, KeepAwake }

        [Serializable]
        private struct Options
        {
            public int fps, texture;
            public bool keepAwake;
        }

        private static Options current;
        private Options pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Restore()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            current = new Options
            {
                fps = 30, texture = QualitySettings.globalTextureMipmapLimit
            };

            // Older display saves share the fps and texture fields.
            string json = PlayerPrefs.GetString(DisplayKey, PlayerPrefs.GetString("Settings.Display", ""));
            if (!string.IsNullOrEmpty(json))
            {
                try { current = ReadOptions(json, current); }
                catch (ArgumentException) { /* Keep defaults for a damaged save. */ }
            }
            current.fps = current.fps >= 60 ? 60 : 30;
            current.texture = Mathf.Clamp(current.texture, 0, 2);
            ApplyToDevice();
        }

        private static Options ReadOptions(string json, Options defaults)
        {
            object boxed = defaults;
            JsonUtility.FromJsonOverwrite(json, boxed);
            return (Options)boxed;
        }

        private static void ApplyToDevice()
        {
            Application.targetFrameRate = current.fps;
            QualitySettings.globalTextureMipmapLimit = current.texture;
            if (Application.isMobilePlatform)
                Screen.sleepTimeout = current.keepAwake ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        private void Awake()
        {
            for (int i = 0; i < values.Length; i++)
            {
                Row row = (Row)i;
                previous[i].onClick.AddListener(() => Change(row, -1));
                next[i].onClick.AddListener(() => Change(row, 1));
            }
            apply.onClick.AddListener(Apply);
            revert.onClick.AddListener(Revert);
            volume.onValueChanged.AddListener(SaveVolume);
        }

        private void OnEnable()
        {
            pending = current;
            volume.SetValueWithoutNotify(AudioListener.volume);
            applyLabel.text = "적용";
            status.text = "";
            Refresh();
        }

        private void Change(Row row, int direction)
        {
            switch (row)
            {
                case Row.FrameRate: pending.fps = pending.fps == 30 ? 60 : 30; break;
                case Row.Texture: pending.texture = (pending.texture + direction + 3) % 3; break;
                case Row.KeepAwake: pending.keepAwake = !pending.keepAwake; break;
            }
            Refresh();
            status.text = "변경 사항을 적용해주세요.";
        }

        private void Refresh()
        {
            values[(int)Row.FrameRate].text = pending.fps + " FPS";
            values[(int)Row.Texture].text = pending.texture switch { 0 => "높음", 1 => "보통", _ => "낮음" };
            values[(int)Row.KeepAwake].text = pending.keepAwake ? "항상 켜짐" : "기기 설정";
        }


        private void Apply()
        {
            current = pending;
            ApplyToDevice();
            PlayerPrefs.SetString(DisplayKey, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
            status.text = "설정을 저장했습니다.";
        }

        private void Revert()
        {
            pending = current;
            Refresh();
            status.text = "변경을 취소했습니다.";
        }

        private static void SaveVolume(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }
    }
}
