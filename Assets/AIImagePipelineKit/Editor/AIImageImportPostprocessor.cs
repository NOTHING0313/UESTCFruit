#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AIImagePipelineKit.Editor
{
    /// <summary>
    /// 自动将 AI 生成目录下的 PNG 导入为 Unity UI 可用的 Sprite。
    /// </summary>
    public sealed class AIImageImportPostprocessor : AssetPostprocessor
    {
        internal const string OutputRootPrefsKey = "AIImagePipeline.OutputRootAssetPath";
        internal const string DefaultTargetFolder = "Assets/Arts/AI_Generate";
        private const float SpritePixelsPerUnit = 100f;

        private static string TargetFolder
        {
            get
            {
                string value = EditorPrefs.GetString(OutputRootPrefsKey, DefaultTargetFolder);
                return NormalizeAssetPath(value).TrimEnd('/');
            }
        }

        private void OnPreprocessTexture()
        {
            if (!IsTargetImage(assetPath)) return;

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null) return;

            ApplySpriteImportSettings(importer);
        }

        [MenuItem("Tools/AI Image Pipeline/Reimport Generated Sprites")]
        internal static void ReimportGeneratedSpritesMenu()
        {
            string targetFolder = TargetFolder;
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                Debug.LogWarning($"AI image folder not found: {targetFolder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { targetFolder });
            int changedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsTargetImage(path)) continue;

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                ApplySpriteImportSettings(importer);
                importer.SaveAndReimport();
                changedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"Reimported {changedCount} generated AI image assets as UI Sprites from {targetFolder}.");
        }

        /// <summary>
        /// 判断资源是否属于 AI 生成图片目录。
        /// </summary>
        private static bool IsTargetImage(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;

            string normalizedPath = NormalizeAssetPath(assetPath);
            string targetFolder = TargetFolder;
            bool inTargetFolder = normalizedPath.StartsWith(targetFolder + "/", StringComparison.OrdinalIgnoreCase);
            bool isPng = normalizedPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

            return inTargetFolder && isPng;
        }

        /// <summary>
        /// 应用 UI Sprite 导入设置。
        /// </summary>
        private static void ApplySpriteImportSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpritePixelsPerUnit;

            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.isReadable = false;

            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
#endif
