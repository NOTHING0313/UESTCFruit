using System.Collections.Generic;
using System.Text;

namespace BuffSystem
{
    /// <summary>
    /// ID Registry 只读扫描报告。
    /// 该报告只用于 Authoring Hub 展示和复制，不写入项目文件。
    /// </summary>
    internal sealed class BuffAuthoringIdRegistryScanReport
    {
        public string RegistryPath;
        public bool RegistryExists;
        public bool RegistryParseSucceeded;
        public int RecommendedNextBuffConfigId;
        public int RecommendedNextEffectId;
        public readonly List<BuffAuthoringIdEntry> BuffEntries = new List<BuffAuthoringIdEntry>();
        public readonly List<BuffAuthoringIdEntry> EffectEntries = new List<BuffAuthoringIdEntry>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Infos = new List<string>();

        public string ToPlainText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("========== BuffSystem ID Registry Scan Report ==========");
            builder.AppendLine($"RegistryPath: {RegistryPath}");
            builder.AppendLine($"RegistryExists: {RegistryExists}");
            builder.AppendLine($"RegistryParseSucceeded: {RegistryParseSucceeded}");
            builder.AppendLine($"RecommendedNextBuffConfigId: {RecommendedNextBuffConfigId}");
            builder.AppendLine($"RecommendedNextEffectId: {RecommendedNextEffectId}");
            builder.AppendLine($"BuffEntryCount: {BuffEntries.Count}");
            builder.AppendLine($"EffectEntryCount: {EffectEntries.Count}");
            AppendSection(builder, "Errors", Errors);
            AppendSection(builder, "Warnings", Warnings);
            AppendSection(builder, "Infos", Infos);
            AppendEntries(builder, "Buff Entries", BuffEntries);
            AppendEntries(builder, "Effect Entries", EffectEntries);
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string title, List<string> lines)
        {
            builder.AppendLine();
            builder.AppendLine($"[{title}]");
            if (lines.Count == 0)
            {
                builder.AppendLine("None");
                return;
            }

            for (int i = 0; i < lines.Count; i++)
                builder.AppendLine($"- {lines[i]}");
        }

        private static void AppendEntries(StringBuilder builder, string title, List<BuffAuthoringIdEntry> entries)
        {
            builder.AppendLine();
            builder.AppendLine($"[{title}]");
            if (entries.Count == 0)
            {
                builder.AppendLine("None");
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                BuffAuthoringIdEntry entry = entries[i];
                builder.AppendLine($"- Id={entry.Id}, Name={entry.Name}, ClassName={entry.ClassName}, Status={entry.Status}, Source={entry.SourceKind}, Reserved={entry.IsReserved}, Path={entry.Path}, Guid={entry.Guid}");
            }
        }
    }

    internal sealed class BuffAuthoringIdEntry
    {
        public int Id;
        public string Name;
        public string ClassName;
        public string SourceKind;
        public string Path;
        public string Guid;
        public string Status;
        public bool IsReserved;
    }
}
