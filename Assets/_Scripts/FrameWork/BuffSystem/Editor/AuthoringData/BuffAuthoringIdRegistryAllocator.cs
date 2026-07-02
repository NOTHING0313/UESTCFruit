using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using BuffSystem.Editor.AuthoringGraphs;

namespace BuffSystem
{
    /// <summary>
    /// ID Registry 的 Editor-only 分配器。
    /// 只预留 ID 并写入 Registry JSON，不创建 BuffConfigData，不生成 Effect，不修改注册表。
    /// </summary>
    internal static class BuffAuthoringIdRegistryAllocator
    {
        private static readonly Regex ClassNameRegex = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
            "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
            "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while"
        };

        internal static int FindNextBuffConfigId(BuffAuthoringIdRegistryScanReport scanReport, BuffAuthoringIdRegistryData registryData)
        {
            int start = Math.Max(
                registryData != null ? registryData.nextBuffConfigId : BuffAuthoringIdRegistryScanner.DefaultNextBuffConfigId,
                BuffAuthoringIdRegistryScanner.DefaultNextBuffConfigId);

            return FindNextAvailable(start, CollectBuffIds(scanReport, registryData));
        }

        internal static int FindNextEffectId(BuffAuthoringIdRegistryScanReport scanReport, BuffAuthoringIdRegistryData registryData)
        {
            int start = Math.Max(
                registryData != null ? registryData.nextEffectId : BuffAuthoringIdRegistryScanner.DefaultNextEffectId,
                BuffAuthoringIdRegistryScanner.DefaultNextEffectId);

            return FindNextAvailable(start, CollectEffectIds(scanReport, registryData));
        }

        internal static bool ReserveBuffId(
            string path,
            BuffAuthoringIdRegistryScanReport scanReport,
            string buffName,
            BuffCandidateGraph graph,
            out BuffAuthoringIdRegistryBuffEntry entry,
            out string error)
        {
            entry = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(buffName))
            {
                error = "待预留 Buff 名称为空。";
                return false;
            }

            if (!BuffAuthoringIdRegistryStore.LoadOrDefault(path, out BuffAuthoringIdRegistryData data, out error))
                return false;

            if (scanReport != null && scanReport.Errors.Count > 0)
            {
                error = "当前扫描报告存在 Error，已阻止预留 Buff ID。";
                return false;
            }

            if (BuffNameExists(scanReport, data, buffName))
            {
                error = $"Buff 名称已存在，已阻止重复预留：{buffName}";
                return false;
            }

            int id = FindNextBuffConfigId(scanReport, data);
            if (id <= 0)
            {
                error = "没有可用 Buff ConfigId。";
                return false;
            }

            string now = DateTime.UtcNow.ToString("o");
            entry = new BuffAuthoringIdRegistryBuffEntry
            {
                configId = id,
                buffName = buffName.Trim(),
                graphGuid = GetGraphGuid(graph),
                assetPath = string.Empty,
                status = BuffAuthoringIdRegistryStatus.Reserved,
                createdAt = now,
                updatedAt = now
            };

