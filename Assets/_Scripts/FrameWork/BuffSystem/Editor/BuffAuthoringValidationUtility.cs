using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace BuffSystem
{
    /// <summary>
    /// Buff 制作工具的 Editor-only 只读校验工具；不修改运行时、资源、白名单或注册表。
    /// </summary>
    internal static class BuffAuthoringValidationUtility
    {
        internal const string DefaultBuffAssetRoot = "Assets/Resources/BuffSystem/Buff";
        internal const string DefaultEffectFolder = "Assets/_Scripts/FrameWork/BuffSystem/Effects";
        internal const string BuffSystemRoot = "Assets/_Scripts/FrameWork/BuffSystem";
        private const int SmokePilotConfigId = 991001;

        private static readonly Regex EffectIdConstRegex = new Regex(
            @"const\s+int\s+EffectId\s*=\s*(\d+)",
            RegexOptions.Compiled);

        internal static List<BuffAssetSummary> ScanBuffAssets(string rootPath = DefaultBuffAssetRoot)
        {
            List<BuffAssetSummary> summaries = new List<BuffAssetSummary>();
            string[] guids = AssetDatabase.FindAssets("t:BuffConfigData", new[] { rootPath });

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                BuffConfigData config = AssetDatabase.LoadAssetAtPath<BuffConfigData>(assetPath);

                if (config == null)
                    continue;

                summaries.Add(new BuffAssetSummary(config, assetPath));
            }

            return summaries;
        }

        internal static Dictionary<int, int> BuildConfigIdIndex(IEnumerable<BuffAssetSummary> summaries)
        {
            Dictionary<int, int> index = new Dictionary<int, int>();

            foreach (BuffAssetSummary summary in summaries)
            {
                if (!index.ContainsKey(summary.ConfigId))
                    index.Add(summary.ConfigId, 0);

                index[summary.ConfigId]++;
            }

            return index;
        }

        internal static bool IsConfigIdDuplicate(int configId, Dictionary<int, int> index)
        {
            return index != null && index.TryGetValue(configId, out int count) && count > 0;
        }

        internal static EffectRegistryCheckResult CheckProductionEffectRegistered(int effectId)
        {
            if (effectId <= 0)
                return new EffectRegistryCheckResult(effectId, false, false, "OK", string.Empty);

            try
            {
                BuffEffectRegistry registry = new BuffEffectRegistry();
                Type bootstrapType = Type.GetType("BuffSystem.BuffEffectRegistryBootstrap, Assembly-CSharp");
                if (bootstrapType == null)
                    return new EffectRegistryCheckResult(effectId, false, true, "无法找到 BuffEffectRegistryBootstrap。", "无法找到 BuffEffectRegistryBootstrap。");

                System.Reflection.MethodInfo method = bootstrapType.GetMethod(
                    "RegisterProductionEffects",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (method == null)
                    return new EffectRegistryCheckResult(effectId, false, true, "无法找到 RegisterProductionEffects。", "无法找到 RegisterProductionEffects。");

                method.Invoke(null, new object[] { registry });
                bool registered = registry.TryGet(effectId, out _);
                return new EffectRegistryCheckResult(effectId, registered, false, "OK", string.Empty);
            }
            catch (Exception exception)
            {
                return new EffectRegistryCheckResult(effectId, false, true, exception.Message, exception.Message);
            }
        }

        internal static bool IsEffectIdUsedByBuffConfigData(int effectId, IEnumerable<BuffAssetSummary> summaries)
        {
            return GetBuffConfigEffectHits(effectId, summaries).Count > 0;
        }

        internal static List<BuffAssetSummary> GetBuffConfigEffectHits(int effectId, IEnumerable<BuffAssetSummary> summaries)
        {
            List<BuffAssetSummary> hits = new List<BuffAssetSummary>();

            if (effectId <= 0 || summaries == null)
                return hits;

            foreach (BuffAssetSummary summary in summaries)
            {
                if (summary.EffectId == effectId)
                    hits.Add(summary);
            }

            return hits;
        }

        internal static CompressedEligibilityResult ComputeCompressedEligibility(BuffConfigData config)
        {
            if (config == null)
                return new CompressedEligibilityResult(false, false, false, false, false, false, CompressedParallelBuffLayerBuffer.Capacity);

            return ComputeCompressedEligibility(
                config.BuffType,
                config.BuffTriggerType,
                config.ParallelStorageMode,
                config.Unlimited,
                config.MaxStack);
        }

        internal static CompressedEligibilityResult ComputeCompressedEligibility(
            BuffInstanceType buffType,
            BuffTriggerType triggerType,
            ParallelBuffStorageMode storageMode,
            bool unlimited,
            int maxStack)
        {
            bool buffTypeParallel = buffType == BuffInstanceType.parallel;
            bool storageCompressed = storageMode == ParallelBuffStorageMode.CompressedExpiryFrameList;
            bool triggerTick = triggerType == BuffTriggerType.Tick;
            bool unlimitedFalse = !unlimited;
            bool maxStackWithinCapacity = maxStack <= CompressedParallelBuffLayerBuffer.Capacity;
            bool eligible = buffTypeParallel && storageCompressed && triggerTick && unlimitedFalse && maxStackWithinCapacity;

            CompressedEligibilityResult result = new CompressedEligibilityResult(
                eligible,
                buffTypeParallel,
                storageCompressed,
                triggerTick,
                unlimitedFalse,
                maxStackWithinCapacity,
                CompressedParallelBuffLayerBuffer.Capacity);

            if (!buffTypeParallel)
                result.Reasons.Add("BuffType 不是 parallel。");

            if (!storageCompressed)
                result.Reasons.Add("ParallelStorageMode 不是 CompressedExpiryFrameList。");

            if (!triggerTick)
                result.Reasons.Add("TriggerType 不是 Tick。");

            if (!unlimitedFalse)
                result.Reasons.Add("Unlimited 会阻止 compressed。");

            if (!maxStackWithinCapacity)
                result.Reasons.Add($"MaxStack={maxStack} 超过 Capacity={CompressedParallelBuffLayerBuffer.Capacity}。");

            return result;
        }

        internal static bool IsDebugOrSmoke(int configId, string name)
        {
            if (configId == SmokePilotConfigId)
                return true;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Smoke", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static List<EffectIdConstantHit> ScanEffectIdConstants(string folderPath, int effectId)
        {
            List<EffectIdConstantHit> hits = new List<EffectIdConstantHit>();

            if (effectId <= 0 || !Directory.Exists(folderPath))
                return hits;

            string[] files = Directory.GetFiles(folderPath, "*.cs", SearchOption.TopDirectoryOnly);

            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i], Encoding.UTF8);
                MatchCollection matches = EffectIdConstRegex.Matches(text);

                for (int j = 0; j < matches.Count; j++)
                {
                    if (!int.TryParse(matches[j].Groups[1].Value, out int id))
                        continue;

                    if (id != effectId)
                        continue;

                    hits.Add(new EffectIdConstantHit(effectId, NormalizeAssetPath(files[i]), matches[j].Value));
                }
            }

            return hits;
        }

        internal static string MakeSafeFileName(string rawName, string fallback = "Buff")
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return fallback;

            HashSet<char> invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            StringBuilder builder = new StringBuilder(rawName.Length);

            for (int i = 0; i < rawName.Length; i++)
            {
                char c = rawName[i];
                if (invalidChars.Contains(c))
                    continue;

                builder.Append(char.IsWhiteSpace(c) ? '_' : c);
            }

            string safeName = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(safeName) ? fallback : safeName;
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }
    }

    internal sealed class BuffAssetSummary
    {
        internal readonly int ConfigId;
        internal readonly string Name;
        internal readonly string AssetPath;
        internal readonly int EffectId;
        internal readonly BuffInstanceType BuffType;
        internal readonly BuffTriggerType TriggerType;
        internal readonly ParallelBuffStorageMode ParallelStorageMode;
        internal readonly bool Unlimited;
        internal readonly int MaxStack;
        internal readonly float Duration;
        internal readonly float TickTime;
        internal readonly bool IsSmokeOrDebug;
        internal readonly BuffConfigData SourceAsset;

        internal BuffAssetSummary(BuffConfigData sourceAsset, string assetPath)
        {
            SourceAsset = sourceAsset;
            AssetPath = assetPath;
            ConfigId = sourceAsset.ID;
            Name = sourceAsset.Name;
            EffectId = sourceAsset.EffectId;
            BuffType = sourceAsset.BuffType;
            TriggerType = sourceAsset.BuffTriggerType;
            ParallelStorageMode = sourceAsset.ParallelStorageMode;
            Unlimited = sourceAsset.Unlimited;
            MaxStack = sourceAsset.MaxStack;
            Duration = sourceAsset.Duration;
            TickTime = sourceAsset.TickTime;
            IsSmokeOrDebug = BuffAuthoringValidationUtility.IsDebugOrSmoke(ConfigId, Name);
        }
    }

    internal readonly struct EffectRegistryCheckResult
    {
        internal readonly int EffectId;
        internal readonly bool IsRegistered;
        internal readonly bool IsUnknown;
        internal readonly string Status;
        internal readonly string ErrorMessage;

        internal EffectRegistryCheckResult(int effectId, bool isRegistered, bool isUnknown, string status, string errorMessage)
        {
            EffectId = effectId;
            IsRegistered = isRegistered;
            IsUnknown = isUnknown;
            Status = status;
            ErrorMessage = errorMessage;
        }
    }

    internal sealed class CompressedEligibilityResult
    {
        internal readonly bool IsEligible;
        internal readonly bool BuffTypeParallel;
        internal readonly bool StorageCompressed;
        internal readonly bool TriggerTick;
        internal readonly bool UnlimitedFalse;
        internal readonly bool MaxStackWithinCapacity;
        internal readonly int Capacity;
        internal readonly List<string> Reasons = new List<string>();

        internal CompressedEligibilityResult(
            bool isEligible,
            bool buffTypeParallel,
            bool storageCompressed,
            bool triggerTick,
            bool unlimitedFalse,
            bool maxStackWithinCapacity,
            int capacity)
        {
            IsEligible = isEligible;
            BuffTypeParallel = buffTypeParallel;
            StorageCompressed = storageCompressed;
            TriggerTick = triggerTick;
            UnlimitedFalse = unlimitedFalse;
            MaxStackWithinCapacity = maxStackWithinCapacity;
            Capacity = capacity;
        }
    }

    internal readonly struct EffectIdConstantHit
    {
        internal readonly int EffectId;
        internal readonly string FilePath;
        internal readonly string Summary;

        internal EffectIdConstantHit(int effectId, string filePath, string summary)
        {
            EffectId = effectId;
            FilePath = filePath;
            Summary = summary;
        }
    }
}
