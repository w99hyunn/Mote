using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mote.EditorTools
{
    internal enum MobilePackage { Apk, AppBundle, Xcode }

    internal static class MobileBuild
    {
        internal static BuildTarget Target(MobilePackage format) =>
            format == MobilePackage.Xcode ? BuildTarget.iOS : BuildTarget.Android;

        internal static BuildTargetGroup Group(MobilePackage format) =>
            BuildPipeline.GetBuildTargetGroup(Target(format));

        internal static bool Installed(MobilePackage format) =>
            BuildPipeline.IsBuildTargetSupported(Group(format), Target(format)) &&
            Directory.Exists(BuildPipeline.GetPlaybackEngineDirectory(Target(format), BuildOptions.None));

        internal static string[] Scenes()
        {
            var profile = BuildProfile.GetActiveBuildProfile();
            var list = profile ? profile.GetScenesForBuild() : EditorBuildSettings.scenes;
            return list.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        }

        internal static string OutputFolder(MobilePackage format) =>
            Path.GetFullPath(Path.Combine("Builds", format == MobilePackage.Xcode ? "iOS" : "Android",
                format.ToString()));

        internal static string Extension(MobilePackage format) =>
            format == MobilePackage.Apk ? ".apk" : format == MobilePackage.AppBundle ? ".aab" : "";

        internal static string Problem(MobilePackage format, string name, string[] scenes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                return "편집 모드에서 컴파일 또는 빌드가 끝나면 실행할 수 있습니다.";
            if (!Installed(format)) return $"{Target(format)} Build Support를 설치하세요.";
            if (EditorUserBuildSettings.activeBuildTarget != Target(format)) return "선택한 플랫폼으로 전환하세요.";
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == "." || name == ".." ||
                name.EndsWith(".") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Any(char.IsControl) ||
                System.Text.RegularExpressions.Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\.", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                name.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0)
                return "출력 이름에 유효한 파일 이름을 입력하세요.";
            if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)) return "앱 버전을 입력하세요.";
            if (string.IsNullOrWhiteSpace(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(Group(format)))))
                return "Player Settings에서 앱 식별자를 입력하세요.";
            foreach (string path in scenes)
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return "빌드 씬을 찾을 수 없습니다: " + path;
            foreach (string scene in new[] { "Main", "System" })
                if (!scenes.Any(path => Path.GetFileNameWithoutExtension(path) == scene)) return scene + " 씬을 빌드 목록에 추가하세요.";
            if (format == MobilePackage.Xcode)
                return string.IsNullOrWhiteSpace(PlayerSettings.iOS.buildNumber) ? "iOS 빌드 번호를 입력하세요." : null;

            if (PlayerSettings.Android.bundleVersionCode < 1) return "Android 버전 코드는 1 이상이어야 합니다.";
            if (!PlayerSettings.Android.useCustomKeystore)
                return format == MobilePackage.AppBundle ? "Google Play 배포용 키스토어를 Player Settings에서 설정하세요." : null;
            if (!File.Exists(PlayerSettings.Android.keystoreName) || string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
                return "Player Settings에서 Android 키스토어 경로와 키 별칭을 확인하세요.";
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
                return "Android 서명 비밀번호를 입력하세요.";
            return null;
        }

        internal static BuildReport Run(MobilePackage format, string name, bool development, bool launch)
        {
            var scenes = Scenes();
            string problem = Problem(format, name, scenes);
            if (problem != null) throw new InvalidOperationException(problem);
            if (launch && format != MobilePackage.Apk) throw new InvalidOperationException("기기 실행은 APK에서 지원합니다.");
            var root = OutputFolder(format);
            var stem = name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = Path.Combine(root, stem + Extension(format));
            for (int i = 2; Directory.Exists(path) || File.Exists(path); i++)
                path = Path.Combine(root, $"{stem}-{i}{Extension(format)}");
            Directory.CreateDirectory(root);
            var options = new BuildPlayerOptions
            {
                target = Target(format),
                targetGroup = Group(format),
                locationPathName = path,
                scenes = scenes,
                options = (development ? BuildOptions.Development : BuildOptions.None) |
                          (launch ? BuildOptions.AutoRunPlayer : BuildOptions.None)
            };
            if (format == MobilePackage.Xcode) return BuildPipeline.BuildPlayer(options);

            bool bundle = EditorUserBuildSettings.buildAppBundle;
            bool export = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.buildAppBundle = format == MobilePackage.AppBundle;
                return BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = bundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = export;
            }
        }
    }
}