            data.buffs.Add(entry);
            data.nextBuffConfigId = FindNextAvailable(id + 1, CollectBuffIds(scanReport, data));
            return BuffAuthoringIdRegistryStore.Save(path, data, out error);
        }

        internal static bool ReserveEffectId(
            string path,
            BuffAuthoringIdRegistryScanReport scanReport,
            string effectName,
            string className,
            BuffCandidateGraph graph,
            out BuffAuthoringIdRegistryEffectEntry entry,
            out string error)
        {
            entry = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(effectName))
            {
                error = "待预留 Effect 名称为空。";
                return false;
            }

            if (!IsValidClassName(className))
            {
                error = $"Effect 类名非法：{className}";
                return false;
            }

            if (!BuffAuthoringIdRegistryStore.LoadOrDefault(path, out BuffAuthoringIdRegistryData data, out error))
                return false;

            if (scanReport != null && scanReport.Errors.Count > 0)
            {
                error = "当前扫描报告存在 Error，已阻止预留 EffectId。";
                return false;
            }

            if (EffectNameOrClassExists(scanReport, data, effectName, className))
            {
                error = $"Effect 名称或类名已存在，已阻止重复预留：{effectName} / {className}";
                return false;
            }

            int id = FindNextEffectId(scanReport, data);
            if (id <= 0)
            {
                error = "没有可用 EffectId。";
                return false;
            }

            string now = DateTime.UtcNow.ToString("o");
            entry = new BuffAuthoringIdRegistryEffectEntry
            {
                effectId = id,
                effectName = effectName.Trim(),
                className = className.Trim(),
                scriptPath = string.Empty,
                graphGuid = GetGraphGuid(graph),
                status = BuffAuthoringIdRegistryStatus.Reserved,
                createdAt = now,
                updatedAt = now
            };

            data.effects.Add(entry);
            data.nextEffectId = FindNextAvailable(id + 1, CollectEffectIds(scanReport, data));
            return BuffAuthoringIdRegistryStore.Save(path, data, out error);
        }

        internal static bool RebuildRegistryFromScan(
            string path,
            BuffAuthoringIdRegistryScanReport scanReport,
            out BuffAuthoringIdRegistryData data,
            out string error)
        {
            data = null;
            error = string.Empty;

            if (scanReport == null)
            {
                error = "请先扫描 ID 占用，再重建 Registry。";
                return false;
            }

            if (scanReport.Errors.Count > 0)
            {
                error = "当前扫描报告存在 Error，已阻止重建 Registry。";
                return false;
            }

            if (!BuffAuthoringIdRegistryStore.LoadOrDefault(path, out _, out error))
                return false;

            data = BuffAuthoringIdRegistryStore.CreateDefaultData();
            HashSet<int> buffIds = new HashSet<int>();
            HashSet<int> effectIds = new HashSet<int>();

            for (int i = 0; i < scanReport.BuffEntries.Count; i++)
            {
                BuffAuthoringIdEntry source = scanReport.BuffEntries[i];
                if (!buffIds.Add(source.Id))
                    continue;

                data.buffs.Add(new BuffAuthoringIdRegistryBuffEntry
                {
                    configId = source.Id,
                    buffName = source.Name,
                    graphGuid = source.Guid,
                    assetPath = source.Path,
                    status = source.IsReserved ? BuffAuthoringIdRegistryStatus.Reserved : BuffAuthoringIdRegistryStatus.Imported,
                    createdAt = string.Empty,
                    updatedAt = DateTime.UtcNow.ToString("o")
                });
            }

            for (int i = 0; i < scanReport.EffectEntries.Count; i++)
            {
                BuffAuthoringIdEntry source = scanReport.EffectEntries[i];
                if (!effectIds.Add(source.Id))
                    continue;

                data.effects.Add(new BuffAuthoringIdRegistryEffectEntry
                {
                    effectId = source.Id,
                    effectName = source.Name,
                    className = source.ClassName,
                    scriptPath = source.Path,
                    graphGuid = source.Guid,
                    status = source.IsReserved ? BuffAuthoringIdRegistryStatus.Reserved : BuffAuthoringIdRegistryStatus.Imported,
                    createdAt = string.Empty,
                    updatedAt = DateTime.UtcNow.ToString("o")
                });
            }

            data.nextBuffConfigId = FindNextBuffConfigId(scanReport, data);
            data.nextEffectId = FindNextEffectId(scanReport, data);
            return BuffAuthoringIdRegistryStore.Save(path, data, out error);
        }

        internal static bool UpsertGeneratedBuffEntry(
            string path,
            int configId,
            string buffName,
            BuffCandidateGraph graph,
            string assetPath,
            out string error)
        {
            error = string.Empty;
            if (configId <= 0)
            {
                error = "ConfigId 无效，无法写入 Registry。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(buffName))
            {
                error = "Buff 名称为空，无法写入 Registry。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "Buff assetPath 为空，无法写入 Registry。";
                return false;
            }

            if (!BuffAuthoringIdRegistryStore.LoadOrDefault(path, out BuffAuthoringIdRegistryData data, out error))
                return false;

            string now = DateTime.UtcNow.ToString("o");
            BuffAuthoringIdRegistryBuffEntry entry = FindBuffEntry(data, configId);
            if (entry == null)
            {
                entry = new BuffAuthoringIdRegistryBuffEntry
                {
                    configId = configId,
                    createdAt = now
                };
                data.buffs.Add(entry);
            }
            else if (!string.IsNullOrWhiteSpace(entry.assetPath)
                && !string.Equals(entry.assetPath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Registry 中 ConfigId={configId} 已绑定其他 assetPath：{entry.assetPath}";
                return false;
            }

            entry.buffName = buffName.Trim();
            entry.graphGuid = GetGraphGuid(graph);
            entry.assetPath = assetPath;
            entry.status = BuffAuthoringIdRegistryStatus.Generated;
            if (string.IsNullOrWhiteSpace(entry.createdAt))
                entry.createdAt = now;
            entry.updatedAt = now;

            data.nextBuffConfigId = FindNextBuffConfigId(null, data);
            return BuffAuthoringIdRegistryStore.Save(path, data, out error);
        }

        internal static bool UpsertGeneratedEffectEntry(
            string path,
            int effectId,
            string effectName,
            string className,
            BuffCandidateGraph graph,
            string scriptPath,
            out string error)
        {
            error = string.Empty;
            if (effectId <= 0)
            {
                error = "EffectId 无效，无法写入 Registry。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(effectName))
            {
                error = "Effect 名称为空，无法写入 Registry。";
                return false;
            }

            if (!IsValidClassName(className))
            {
                error = $"Effect 类名非法，无法写入 Registry：{className}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                error = "Effect scriptPath 为空，无法写入 Registry。";
                return false;
            }

            if (!BuffAuthoringIdRegistryStore.LoadOrDefault(path, out BuffAuthoringIdRegistryData data, out error))
                return false;

            string now = DateTime.UtcNow.ToString("o");
            BuffAuthoringIdRegistryEffectEntry entry = FindEffectEntry(data, effectId);
            if (entry == null)
            {
                entry = new BuffAuthoringIdRegistryEffectEntry
                {
                    effectId = effectId,
                    createdAt = now
                };
                data.effects.Add(entry);
            }
            else if (!string.IsNullOrWhiteSpace(entry.scriptPath)
                && !string.Equals(entry.scriptPath, scriptPath, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Registry 中 EffectId={effectId} 已绑定其他 scriptPath：{entry.scriptPath}";
                return false;
            }

            entry.effectName = effectName.Trim();
            entry.className = className.Trim();
            entry.scriptPath = scriptPath;
            entry.graphGuid = GetGraphGuid(graph);
            entry.status = BuffAuthoringIdRegistryStatus.Generated;
            if (string.IsNullOrWhiteSpace(entry.createdAt))
                entry.createdAt = now;
            entry.updatedAt = now;

            data.nextEffectId = FindNextEffectId(null, data);
            return BuffAuthoringIdRegistryStore.Save(path, data, out error);
        }

        private static int FindNextAvailable(int start, HashSet<int> occupied)
        {
            int id = Math.Max(1, start);
            while (id < BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
            {
                if (!occupied.Contains(id))
                    return id;

                id++;
            }

            return -1;
        }

        private static HashSet<int> CollectBuffIds(BuffAuthoringIdRegistryScanReport scanReport, BuffAuthoringIdRegistryData data)
        {
            HashSet<int> ids = new HashSet<int>();
            if (scanReport != null)
            {
                for (int i = 0; i < scanReport.BuffEntries.Count; i++)
                    ids.Add(scanReport.BuffEntries[i].Id);
            }

            if (data != null && data.buffs != null)
            {
                for (int i = 0; i < data.buffs.Count; i++)
                    ids.Add(data.buffs[i].configId);
            }

            return ids;
        }

        private static HashSet<int> CollectEffectIds(BuffAuthoringIdRegistryScanReport scanReport, BuffAuthoringIdRegistryData data)
        {
            HashSet<int> ids = new HashSet<int>();
            if (scanReport != null)
            {
                for (int i = 0; i < scanReport.EffectEntries.Count; i++)
                    ids.Add(scanReport.EffectEntries[i].Id);
            }

            if (data != null && data.effects != null)
            {
                for (int i = 0; i < data.effects.Count; i++)
                    ids.Add(data.effects[i].effectId);
            }

            return ids;
        }

        private static bool BuffNameExists(BuffAuthoringIdRegistryScanReport scanReport, BuffAuthoringIdRegistryData data, string buffName)
        {
            string target = buffName.Trim();
            if (scanReport != null)
            {
                for (int i = 0; i < scanReport.BuffEntries.Count; i++)
                {
                    if (string.Equals(scanReport.BuffEntries[i].Name, target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (data != null && data.buffs != null)
            {
                for (int i = 0; i < data.buffs.Count; i++)
                {
                    if (string.Equals(data.buffs[i].buffName, target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static bool EffectNameOrClassExists(BuffAuthoringIdRegistryScanReport scanReport, BuffAuthoringIdRegistryData data, string effectName, string className)
        {
            string targetName = effectName.Trim();
            string targetClass = className.Trim();
            if (scanReport != null)
            {
                for (int i = 0; i < scanReport.EffectEntries.Count; i++)
                {
                    BuffAuthoringIdEntry entry = scanReport.EffectEntries[i];
                    if (string.Equals(entry.Name, targetName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.ClassName, targetClass, StringComparison.Ordinal))
                        return true;
                }
            }

            if (data != null && data.effects != null)
            {
                for (int i = 0; i < data.effects.Count; i++)
                {
                    BuffAuthoringIdRegistryEffectEntry entry = data.effects[i];
                    if (string.Equals(entry.effectName, targetName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.className, targetClass, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private static BuffAuthoringIdRegistryBuffEntry FindBuffEntry(BuffAuthoringIdRegistryData data, int configId)
        {
            if (data == null || data.buffs == null)
                return null;

            for (int i = 0; i < data.buffs.Count; i++)
            {
                if (data.buffs[i].configId == configId)
                    return data.buffs[i];
            }

            return null;
        }

        private static BuffAuthoringIdRegistryEffectEntry FindEffectEntry(BuffAuthoringIdRegistryData data, int effectId)
        {
            if (data == null || data.effects == null)
                return null;

            for (int i = 0; i < data.effects.Count; i++)
            {
                if (data.effects[i].effectId == effectId)
                    return data.effects[i];
            }

            return null;
        }

        private static bool IsValidClassName(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return false;

            string trimmed = className.Trim();
            if (!ClassNameRegex.IsMatch(trimmed))
                return false;

            return !CSharpKeywords.Contains(trimmed);
        }

        private static string GetGraphGuid(BuffCandidateGraph graph)
        {
            if (graph == null)
                return string.Empty;

            string path = AssetDatabase.GetAssetPath(graph);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }
    }
}
