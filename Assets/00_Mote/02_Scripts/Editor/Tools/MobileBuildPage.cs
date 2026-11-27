using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mote.EditorTools
{
    internal sealed class MobileBuildPage : IMoteToolPage
    {
        private const string Pref = "Mote.MobileBuild.";
        public string Title => "모바일 빌드";
        public string IconName => "BuildSettings.Editor";

        public VisualElement CreateView()
        {
            var view = new VisualElement();
            var selectedFormat = (MobilePackage)Mathf.Clamp(EditorPrefs.GetInt(Pref + "Format"), 0, 2);
            var target = new ToggleButtonGroup { allowEmptySelection = false };
            target.AddToClassList("format-tabs");
            foreach (string option in new[] { "Android APK", "Google Play AAB", "iOS Xcode" })
                target.Add(new Button { text = option });
            target.SetValueWithoutNotify(new ToggleButtonGroupState(1UL << (int)selectedFormat, 3));
            view.Add(target);
            MobilePackage Format() => selectedFormat;
            var settings = ToolSection.Add(view, "패키지 설정");
            var version = new TextField("앱 버전") { value = PlayerSettings.bundleVersion, isDelayed = true };
            var code = new IntegerField("Android 버전 코드") { value = PlayerSettings.Android.bundleVersionCode, isDelayed = true };
            var number = new TextField("iOS 빌드 번호") { value = PlayerSettings.iOS.buildNumber, isDelayed = true };
            var name = new TextField("출력 이름") { value = EditorPrefs.GetString(Pref + "Name", "Mote"), isDelayed = true };
            var dev = new Toggle("Development Build") { value = EditorPrefs.GetBool(Pref + "Development") };
            settings.Add(version);
            settings.Add(code);
            settings.Add(number);
            settings.Add(name);
            settings.Add(dev);

            var signing = new Foldout { text = "Android 서명", value = false };
            var keyInfo = new Label();
            signing.Add(keyInfo);
            var keyPass = new TextField("키스토어 비밀번호") { isPasswordField = true, isDelayed = true };
            var aliasPass = new TextField("키 비밀번호") { isPasswordField = true, isDelayed = true };
            signing.Add(keyPass);
            signing.Add(aliasPass);
            signing.Add(new Button(() => SettingsService.OpenProjectSettings("Project/Player")) { text = "Player Settings 열기" });
            var options = ToolSection.Add(view, "빌드 구성");
            options.Add(signing);
            var sceneSection = new Foldout { text = "빌드 씬", value = false };
            options.Add(sceneSection);
            var info = new Label();
            info.style.whiteSpace = WhiteSpace.Normal;
            var output = ToolSection.Add(view, "출력");
            output.Add(info);
            var notice = new HelpBox("", HelpBoxMessageType.Info);
            view.Add(notice);
            var actions = new VisualElement();
            actions.AddToClassList("actions");
            output.Add(actions);
            var change = new Button { text = "플랫폼 전환" };
            var build = new Button { text = "빌드" };
            build.AddToClassList("primary");
            var run = new Button { text = "빌드 후 실행" };
            var folder = new Button(() =>
            {
                string path = MobileBuild.OutputFolder(Format());
                Directory.CreateDirectory(path);
                EditorUtility.RevealInFinder(path);
            }) { text = "출력 폴더 보기" };
            foreach (var b in new[] { folder, change, run, build }) actions.Add(b);
            var result = new HelpBox("", HelpBoxMessageType.Info);
            result.style.display = DisplayStyle.None;
            view.Add(result);
            string sceneList = null;
            void Refresh()
            {
                var format = Format();
                bool android = format != MobilePackage.Xcode;
                var scenes = MobileBuild.Scenes();
                string list = string.Join("\n", scenes);
                if (list != sceneList)
                {
                    sceneList = list;
                    sceneSection.Clear();
                    for (int i = 0; i < scenes.Length; i++)
                        sceneSection.Add(new Label($"{i}   {Path.GetFileNameWithoutExtension(scenes[i])}") { tooltip = scenes[i] });
                    sceneSection.text = $"빌드 씬 ({scenes.Length})";
                }
                code.style.display = signing.style.display = android ? DisplayStyle.Flex : DisplayStyle.None;
                number.style.display = android ? DisplayStyle.None : DisplayStyle.Flex;
                string key = PlayerSettings.Android.keystoreName;
                keyInfo.text = PlayerSettings.Android.useCustomKeystore ? $"{Path.GetFileName(key)}  /  {PlayerSettings.Android.keyaliasName}" : "디버그 키스토어";
                keyPass.SetEnabled(PlayerSettings.Android.useCustomKeystore);
                aliasPass.SetEnabled(PlayerSettings.Android.useCustomKeystore);
                keyPass.textEdition.placeholder = string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) ? "입력 필요" : "현재 세션에 설정됨";
                aliasPass.textEdition.placeholder = string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass) ? "입력 필요" : "현재 세션에 설정됨";
                string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(MobileBuild.Group(format)));
                info.text = $"{id}\n{FileUtil.GetProjectRelativePath(MobileBuild.OutputFolder(format).Replace('\\','/'))}/{name.value}-<날짜·시각>{MobileBuild.Extension(format)}";
                string problem = MobileBuild.Problem(format, name.value, scenes);
                notice.style.display = problem == null ? DisplayStyle.None : DisplayStyle.Flex;
                notice.text = problem ?? "";
                change.style.display = EditorUserBuildSettings.activeBuildTarget == MobileBuild.Target(format) ? DisplayStyle.None : DisplayStyle.Flex;
                change.SetEnabled(MobileBuild.Installed(format) && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer);
                build.SetEnabled(problem == null);
                run.style.display = format == MobilePackage.Apk ? DisplayStyle.Flex : DisplayStyle.None;
                run.SetEnabled(problem == null && format == MobilePackage.Apk);
            }
            void Report(string message, HelpBoxMessageType type)
            {
                result.text = message;
                result.messageType = type;
                result.style.display = DisplayStyle.Flex;
            }
            void Execute(bool launch)
            {
                try
                {
                    var report = MobileBuild.Run(Format(), name.value, dev.value, launch);
                    var summary = report.summary;
                    bool ok = summary.result == BuildResult.Succeeded;
                    Report($"{summary.result} · {summary.totalTime.TotalSeconds:F1}초 · 오류 {summary.totalErrors}개\n{summary.outputPath}",
                        ok ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning);
                }
                catch (Exception ex) { Report(ex.Message, HelpBoxMessageType.Error); }
                Refresh();
            }

            target.RegisterValueChangedCallback(e =>
            {
                for (int i = 0; i < 3; i++)
                    if (e.newValue[i]) selectedFormat = (MobilePackage)i;
                EditorPrefs.SetInt(Pref + "Format", (int)selectedFormat);
                result.style.display = DisplayStyle.None;
                Refresh();
            });
            version.RegisterValueChangedCallback(e => { PlayerSettings.bundleVersion = e.newValue; Refresh(); });
            code.RegisterValueChangedCallback(e => { PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, e.newValue); code.SetValueWithoutNotify(PlayerSettings.Android.bundleVersionCode); Refresh(); });
            number.RegisterValueChangedCallback(e => { PlayerSettings.iOS.buildNumber = e.newValue; Refresh(); });
            name.RegisterValueChangedCallback(e => { EditorPrefs.SetString(Pref + "Name", e.newValue); Refresh(); });
            dev.RegisterValueChangedCallback(e => EditorPrefs.SetBool(Pref + "Development", e.newValue));
            keyPass.RegisterValueChangedCallback(e => { PlayerSettings.Android.keystorePass = e.newValue; keyPass.SetValueWithoutNotify(""); Refresh(); });
            aliasPass.RegisterValueChangedCallback(e => { PlayerSettings.Android.keyaliasPass = e.newValue; aliasPass.SetValueWithoutNotify(""); Refresh(); });
            change.clicked += () =>
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(MobileBuild.Group(Format()), MobileBuild.Target(Format())))
                    Report("플랫폼 전환을 시작할 수 없습니다.", HelpBoxMessageType.Warning);
            };
            build.clicked += () => Execute(false);
            run.clicked += () => Execute(true);
            view.RegisterCallback<FocusInEvent>(_ => Refresh());
            view.schedule.Execute(Refresh).Every(1500);
            Refresh();
            return view;
        }
    }
}
