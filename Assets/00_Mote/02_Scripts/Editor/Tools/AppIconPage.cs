using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mote.EditorTools
{
    internal sealed class AppIconPage : IMoteToolPage
    {
        public string Title => "앱 아이콘";
        public string IconName => "Texture2D Icon";

        public VisualElement CreateView()
        {
            var view = new VisualElement();
            var source = new ObjectField("원본 PNG") { objectType = typeof(Texture2D), allowSceneObjects = false };
            source.value = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconGenerator.Source);
            source.AddToClassList(BaseField<UnityEngine.Object>.alignedFieldUssClassName);
            var settings = ToolSection.Add(view, "생성 설정");
            settings.Add(source);
            string paddingKey = "Mote.IconPadding." + Application.dataPath;
            var padding = new SliderInt("여백 조절 (%)", IconArtwork.MinPadding, IconArtwork.MaxPadding)
            {
                showInputField = true,
                value = Mathf.Clamp(EditorPrefs.GetInt(paddingKey, 0), IconArtwork.MinPadding, IconArtwork.MaxPadding),
                tooltip = "0은 기본 크기입니다. 양수는 여백을 늘리고 음수는 심볼을 확대합니다."
            };
            padding.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            settings.Add(padding);
            var previewSection = ToolSection.Add(view, "홈 화면 미리보기");
            var previews = new VisualElement();
            previews.AddToClassList("previews");
            previewSection.Add(previews);
            var androidImage = Preview(previews, "Android", "adaptive");
            var iosImage = Preview(previews, "iOS", "ios");
            var targets = new VisualElement();
            targets.AddToClassList("platforms");
            settings.Add(targets);
            targets.Add(new Label("대상 플랫폼"));
            var android = new Toggle { text = "Android", value = true };
            var ios = new Toggle { text = "iOS", value = true };
            targets.Add(android);
            targets.Add(ios);
            var actions = new VisualElement();
            actions.AddToClassList("actions");
            view.Add(actions);
            var apply = new Button { text = "생성 및 적용" };
            apply.AddToClassList("primary");
            var files = new Button(() => EditorUtility.FocusProjectWindow()) { text = "생성 폴더 보기" };
            files.clicked += () => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<DefaultAsset>(AppIconGenerator.Output));
            actions.Add(files);
            actions.Add(apply);
            var status = new HelpBox("", HelpBoxMessageType.Info);
            void Report(string message, HelpBoxMessageType type = HelpBoxMessageType.Info)
            {
                status.text = message;
                status.messageType = type;
                status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            status.AddToClassList("status");
            view.Add(status);
            var sizes = new Foldout { text = "생성된 크기", value = false };
            previewSection.Add(sizes);
            void UpdateSizes()
            {
                sizes.Clear();
                foreach (string folder in new[] { "Android", "iOS", "GooglePlay" })
                {
                    string path = AppIconGenerator.Output + "/" + folder;
                    if (!Directory.Exists(path)) continue;
                    var pixels = Directory.GetFiles(path, "*.png").Select(Path.GetFileNameWithoutExtension)
                        .Select(name => name.Split('-').Last()).Select(value => int.TryParse(value, out int px) ? px : 0)
                        .Where(px => px > 0).Distinct().OrderBy(px => px);
                    var row = new Label(folder + "   " + string.Join(" · ", pixels) + " px");
                    row.AddToClassList("output-size");
                    sizes.Add(row);
                }
                sizes.style.display = sizes.contentContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            UpdateSizes();
            bool valid = false;
            void Enabled()
            {
                apply.SetEnabled(valid && (android.value || ios.value));
                files.SetEnabled(AssetDatabase.IsValidFolder(AppIconGenerator.Output));
            }
            void Release(Image image)
            {
                if (image.image) UnityEngine.Object.DestroyImmediate(image.image);
                image.image = null;
            }
            IconArtwork art = null;
            void Refresh(bool reloadSource)
            {
                Release(androidImage);
                Release(iosImage);
                valid = false;
                Report("");
                try
                {
                    if (source.value)
                    {
                        if (reloadSource || art == null) art = new IconArtwork((Texture2D)source.value);
                        androidImage.image = art.Render(160, IconLayer.Full, true, padding.value);
                        iosImage.image = art.Render(160, IconLayer.Full, paddingPercent: padding.value);
                        valid = true;
                    }
                }
                catch (Exception ex) { Report(ex.Message, HelpBoxMessageType.Error); }
                Enabled();
            }
            view.RegisterCallback<DetachFromPanelEvent>(_ => { Release(androidImage); Release(iosImage); });
            var previewUpdate = view.schedule.Execute(() => Refresh(false));
            previewUpdate.Pause();
            padding.RegisterValueChangedCallback(e =>
            {
                EditorPrefs.SetInt(paddingKey, e.newValue);
                previewUpdate.ExecuteLater(16);
            });
            source.RegisterValueChangedCallback(_ =>
            {
                art = null;
                Refresh(true);
            });
            android.RegisterValueChangedCallback(_ => Enabled());
            ios.RegisterValueChangedCallback(_ => Enabled());
            apply.clicked += () =>
            {
                try
                {
                    Report(AppIconGenerator.Apply((Texture2D)source.value, android.value, ios.value, padding.value));
                    UpdateSizes();
                }
                catch (Exception ex) { Report(ex.Message, HelpBoxMessageType.Error); Debug.LogException(ex); }
                Enabled();
            };
            Refresh(true);
            return view;
        }

        private static Image Preview(VisualElement parent, string label, string shape)
        {
            var card = new VisualElement();
            card.AddToClassList("preview-item");
            var mask = new VisualElement();
            mask.AddToClassList("mask");
            mask.AddToClassList(shape);
            var image = new Image();
            mask.Add(image);
            card.Add(mask);
            card.Add(new Label(label));
            parent.Add(card);
            return image;
        }
    }
}
