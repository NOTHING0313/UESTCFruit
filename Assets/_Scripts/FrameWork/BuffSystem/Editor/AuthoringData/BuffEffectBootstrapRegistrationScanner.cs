using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace BuffSystem
{
    /// <summary>
    /// 只读扫描 BuffEffectRegistryBootstrap 中的 Effect 注册行和 auto 区块。
    /// </summary>
    internal static class BuffEffectBootstrapRegistrationScanner
    {
        internal const string BootstrapPath = "Assets/_Scripts/FrameWork/BuffSystem/BuffEffectRegistryBootstrap.cs";
        internal const string AutoStartMarker = "// <buffsystem-auto-effect-registry>";
        internal const string AutoEndMarker = "// </buffsystem-auto-effect-registry>";

        private static readonly Regex RegisterRegex = new Regex(
            @"registry\.Register\s*\(\s*(?<id>[A-Za-z_][A-Za-z0-9_]*|\d+)\s*,\s*new\s+(?<class>[A-Za-z_][A-Za-z0-9_\.]*)\s*\(\s*\)\s*\)\s*;",
            RegexOptions.Compiled);

        private static readonly Regex ConstIntRegex = new Regex(
            @"const\s+int\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<id>\d+)",
            RegexOptions.Compiled);

        internal static BuffEffectBootstrapRegistrationScanReport Scan()
        {
            BuffEffectBootstrapRegistrationScanReport report = new BuffEffectBootstrapRegistrationScanReport
            {
                BootstrapPath = BootstrapPath,
                FileExists = File.Exists(BootstrapPath)
            };

            if (!report.FileExists)
            {
                report.Errors.Add("未找到 BuffEffectRegistryBootstrap.cs。");
                return report;
            }

            string[] lines = File.ReadAllLines(BootstrapPath);
            Dictionary<string, int> constIds = ScanConstIds(lines);
            bool insideAutoBlock = false;
            int startCount = 0;
            int endCount = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains(AutoStartMarker))
                {
                    startCount++;
                    insideAutoBlock = true;
                    report.HasAutoBlock = true;
                    report.AutoStartLine = i + 1;
                }

                Match match = RegisterRegex.Match(line);
                if (match.Success)
                {
                    string idToken = match.Groups["id"].Value;
                    int effectId = ResolveEffectId(idToken, constIds);
                    report.Entries.Add(new BuffEffectBootstrapRegistrationEntry
                    {
                        EffectId = effectId,
                        EffectIdToken = idToken,
                        EffectClassName = match.Groups["class"].Value,
                        IsAutoBlock = insideAutoBlock,
                        LineNumber = i + 1,
                        RawLine = line.Trim()
                    });
                }

                if (line.Contains(AutoEndMarker))
                {
                    endCount++;
                    report.AutoEndLine = i + 1;
                    insideAutoBlock = false;
                }
            }

            report.MarkerPaired = startCount == endCount && startCount <= 1;
            if (!report.MarkerPaired)
                report.Errors.Add("auto 注册区块 marker 不成对或存在多个 auto 区块。");

            if (insideAutoBlock)
                report.Errors.Add("auto 注册区块缺少结束 marker。");

            return report;
        }

        private static Dictionary<string, int> ScanConstIds(string[] lines)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            for (int i = 0; i < lines.Length; i++)
            {
                Match match = ConstIntRegex.Match(lines[i]);
                if (!match.Success)
                    continue;

                if (int.TryParse(match.Groups["id"].Value, out int id))
                    result[match.Groups["name"].Value] = id;
            }

            return result;
        }

        private static int ResolveEffectId(string token, Dictionary<string, int> constIds)
        {
            if (int.TryParse(token, out int id))
                return id;

            return constIds.TryGetValue(token, out id) ? id : -1;
        }
    }

    internal sealed class BuffEffectBootstrapRegistrationScanReport
    {
        internal readonly List<BuffEffectBootstrapRegistrationEntry> Entries = new List<BuffEffectBootstrapRegistrationEntry>();
        internal readonly List<string> Errors = new List<string>();

        internal string BootstrapPath;
        internal bool FileExists;
        internal bool HasAutoBlock;
        internal bool MarkerPaired;
        internal int AutoStartLine;
        internal int AutoEndLine;
        internal bool HasError => Errors.Count > 0;
    }

    internal sealed class BuffEffectBootstrapRegistrationEntry
    {
        internal int EffectId;
        internal string EffectIdToken;
        internal string EffectClassName;
        internal bool IsAutoBlock;
        internal int LineNumber;
        internal string RawLine;
    }
}
