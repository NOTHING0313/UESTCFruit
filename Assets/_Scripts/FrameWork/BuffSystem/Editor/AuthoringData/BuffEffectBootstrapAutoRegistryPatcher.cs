using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace BuffSystem
{
    /// <summary>
    /// 维护 BuffEffectRegistryBootstrap 中的 Effect 自动注册区块。
    /// </summary>
    internal static class BuffEffectBootstrapAutoRegistryPatcher
    {
        private static readonly Regex TypeNameRegex = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
            RegexOptions.Compiled);

        internal static bool TryUpsertAutoRegistration(
            int effectId,
            string effectClassName,
            out BuffEffectBootstrapAutoRegistryReport report)
        {
            report = new BuffEffectBootstrapAutoRegistryReport
            {
                BootstrapPath = BuffEffectBootstrapRegistrationScanner.BootstrapPath
            };

            if (!ValidateInput(effectId, effectClassName, report))
                return false;

            BuffEffectBootstrapRegistrationScanReport scanReport = BuffEffectBootstrapRegistrationScanner.Scan();
            if (!scanReport.FileExists)
            {
                report.Errors.Add("未找到 BuffEffectRegistryBootstrap.cs。");
                return false;
            }

            for (int i = 0; i < scanReport.Errors.Count; i++)
                report.Errors.Add(scanReport.Errors[i]);

            if (report.HasError)
                return false;

            if (!CheckConflicts(effectId, effectClassName, scanReport, report))
                return false;

            string[] lines = File.ReadAllLines(BuffEffectBootstrapRegistrationScanner.BootstrapPath);
            if (!ContainsRegisterProductionEffects(lines))
            {
                report.Errors.Add("未找到 RegisterProductionEffects 方法，已停止写入。");
                return false;
            }

            Dictionary<int, string> autoEntries = BuildAutoEntryMap(scanReport);
            autoEntries[effectId] = effectClassName.Trim();
            string[] newLines = scanReport.HasAutoBlock
                ? ReplaceExistingAutoBlock(lines, scanReport, autoEntries)
                : InsertNewAutoBlock(lines, scanReport, autoEntries);

            File.WriteAllText(
                BuffEffectBootstrapRegistrationScanner.BootstrapPath,
                string.Join("\n", newLines) + "\n",
                Encoding.UTF8);

            AssetDatabase.Refresh();
            report.Succeeded = true;
            report.WroteFile = true;
            report.Infos.Add("已写入 BuffEffectRegistryBootstrap auto 区块，请等待 Unity 编译完成。");
            return true;
        }

        private static bool ValidateInput(int effectId, string effectClassName, BuffEffectBootstrapAutoRegistryReport report)
        {
            if (effectId <= 0)
                report.Errors.Add("EffectId 必须大于 0。");

            if (effectId >= BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
                report.Errors.Add("EffectId 位于 990000+ Debug / Smoke / Reserved 保留段，普通自动注册不能使用。");

            if (string.IsNullOrWhiteSpace(effectClassName))
            {
                report.Errors.Add("Effect class name 为空。");
            }
            else if (!TypeNameRegex.IsMatch(effectClassName.Trim()))
            {
                report.Errors.Add("Effect class name 不是合法的 C# 类型名。");
            }

            return !report.HasError;
        }

        private static bool CheckConflicts(
            int effectId,
            string effectClassName,
            BuffEffectBootstrapRegistrationScanReport scanReport,
            BuffEffectBootstrapAutoRegistryReport report)
        {
            string normalizedClassName = effectClassName.Trim();
            bool foundSameAutoEntry = false;

            for (int i = 0; i < scanReport.Entries.Count; i++)
            {
                BuffEffectBootstrapRegistrationEntry entry = scanReport.Entries[i];
                if (entry.EffectId <= 0)
                    continue;

                bool sameId = entry.EffectId == effectId;
                bool sameClass = entry.EffectClassName == normalizedClassName;

                if (!entry.IsAutoBlock)
                {
                    if (sameId)
                        report.Errors.Add($"EffectId {effectId} 已在手工注册区使用：line {entry.LineNumber}。");

                    if (sameClass && !sameId)
                        report.Errors.Add($"Effect class {normalizedClassName} 已在手工注册区使用不同 EffectId：{entry.EffectId}。");
                }
                else
                {
                    if (sameId && !sameClass)
                        report.Errors.Add($"auto 区块中 EffectId {effectId} 已绑定到不同 class：{entry.EffectClassName}。");

                    if (sameClass && !sameId)
                        report.Errors.Add($"auto 区块中 Effect class {normalizedClassName} 已绑定到不同 EffectId：{entry.EffectId}。");

                    if (sameId && sameClass)
                        foundSameAutoEntry = true;
                }
            }

            if (foundSameAutoEntry && !report.HasError)
                report.Infos.Add("auto 区块中已存在相同 EffectId / class，执行标准化写回。");

            return !report.HasError;
        }

        private static Dictionary<int, string> BuildAutoEntryMap(BuffEffectBootstrapRegistrationScanReport scanReport)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();
            for (int i = 0; i < scanReport.Entries.Count; i++)
            {
                BuffEffectBootstrapRegistrationEntry entry = scanReport.Entries[i];
                if (!entry.IsAutoBlock || entry.EffectId <= 0)
                    continue;

                result[entry.EffectId] = entry.EffectClassName;
            }

            return result;
        }

        private static string[] ReplaceExistingAutoBlock(
            string[] lines,
            BuffEffectBootstrapRegistrationScanReport scanReport,
            Dictionary<int, string> autoEntries)
        {
            List<string> result = new List<string>();
            int startIndex = scanReport.AutoStartLine - 1;
            int endIndex = scanReport.AutoEndLine - 1;

            for (int i = 0; i < startIndex; i++)
                result.Add(lines[i]);

            result.AddRange(BuildAutoBlock(lines[startIndex], autoEntries));

            for (int i = endIndex + 1; i < lines.Length; i++)
                result.Add(lines[i]);

            return result.ToArray();
        }

        private static string[] InsertNewAutoBlock(
            string[] lines,
            BuffEffectBootstrapRegistrationScanReport scanReport,
            Dictionary<int, string> autoEntries)
        {
            List<string> result = new List<string>(lines);
            int insertIndex = FindInsertIndex(lines, scanReport);
            string indent = DetectRegisterIndent(lines, scanReport);
            List<string> block = BuildAutoBlock(indent + BuffEffectBootstrapRegistrationScanner.AutoStartMarker, autoEntries);
            block.Insert(0, string.Empty);
            result.InsertRange(insertIndex, block);
            return result.ToArray();
        }

        private static int FindInsertIndex(string[] lines, BuffEffectBootstrapRegistrationScanReport scanReport)
        {
            int lastManualRegisterLine = -1;
            for (int i = 0; i < scanReport.Entries.Count; i++)
            {
                BuffEffectBootstrapRegistrationEntry entry = scanReport.Entries[i];
                if (!entry.IsAutoBlock)
                    lastManualRegisterLine = entry.LineNumber - 1;
            }

            if (lastManualRegisterLine >= 0)
                return lastManualRegisterLine + 1;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("return;"))
                    return i + 1;
            }

            return lines.Length;
        }

        private static string DetectRegisterIndent(string[] lines, BuffEffectBootstrapRegistrationScanReport scanReport)
        {
            for (int i = 0; i < scanReport.Entries.Count; i++)
            {
                string rawLine = lines[scanReport.Entries[i].LineNumber - 1];
                return rawLine.Substring(0, rawLine.Length - rawLine.TrimStart().Length);
            }

            return "            ";
        }

        private static List<string> BuildAutoBlock(string markerLine, Dictionary<int, string> autoEntries)
        {
            string indent = markerLine.Substring(0, markerLine.Length - markerLine.TrimStart().Length);
            List<string> result = new List<string>
            {
                indent + BuffEffectBootstrapRegistrationScanner.AutoStartMarker,
                indent + "// This block is maintained by Buff Authoring Hub.",
                indent + "// Manual edits inside this block may be overwritten.",
                indent + "// Move long-term custom registrations outside this block.",
                indent + "// Auto registration does not imply whitelist approval or runtime validation."
            };

            foreach (KeyValuePair<int, string> pair in autoEntries.OrderBy(pair => pair.Key))
                result.Add($"{indent}registry.Register({pair.Key}, new {pair.Value}());");

            result.Add(indent + BuffEffectBootstrapRegistrationScanner.AutoEndMarker);
            return result;
        }

        private static bool ContainsRegisterProductionEffects(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("RegisterProductionEffects"))
                    return true;
            }

            return false;
        }
    }
}
