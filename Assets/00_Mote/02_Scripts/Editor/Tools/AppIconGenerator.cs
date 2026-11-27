using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Mote.EditorTools
{
    internal static class AppIconGenerator
    {
        internal const string Root = "Assets/00_Mote/04_UI/AppIcon";
        internal const string Source = Root + "/Mote.png";
        internal const string Output = Root + "/Generated";

        internal static string Apply(Texture2D source, bool android, bool ios, int paddingPercent = 0)
        {
            if (!android && !ios) throw new ArgumentException("플랫폼을 선택하세요.");
            if (AssetDatabase.GetAssetPath(source).StartsWith(Output + "/", StringComparison.Ordinal))
                throw new ArgumentException("생성된 파일 대신 원본 PNG를 선택하세요.");
            var art = new IconArtwork(source);
            var outputs = new Dictionary<string, Texture2D>();
            var assignments = new List<Action>();
            int androidCount = 0, iosCount = 0;
            Texture2D Generate(string platform, int size, IconLayer layer)
            {
                string path = $"{Output}/{platform}/{layer}-{size}.png";
                if (outputs.TryGetValue(path, out var cached)) return cached;
                var texture = art.Render(size, layer, paddingPercent: paddingPercent);
                try
                {
                    var bytes = texture.EncodeToPNG();
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
                        File.WriteAllBytes(path, bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None ||
                        importer.textureCompression != TextureImporterCompression.Uncompressed ||
                        importer.alphaIsTransparency != (layer == IconLayer.Foreground))
                    {
                        importer.mipmapEnabled = false;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.alphaIsTransparency = layer == IconLayer.Foreground;
                        importer.SaveAndReimport();
                    }
                    var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (!asset || asset.width != size || asset.height != size)
                        throw new IOException("아이콘 크기를 확인하세요: " + path);
                    outputs.Add(path, asset);
                    return asset;
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }

            int Prepare(NamedBuildTarget target, string folder)
            {
                int count = 0;
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
                {
                    var slots = PlayerSettings.GetPlatformIcons(target, kind);
                    foreach (var slot in slots)
                    {
                        if (slot.width != slot.height) throw new NotSupportedException("정사각형 아이콘만 지원합니다.");
                        if (target == NamedBuildTarget.Android && kind.ToString() == "Adaptive")
                            slot.SetTextures(Generate(folder, slot.width, IconLayer.Background),
                                Generate(folder, slot.width, IconLayer.Foreground));
                        else slot.SetTexture(Generate(folder, slot.width, IconLayer.Full));
                        count++;
                    }
                    assignments.Add(() => PlayerSettings.SetPlatformIcons(target, kind, slots));
                }
                return count;
            }

            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").OfType<PlayerSettings>().Single();
            if (android)
            {
                androidCount = Prepare(NamedBuildTarget.Android, "Android");
                if (androidCount == 0) throw new InvalidOperationException("Android Build Support 설치를 확인하세요.");
                Generate("GooglePlay", 512, IconLayer.Full);
            }
            if (ios)
            {
                iosCount = Prepare(NamedBuildTarget.iOS, "iOS");
                if (iosCount == 0)
                {
                    // Windows editors without iOS Build Support still serialize the project's actual icon slots.
                    var serialized = new SerializedObject(settings);
                    var targets = serialized.FindProperty("m_BuildTargetPlatformIcons");
                    for (int t = 0; targets != null && t < targets.arraySize; t++)
                    {
                        var target = targets.GetArrayElementAtIndex(t);
                        if (target.FindPropertyRelative("m_BuildTarget").stringValue != "iPhone") continue;
                        var slots = target.FindPropertyRelative("m_Icons");
                        for (int i = 0; i < slots.arraySize; i++)
                        {
                            var slot = slots.GetArrayElementAtIndex(i);
                            int size = slot.FindPropertyRelative("m_Width").intValue;
                            if (size <= 0 || size != slot.FindPropertyRelative("m_Height").intValue)
                                throw new InvalidOperationException("iOS 아이콘 슬롯 크기가 잘못되었습니다.");
                            string path = slot.FindPropertyRelative("m_Textures").propertyPath;
                            var asset = Generate("iOS", size, IconLayer.Full);
                            assignments.Add(() =>
                            {
                                var current = new SerializedObject(settings);
                                var textures = current.FindProperty(path);
                                if (textures.arraySize == 0) textures.arraySize = 1;
                                textures.GetArrayElementAtIndex(0).objectReferenceValue = asset;
                                current.ApplyModifiedPropertiesWithoutUndo();
                            });
                            iosCount++;
                        }
                    }
                    if (iosCount == 0) throw new InvalidOperationException("iOS Build Support 또는 iPhone 아이콘 슬롯이 필요합니다.");
                }
            }

            var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Unknown, IconKind.Application);
            var defaults = sizes.Select(s => Generate("Default", s, IconLayer.Full)).ToArray();
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(settings, "Mote 앱 아이콘 적용");
            try
            {
                foreach (var assign in assignments) assign();
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, defaults, IconKind.Application);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
            return $"적용 완료 · Android {androidCount}개 슬롯 · iOS {iosCount}개 슬롯 · PNG {outputs.Count}개";
        }
    }
}
