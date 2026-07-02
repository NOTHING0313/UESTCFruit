using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// Buff / Effect ID 占用只读扫描器。
    /// 只读取现有 asset、脚本和可选 Registry JSON，不创建文件，不写入 JSON，不修改 runtime。
    /// </summary>
    internal static class BuffAuthoringIdRegistryScanner
    {
        internal const int DefaultNextBuffConfigId = 100001;
        internal const int DefaultNextEffectId = 200001;
        internal const int ReservedDebugIdStart = 990000;

        private const string DefaultEffectFolder = "Assets/_Scripts/FrameWork/BuffSystem/Effects";
        private const string RegistryBootstrapPath = "Assets/_Scripts/FrameWork/BuffSystem/BuffEffectRegistryBootstrap.cs";
        private const string SourceBuffConfigDataAsset = "BuffConfigDataAsset";
        private const string SourceEffectScript = "EffectScript";
        private const string SourceRegistryBootstrap = "RegistryBootstrap";
        private const string SourceRegistryJson = "RegistryJson";

        private static readonly Regex ConstIntRegex = new Regex(
            @"(?:const|readonly)\s+int\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<id>\d+)",
            RegexOptions.Compiled);

        private static readonly Regex EffectIdAssignRegex = new Regex(
            @"EffectId\s*=\s*(?<id>\d+)",
            RegexOptions.Compiled);

        private static readonly Regex ClassRegex = new Regex(
            @"\bclass\s+(?<class>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        private static readonly Regex RegisterRegex = new Regex(
            @"Register\s*\(\s*(?<id>[A-Za-z_][A-Za-z0-9_]*|\d+)\s*,\s*new\s+(?<class>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        internal static BuffAuthoringIdRegistryScanReport Scan(BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringHubSettingsData safeSettings = settings ?? BuffAuthoringHubSettings.Load();
            BuffAuthoringIdRegistryScanReport report = new BuffAuthoringIdRegistryScanReport
            {
                RegistryPath = NormalizePath(safeSettings.IdRegistryJsonPath)
            };

            ScanBuffConfigData(report, safeSettings);
            ScanEffectScripts(report, DefaultEffectFolder);
            ScanEffectScripts(report, safeSettings.EffectScriptDefaultFolder);
            ScanRegistryBootstrap(report);
            BuffAuthoringIdRegistryData registryData = ScanRegistryJson(report);
            Analyze(report, registryData);
            return report;
        }

        private static void ScanBuffConfigData(BuffAuthoringIdRegistryScanReport report, BuffAuthoringHubSettingsData settings)
        {
            List<string> folders = new List<string>();
            AddUniqueFolder(folders, BuffAuthoringHubSettings.DefaultBuffConfigDataFolder, report);
            AddUniqueFolder(folders, settings.BuffConfigDataDefaultFolder, report);

            for (int i = 0; i < folders.Count; i++)
            {
                string[] guids = AssetDatabase.FindAssets("t:BuffConfigData", new[] { folders[i] });
                for (int j = 0; j < guids.Length; j++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                    BuffConfigData config = AssetDatabase.LoadAssetAtPath<BuffConfigData>(path);
                    if (config == null)
                        continue;

                    report.BuffEntries.Add(new BuffAuthoringIdEntry
                    {
                        Id = config.ID,
                        Name = string.IsNullOrWhiteSpace(config.Name) ? Path.GetFileNameWithoutExtension(path) : config.Name,
                        SourceKind = SourceBuffConfigDataAsset,
                        Path = path,
                        Guid = guids[j],
                        IsReserved = IsReserved(config.ID)
                    });
                }
            }
        }

        private static void ScanEffectScripts(BuffAuthoringIdRegistryScanReport report, string folder)
        {
            string normalized = NormalizePath(folder);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (!Directory.Exists(normalized))
            {
                report.Warnings.Add($"Effect 扫描路径不存在：{normalized}");
                return;
            }

            string[] files = Directory.GetFiles(normalized, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                ScanEffectScriptFile(report, NormalizePath(files[i]));
        }

        private static void ScanEffectScriptFile(BuffAuthoringIdRegistryScanReport report, string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                report.Warnings.Add($"无法读取 Effect 脚本：{path}，原因：{exception.Message}");
                return;
            }

            string className = FindFirstClassName(text);
            HashSet<int> ids = new HashSet<int>();

            MatchCollection constMatches = ConstIntRegex.Matches(text);
            for (int i = 0; i < constMatches.Count; i++)
            {
                if (TryParseId(constMatches[i].Groups["id"].Value, out int id))
                    ids.Add(id);
            }

            MatchCollection assignMatches = EffectIdAssignRegex.Matches(text);
            for (int i = 0; i < assignMatches.Count; i++)
            {
                if (TryParseId(assignMatches[i].Groups["id"].Value, out int id))
                    ids.Add(id);
            }

            if (ids.Count == 0)
                return;

            foreach (int id in ids)
            {
                report.EffectEntries.Add(new BuffAuthoringIdEntry
                {
                    Id = id,
                    Name = className,
                    ClassName = className,
                    SourceKind = SourceEffectScript,
                    Path = path,
                    IsReserved = IsReserved(id)
                });
            }
        }

        private static void ScanRegistryBootstrap(BuffAuthoringIdRegistryScanReport report)
        {
            if (!File.Exists(RegistryBootstrapPath))
            {
                report.Warnings.Add($"找不到 RegistryBootstrap：{RegistryBootstrapPath}");
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(RegistryBootstrapPath, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                report.Warnings.Add($"无法读取 RegistryBootstrap：{exception.Message}");
                return;
            }

            Dictionary<string, int> constIds = new Dictionary<string, int>();
            MatchCollection constMatches = ConstIntRegex.Matches(text);
            for (int i = 0; i < constMatches.Count; i++)
            {
                string name = constMatches[i].Groups["name"].Value;
                if (TryParseId(constMatches[i].Groups["id"].Value, out int id) && !constIds.ContainsKey(name))
                    constIds.Add(name, id);
            }

            MatchCollection registerMatches = RegisterRegex.Matches(text);
            for (int i = 0; i < registerMatches.Count; i++)
            {
                string rawId = registerMatches[i].Groups["id"].Value;
                string className = registerMatches[i].Groups["class"].Value;
                int id;

                if (!TryParseId(rawId, out id) && !constIds.TryGetValue(rawId, out id))
                {
                    report.Warnings.Add($"无法解析 RegistryBootstrap 注册 ID：{rawId}");
                    continue;
                }

                report.EffectEntries.Add(new BuffAuthoringIdEntry
                {
                    Id = id,
                    Name = className,
                    ClassName = className,
                    SourceKind = SourceRegistryBootstrap,
                    Path = RegistryBootstrapPath,
                    IsReserved = IsReserved(id)
                });
            }
        }

        private static BuffAuthoringIdRegistryData ScanRegistryJson(BuffAuthoringIdRegistryScanReport report)
        {
            string path = NormalizePath(report.RegistryPath);
            report.RegistryExists = File.Exists(path);

            if (!report.RegistryExists)
            {
                report.RegistryParseSucceeded = false;
                report.Warnings.Add($"Registry JSON 不存在：{path}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                BuffAuthoringIdRegistryData data = JsonUtility.FromJson<BuffAuthoringIdRegistryData>(json);
                if (data == null)
                {
                    report.Errors.Add($"Registry JSON 格式错误：{path}");
                    return null;
                }

                report.RegistryParseSucceeded = true;
                AddRegistryJsonEntries(report, data);
                return data;
            }
            catch (Exception exception)
            {
                report.RegistryParseSucceeded = false;
                report.Errors.Add($"Registry JSON 解析失败：{exception.Message}");
                return null;
            }
        }

        private static void AddRegistryJsonEntries(BuffAuthoringIdRegistryScanReport report, BuffAuthoringIdRegistryData data)
        {
            if (data.buffs != null)
            {
                for (int i = 0; i < data.buffs.Count; i++)
                {
                    BuffAuthoringIdRegistryBuffEntry entry = data.buffs[i];
                    if (string.IsNullOrWhiteSpace(entry.assetPath))
                        report.Warnings.Add($"Registry Buff 条目没有 assetPath：ConfigId={entry.configId}，Name={entry.buffName}");

                    if (string.IsNullOrWhiteSpace(entry.graphGuid))
                        report.Warnings.Add($"Registry Buff 条目 Graph GUID 为空：ConfigId={entry.configId}，Name={entry.buffName}");

                    report.BuffEntries.Add(new BuffAuthoringIdEntry
                    {
                        Id = entry.configId,
                        Name = entry.buffName,
                        SourceKind = SourceRegistryJson,
                        Path = entry.assetPath,
                        Guid = entry.graphGuid,
                        Status = entry.status,
                        IsReserved = IsReserved(entry.configId)
                    });
                }
            }

            if (data.effects != null)
            {
                for (int i = 0; i < data.effects.Count; i++)
                {
                    BuffAuthoringIdRegistryEffectEntry entry = data.effects[i];
                    if (string.IsNullOrWhiteSpace(entry.scriptPath))
                        report.Warnings.Add($"Registry Effect 条目没有 scriptPath：EffectId={entry.effectId}，Name={entry.effectName}");

                    if (string.IsNullOrWhiteSpace(entry.graphGuid))
                        report.Warnings.Add($"Registry Effect 条目 Graph GUID 为空：EffectId={entry.effectId}，Name={entry.effectName}");

                    report.EffectEntries.Add(new BuffAuthoringIdEntry
                    {
                        Id = entry.effectId,
                        Name = entry.effectName,
                        ClassName = entry.className,
                        SourceKind = SourceRegistryJson,
                        Path = entry.scriptPath,
                        Guid = entry.graphGuid,
                        Status = entry.status,
                        IsReserved = IsReserved(entry.effectId)
                    });
                }
            }
        }

        private static void Analyze(BuffAuthoringIdRegistryScanReport report, BuffAuthoringIdRegistryData registryData)
        {
            AnalyzeEntries(report.BuffEntries, "Buff ConfigId", report.Errors, report.Warnings);
            AnalyzeEntries(report.EffectEntries, "EffectId", report.Errors, report.Warnings);

            int buffStart = registryData != null && registryData.nextBuffConfigId > 0
                ? registryData.nextBuffConfigId
                : DefaultNextBuffConfigId;
            int effectStart = registryData != null && registryData.nextEffectId > 0
                ? registryData.nextEffectId
                : DefaultNextEffectId;

            report.RecommendedNextBuffConfigId = FindNextAvailable(buffStart, report.BuffEntries);
            report.RecommendedNextEffectId = FindNextAvailable(effectStart, report.EffectEntries);

            report.Infos.Add($"推荐下一个 Buff ConfigId：{report.RecommendedNextBuffConfigId}");
            report.Infos.Add($"推荐下一个 EffectId：{report.RecommendedNextEffectId}");
            report.Infos.Add($"扫描到 Buff ID 占用：{report.BuffEntries.Count}");
            report.Infos.Add($"扫描到 Effect ID 占用：{report.EffectEntries.Count}");
            report.Infos.Add($"Registry JSON 路径：{report.RegistryPath}");
        }

        private static void AnalyzeEntries(List<BuffAuthoringIdEntry> entries, string label, List<string> errors, List<string> warnings)
        {
            Dictionary<int, HashSet<string>> namesById = new Dictionary<int, HashSet<string>>();
            Dictionary<string, HashSet<int>> idsByName = new Dictionary<string, HashSet<int>>();

            for (int i = 0; i < entries.Count; i++)
            {
                BuffAuthoringIdEntry entry = entries[i];
                string name = string.IsNullOrWhiteSpace(entry.Name) ? entry.ClassName : entry.Name;
                if (string.IsNullOrWhiteSpace(name))
                    name = "<empty>";

                if (entry.Id <= 0)
                    errors.Add($"{label} 小于等于 0：{entry.Id}，来源：{entry.Path}");

                if (entry.IsReserved)
                    warnings.Add($"{label} 位于 990000+ 保留段：{entry.Id}，来源：{entry.SourceKind}，路径：{entry.Path}");

                if (!namesById.ContainsKey(entry.Id))
                    namesById.Add(entry.Id, new HashSet<string>());
                namesById[entry.Id].Add(name);

                if (!idsByName.ContainsKey(name))
                    idsByName.Add(name, new HashSet<int>());
                idsByName[name].Add(entry.Id);
            }

            foreach (KeyValuePair<int, HashSet<string>> pair in namesById)
            {
                if (pair.Value.Count > 1)
                    errors.Add($"{label} 冲突：ID={pair.Key} 被多个名称占用：{JoinValues(pair.Value)}");
            }

            foreach (KeyValuePair<string, HashSet<int>> pair in idsByName)
            {
                if (pair.Key == "<empty>")
                    continue;

                if (pair.Value.Count > 1)
                    warnings.Add($"名称对应多个 {label}：{pair.Key} -> {JoinValues(pair.Value)}");
            }
        }

        private static int FindNextAvailable(int start, List<BuffAuthoringIdEntry> entries)
        {
            HashSet<int> occupied = new HashSet<int>();
            for (int i = 0; i < entries.Count; i++)
                occupied.Add(entries[i].Id);

            int id = Math.Max(1, start);
            while (id < ReservedDebugIdStart)
            {
                if (!occupied.Contains(id))
                    return id;

                id++;
            }

            return -1;
        }

        private static void AddUniqueFolder(List<string> folders, string folder, BuffAuthoringIdRegistryScanReport report)
        {
            string normalized = NormalizePath(folder);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (!AssetDatabase.IsValidFolder(normalized))
            {
                report.Warnings.Add($"BuffConfigData 扫描路径不存在：{normalized}");
                return;
            }

            if (!folders.Contains(normalized))
                folders.Add(normalized);
        }

        private static bool TryParseId(string raw, out int id)
        {
            return int.TryParse(raw, out id);
        }

        private static string FindFirstClassName(string text)
        {
            Match match = ClassRegex.Match(text);
            return match.Success ? match.Groups["class"].Value : string.Empty;
        }

        private static bool IsReserved(int id)
        {
            return id >= ReservedDebugIdStart;
        }

        private static string JoinValues<T>(IEnumerable<T> values)
        {
            StringBuilder builder = new StringBuilder();
            bool hasValue = false;
            foreach (T value in values)
            {
                if (hasValue)
                    builder.Append(", ");

                builder.Append(value);
                hasValue = true;
            }

            return builder.ToString();
        }

        private static string NormalizePath(string path)
        {
            return BuffAuthoringHubSettings.NormalizePath(path);
        }
    }
}
