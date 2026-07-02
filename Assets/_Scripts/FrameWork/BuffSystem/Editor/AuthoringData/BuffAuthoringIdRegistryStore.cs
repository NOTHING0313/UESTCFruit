using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// ID Registry 的 Editor-only 文件存取层。
    /// 只读写 Authoring Registry JSON，不创建 BuffConfigData，不生成 Effect，不修改 runtime。
    /// </summary>
    internal static class BuffAuthoringIdRegistryStore
    {
        internal static bool LoadOrDefault(string path, out BuffAuthoringIdRegistryData data, out string error)
        {
            data = null;
            error = string.Empty;
            string normalized = NormalizePath(path);

            if (!ValidateAssetJsonPath(normalized, out error))
                return false;

            if (!File.Exists(normalized))
            {
                data = CreateDefaultData();
                return true;
            }

            return TryLoadExisting(normalized, out data, out error);
        }

        internal static bool Save(string path, BuffAuthoringIdRegistryData data, out string error)
        {
            error = string.Empty;
            string normalized = NormalizePath(path);

            if (!ValidateAssetJsonPath(normalized, out error))
                return false;

            if (data == null)
            {
                error = "Registry 数据为空，已阻止写入。";
                return false;
            }

            // 已存在的 JSON 必须先能正常解析，避免用新内容覆盖损坏文件。
            if (File.Exists(normalized) && !TryLoadExisting(normalized, out _, out error))
                return false;

            if (!EnsureParentFolder(normalized, out error))
                return false;

            if (File.Exists(normalized) && !BackupExisting(normalized, out error))
                return false;

            try
            {
                NormalizeData(data);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(normalized, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                error = $"Registry JSON 写入失败：{exception.Message}";
                return false;
            }
        }

        internal static bool CreateDefault(string path, out BuffAuthoringIdRegistryData data, out string error)
        {
            data = CreateDefaultData();
            error = string.Empty;
            string normalized = NormalizePath(path);

            if (!ValidateAssetJsonPath(normalized, out error))
                return false;

            if (File.Exists(normalized))
            {
                error = $"Registry JSON 已存在，已阻止覆盖：{normalized}";
                return false;
            }

            if (!EnsureParentFolder(normalized, out error))
                return false;

            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(normalized, json, Encoding.UTF8);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                error = $"Registry JSON 创建失败：{exception.Message}";
                return false;
            }
        }

        internal static BuffAuthoringIdRegistryData CreateDefaultData()
        {
            return new BuffAuthoringIdRegistryData
            {
                version = 1,
                nextBuffConfigId = BuffAuthoringIdRegistryScanner.DefaultNextBuffConfigId,
                nextEffectId = BuffAuthoringIdRegistryScanner.DefaultNextEffectId
            };
        }

        internal static bool EnsureParentFolder(string assetPath, out string error)
        {
            error = string.Empty;
            string normalized = NormalizePath(assetPath);
            int separatorIndex = normalized.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                error = $"Registry 路径缺少父目录：{normalized}";
                return false;
            }

            string folder = normalized.Substring(0, separatorIndex);
            if (AssetDatabase.IsValidFolder(folder))
                return true;

            string[] parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                error = $"Registry 父目录必须位于 Assets 下：{folder}";
                return false;
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }

            return true;
        }

        internal static bool BackupExisting(string path, out string error)
        {
            error = string.Empty;
            string normalized = NormalizePath(path);
            if (!File.Exists(normalized))
                return true;

            try
            {
                string backupPath = normalized + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".bak";
                File.Copy(normalized, backupPath, false);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Registry JSON 备份失败：{exception.Message}";
                return false;
            }
        }

        private static bool TryLoadExisting(string path, out BuffAuthoringIdRegistryData data, out string error)
        {
            data = null;
            error = string.Empty;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                data = JsonUtility.FromJson<BuffAuthoringIdRegistryData>(json);
                if (data == null)
                {
                    error = $"Registry JSON 格式错误，已阻止写入：{path}";
                    return false;
                }

                NormalizeData(data);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Registry JSON 解析失败，已阻止写入：{exception.Message}";
                return false;
            }
        }

        private static void NormalizeData(BuffAuthoringIdRegistryData data)
        {
            if (data.version <= 0)
                data.version = 1;

            if (data.nextBuffConfigId <= 0)
                data.nextBuffConfigId = BuffAuthoringIdRegistryScanner.DefaultNextBuffConfigId;

            if (data.nextEffectId <= 0)
                data.nextEffectId = BuffAuthoringIdRegistryScanner.DefaultNextEffectId;

            if (data.buffs == null)
                data.buffs = new System.Collections.Generic.List<BuffAuthoringIdRegistryBuffEntry>();

            if (data.effects == null)
                data.effects = new System.Collections.Generic.List<BuffAuthoringIdRegistryEffectEntry>();
        }

        private static bool ValidateAssetJsonPath(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Registry 路径为空。";
                return false;
            }

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"Registry 路径必须位于 Assets 下：{path}";
                return false;
            }

            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Registry 路径必须是 .json 文件：{path}";
                return false;
            }

            return true;
        }

        private static string NormalizePath(string path)
        {
            return BuffAuthoringHubSettings.NormalizePath(path);
        }
    }
}
