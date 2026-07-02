using UnityEditor;

namespace BuffSystem
{
    /// <summary>
    /// Buff Authoring Hub 的 Editor-only 路径设置。
    /// 第一版使用 EditorPrefs 保存本机工具偏好，不创建项目 asset，不进入 runtime。
    /// </summary>
    internal static class BuffAuthoringHubSettings
    {
        internal const string DefaultGraphFolder = "Assets/_Scripts/FrameWork/BuffSystem/AuthoringGraphs";
        internal const string DefaultBuffConfigDataFolder = "Assets/Resources/BuffSystem/Buff";
        internal const string DefaultEffectScriptFolder = "Assets/_Scripts/FrameWork/BuffSystem/Effects/Generated";
        internal const string DefaultIdRegistryJsonPath = "Assets/_Scripts/FrameWork/BuffSystem/AuthoringData/BuffSystemAuthoringIdRegistry.json";

        private const string KeyPrefix = "UESTCFruit.BuffSystem.AuthoringHub.";
        private const string GraphDefaultFolderKey = KeyPrefix + "GraphDefaultFolder";
        private const string BuffConfigDataDefaultFolderKey = KeyPrefix + "BuffConfigDataDefaultFolder";
        private const string EffectScriptDefaultFolderKey = KeyPrefix + "EffectScriptDefaultFolder";
        private const string IdRegistryJsonPathKey = KeyPrefix + "IdRegistryJsonPath";
        private const string AutoAllocateIdsKey = KeyPrefix + "AutoAllocateIds";
        private const string AutoRegisterEffectsToBootstrapKey = KeyPrefix + "AutoRegisterEffectsToBootstrap";

        internal static BuffAuthoringHubSettingsData Load()
        {
            return new BuffAuthoringHubSettingsData
            {
                GraphDefaultFolder = NormalizePath(EditorPrefs.GetString(GraphDefaultFolderKey, DefaultGraphFolder)),
                BuffConfigDataDefaultFolder = NormalizePath(EditorPrefs.GetString(BuffConfigDataDefaultFolderKey, DefaultBuffConfigDataFolder)),
                EffectScriptDefaultFolder = NormalizePath(EditorPrefs.GetString(EffectScriptDefaultFolderKey, DefaultEffectScriptFolder)),
                IdRegistryJsonPath = NormalizePath(EditorPrefs.GetString(IdRegistryJsonPathKey, DefaultIdRegistryJsonPath)),
                AutoAllocateIds = EditorPrefs.GetBool(AutoAllocateIdsKey, true),
                AutoRegisterEffectsToBootstrap = EditorPrefs.GetBool(AutoRegisterEffectsToBootstrapKey, true)
            };
        }

        internal static BuffAuthoringHubSettingsData ResetToDefaults()
        {
            BuffAuthoringHubSettingsData data = CreateDefaultData();
            Save(data);
            return data;
        }

        internal static void Save(BuffAuthoringHubSettingsData data)
        {
            EditorPrefs.SetString(GraphDefaultFolderKey, NormalizePath(data.GraphDefaultFolder));
            EditorPrefs.SetString(BuffConfigDataDefaultFolderKey, NormalizePath(data.BuffConfigDataDefaultFolder));
            EditorPrefs.SetString(EffectScriptDefaultFolderKey, NormalizePath(data.EffectScriptDefaultFolder));
            EditorPrefs.SetString(IdRegistryJsonPathKey, NormalizePath(data.IdRegistryJsonPath));
            EditorPrefs.SetBool(AutoAllocateIdsKey, data.AutoAllocateIds);
            EditorPrefs.SetBool(AutoRegisterEffectsToBootstrapKey, data.AutoRegisterEffectsToBootstrap);
        }

        internal static void EnsureGraphFolderExists(string graphFolder)
        {
            EnsureAssetFolderExists(NormalizePath(graphFolder));
        }

        internal static bool FolderExists(string folder)
        {
            return AssetDatabase.IsValidFolder(NormalizePath(folder));
        }

        internal static bool ParentFolderExistsForFile(string assetPath)
        {
            string normalized = NormalizePath(assetPath);
            int separatorIndex = normalized.LastIndexOf('/');
            if (separatorIndex <= 0)
                return false;

            return AssetDatabase.IsValidFolder(normalized.Substring(0, separatorIndex));
        }

        internal static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/');
        }

        private static BuffAuthoringHubSettingsData CreateDefaultData()
        {
            return new BuffAuthoringHubSettingsData
            {
                GraphDefaultFolder = DefaultGraphFolder,
                BuffConfigDataDefaultFolder = DefaultBuffConfigDataFolder,
                EffectScriptDefaultFolder = DefaultEffectScriptFolder,
                IdRegistryJsonPath = DefaultIdRegistryJsonPath,
                AutoAllocateIds = true,
                AutoRegisterEffectsToBootstrap = true
            };
        }

        private static void EnsureAssetFolderExists(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                return;

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }

    internal sealed class BuffAuthoringHubSettingsData
    {
        public string GraphDefaultFolder;
        public string BuffConfigDataDefaultFolder;
        public string EffectScriptDefaultFolder;
        public string IdRegistryJsonPath;
        public bool AutoAllocateIds;
        public bool AutoRegisterEffectsToBootstrap;
    }
}
