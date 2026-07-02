using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// Authoring Hub 的内部 ID 服务。
    /// 只用于自动推荐和唯一性校验，不写入 Registry JSON，不创建资源，不修改 runtime。
    /// </summary>
    internal static class BuffAuthoringIdService
    {
        internal static int GetNextAvailableBuffConfigId(BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringIdRegistryScanReport report = BuffAuthoringIdRegistryScanner.Scan(settings);
            return GetRecommendedOfficialId(
                report.RecommendedNextBuffConfigId,
                BuffAuthoringIdRegistryScanner.DefaultNextBuffConfigId,
                report.BuffEntries);
        }

        internal static int GetNextAvailableEffectId(BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringIdRegistryScanReport report = BuffAuthoringIdRegistryScanner.Scan(settings);
            return GetRecommendedOfficialId(
                report.RecommendedNextEffectId,
                BuffAuthoringIdRegistryScanner.DefaultNextEffectId,
                report.EffectEntries);
        }

        internal static BuffAuthoringIdValidationResult ValidateBuffConfigId(int configId, BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringIdRegistryScanReport report = BuffAuthoringIdRegistryScanner.Scan(settings);
            BuffAuthoringIdValidationResult result = new BuffAuthoringIdValidationResult(configId);

            if (configId <= 0)
                result.Errors.Add("ConfigId 必须大于 0。");

            if (configId >= BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
                result.Errors.Add("ConfigId 位于 990000+ Debug / Smoke / Reserved 保留段，普通 Buff 不能使用该 ID。请点击“重新分配 Buff ID”。");

            AddIdConflicts(configId, report.BuffEntries, "ConfigId", result.Errors);
            return result;
        }

        internal static BuffAuthoringIdValidationResult ValidateEffectId(int effectId, BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringIdRegistryScanReport report = BuffAuthoringIdRegistryScanner.Scan(settings);
            BuffAuthoringIdValidationResult result = new BuffAuthoringIdValidationResult(effectId);

            if (effectId <= 0)
                result.Errors.Add("EffectId 必须大于 0。");

            if (effectId >= BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
                result.Errors.Add("EffectId 位于 990000+ Debug / Smoke / Reserved 保留段，普通 Effect 不能使用该 ID。请点击“重新分配 Effect ID”。");

            AddIdConflicts(effectId, report.EffectEntries, "EffectId", result.Errors);
            return result;
        }

        internal static bool ShouldReplaceBuffConfigId(int configId, BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringIdValidationResult result = ValidateBuffConfigId(configId, settings);
            return configId <= 0 || result.HasErrors;
        }

        internal static bool ShouldReplaceEffectId(int effectId, BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringIdValidationResult result = ValidateEffectId(effectId, settings);
            return effectId <= 0 || result.HasErrors;
        }

        private static void AddIdConflicts(int id, List<BuffAuthoringIdEntry> entries, string label, List<string> errors)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                BuffAuthoringIdEntry entry = entries[i];
                if (entry.Id != id)
                    continue;

                string name = string.IsNullOrWhiteSpace(entry.Name) ? entry.ClassName : entry.Name;
                errors.Add($"{label} 已被占用：{id}，来源={entry.SourceKind}，名称={name}，路径={entry.Path}");
            }
        }

        private static int GetRecommendedOfficialId(int recommendedId, int defaultStart, List<BuffAuthoringIdEntry> entries)
        {
            if (recommendedId > 0 && recommendedId < BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
                return recommendedId;

            HashSet<int> occupied = new HashSet<int>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                    occupied.Add(entries[i].Id);
            }

            int id = defaultStart;
            while (id < BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
            {
                if (!occupied.Contains(id))
                    return id;

                id++;
            }

            return -1;
        }
    }

    internal sealed class BuffAuthoringIdValidationResult
    {
        internal readonly int Id;
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();

        internal bool HasErrors => Errors.Count > 0;

        internal BuffAuthoringIdValidationResult(int id)
        {
            Id = id;
        }
    }
}
