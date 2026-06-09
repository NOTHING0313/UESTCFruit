using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BuffSystem.Editor.AuthoringGraphs;

namespace BuffSystem
{
    /// <summary>
    /// Buff 配置制作只读校验窗口；只扫描和报告，不修改任何 Buff asset 或运行时配置。
    /// </summary>
    public sealed class BuffAuthoringValidatorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/BuffSystem/Authoring Validator";
        private const string BuffAssetRoot = "Assets/Resources/BuffSystem/Buff";
        private const int SmokePilotConfigId = 991001;
        private const float ResultLabelWidth = 180f;

        private readonly List<BuffValidationResult> _results = new List<BuffValidationResult>();
        private readonly Dictionary<BuffCategory, int> _categoryCounts = new Dictionary<BuffCategory, int>();
        private Vector2 _scroll;
        private string _summary = "尚未扫描。";
        private BuffCandidateGraphSummary _candidateSummary;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            BuffAuthoringHubWindow.OpenValidator();
        }

        private void OnGUI()
        {
            DrawEmbedded();
        }

        internal void DrawEmbedded()
        {
            DrawEmbedded(null);
        }

        internal void DrawEmbedded(BuffCandidateGraphSummary candidateSummary)
        {
            _candidateSummary = candidateSummary;
            EditorGUILayout.LabelField(BuffAuthoringText.ValidatorTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(BuffAuthoringText.ScanPath, BuffAssetRoot);
            EditorGUILayout.HelpBox(BuffAuthoringText.ValidatorHelp, MessageType.Info);
            DrawCandidateGraphHint();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.ScanRefresh, GUILayout.Width(140f)))
                    Scan();

                GUILayout.Label(_summary, EditorStyles.miniBoldLabel);
            }

            DrawStatistics();
            EditorGUILayout.Space(6f);
            DrawResults();
        }

        private void DrawCandidateGraphHint()
        {
            if (_candidateSummary == null || _candidateSummary.Graph == null)
                return;

            bool exists = BuffCandidateGraphBridge.RealBuffConfigExists(_candidateSummary.ConfigId);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(BuffAuthoringText.CandidateGraphCompare, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(BuffAuthoringText.CandidateGraphConfigId, _candidateSummary.ConfigId.ToString());
            EditorGUILayout.LabelField(BuffAuthoringText.RealBuffConfigExists, exists ? BuffAuthoringText.True : BuffAuthoringText.False);
            EditorGUILayout.HelpBox(
                exists
                    ? "已存在同 ConfigId 的真实 BuffConfigData，可用 Validator 对照检查。"
                    : "当前候选图尚未落地为 BuffConfigData。",
                exists ? MessageType.Info : MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        private void DrawStatistics()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawStat(BuffAuthoringText.Total, _results.Count);
                DrawStat(BuffAuthoringText.Eligible, GetCategoryCount(BuffCategory.EligibleCandidate));
                DrawStat(BuffAuthoringText.NearMiss, GetCategoryCount(BuffCategory.NearMiss));
                DrawStat(BuffAuthoringText.NotCandidate, GetCategoryCount(BuffCategory.NotCandidate));
                DrawStat(BuffAuthoringText.SmokeDebug, GetCategoryCount(BuffCategory.SmokeDebugOnly));
                DrawStat(BuffAuthoringText.Invalid, GetCategoryCount(BuffCategory.Invalid));
            }
        }

        private static void DrawStat(string label, int value)
        {
            GUILayout.Label($"{label}: {value}", GUILayout.Width(130f));
        }

        private void DrawResults()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_results.Count == 0)
            {
                EditorGUILayout.HelpBox(BuffAuthoringText.ScanResultEmpty, MessageType.None);
            }

            for (int i = 0; i < _results.Count; i++)
                DrawResult(_results[i]);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawResult(BuffValidationResult result)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"{result.ConfigId} - {result.Name}", EditorStyles.boldLabel);

            DrawReadOnlyField(BuffAuthoringText.AssetPath, result.AssetPath);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(BuffAuthoringText.Behavior, EditorStyles.miniBoldLabel);
            DrawReadOnlyField(BuffAuthoringText.BuffType, result.BuffType);
            DrawReadOnlyField(BuffAuthoringText.TriggerType, result.TriggerType);
            DrawReadOnlyField(BuffAuthoringText.Storage, result.ParallelStorageMode);
            DrawReadOnlyField(BuffAuthoringText.Unlimited, result.Unlimited);
            DrawReadOnlyField(BuffAuthoringText.MaxStack, result.MaxStack);
            DrawReadOnlyField(BuffAuthoringText.Duration, result.Duration);
            DrawReadOnlyField(BuffAuthoringText.TickTime, result.TickTime);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(BuffAuthoringText.Effect, EditorStyles.miniBoldLabel);
            DrawReadOnlyField(BuffAuthoringText.EffectId, result.EffectId);
            DrawReadOnlyField(BuffAuthoringText.EffectRegistered, result.EffectRegistered);
            DrawReadOnlyField(BuffAuthoringText.CompressedEligibility, result.CompressedEligibility);
            DrawReadOnlyField(BuffAuthoringText.Category, result.Category);

            EditorGUILayout.LabelField(BuffAuthoringText.Issues);
            EditorGUILayout.TextArea(result.IssuesText, GUILayout.MinHeight(36f));
            EditorGUILayout.EndVertical();
        }

        private static void DrawReadOnlyField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(ResultLabelWidth));
                EditorGUILayout.SelectableLabel(
                    value ?? string.Empty,
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight),
                    GUILayout.ExpandWidth(true));
            }
        }

        private void Scan()
        {
            _results.Clear();
            _categoryCounts.Clear();

            List<BuffAssetSummary> summaries = BuffAuthoringValidationUtility.ScanBuffAssets(BuffAssetRoot);
            Dictionary<int, int> idCounts = BuffAuthoringValidationUtility.BuildConfigIdIndex(summaries);

            for (int i = 0; i < summaries.Count; i++)
            {
                BuffValidationResult result = Validate(summaries[i], idCounts);
                _results.Add(result);
                IncrementCategory(result.CategoryValue);
            }

            _results.Sort(CompareResults);
            _summary = $"扫描完成。{BuffAuthoringText.Total}={_results.Count}, {BuffAuthoringText.Eligible}={GetCategoryCount(BuffCategory.EligibleCandidate)}, {BuffAuthoringText.SmokeDebug}={GetCategoryCount(BuffCategory.SmokeDebugOnly)}, {BuffAuthoringText.Invalid}={GetCategoryCount(BuffCategory.Invalid)}";
            Debug.Log($"[BuffAuthoringValidator] {_summary}");
        }

        private static BuffValidationResult Validate(BuffAssetSummary summary, Dictionary<int, int> idCounts)
        {
            BuffConfigData config = summary.SourceAsset;
            List<string> issues = new List<string>();
            bool duplicateId = idCounts.TryGetValue(summary.ConfigId, out int count) && count > 1;
            EffectRegistryCheckResult registryCheck = BuffAuthoringValidationUtility.CheckProductionEffectRegistered(summary.EffectId);
            bool registryAvailable = !registryCheck.IsUnknown;
            bool effectRegistered = registryCheck.IsRegistered;
            bool inRoot = IsUnderBuffRoot(summary.AssetPath);
            CompressedEligibilityResult compressedEligibility = BuffAuthoringValidationUtility.ComputeCompressedEligibility(config);
            bool smoke = summary.IsSmokeOrDebug;

            if (config.ID <= 0)
                issues.Add("错误：ConfigId 必须大于 0。");

            if (duplicateId)
                issues.Add($"错误：ConfigId 重复，当前扫描到 {count} 个相同 ID。");

            if (string.IsNullOrWhiteSpace(config.Name))
                issues.Add("错误：Name 不能为空。");

            if (config.EffectId <= 0)
                issues.Add("错误：EffectId 必须大于 0。");
            else if (registryAvailable && !effectRegistered)
                issues.Add("警告：EffectId 未注册到 production BuffEffectRegistry。");

            if (!registryAvailable)
                issues.Add($"警告：production effect registry 反射检查不完整：{registryCheck.Status}");

            if (!inRoot)
                issues.Add($"警告：asset 不在默认 Resources root：{BuffAssetRoot}。");

            AddCombinationIssues(config, issues);
            AddCompressedEligibilityIssues(config, compressedEligibility.IsEligible, issues);
            AddLifetimeIssues(config, issues);

            BuffCategory category = Classify(config, duplicateId, registryAvailable && effectRegistered, compressedEligibility.IsEligible, smoke);

            if (smoke)
                issues.Add("建议：该配置疑似 Debug / Smoke asset，不应作为正式玩法 Buff 加入 whitelist。");

            if (issues.Count == 0)
                issues.Add("未发现明显制作问题。");

            return new BuffValidationResult
            {
                ConfigId = config.ID.ToString(),
                Name = string.IsNullOrWhiteSpace(config.Name) ? "<empty>" : config.Name,
                AssetPath = summary.AssetPath,
                BuffType = config.BuffType.ToString(),
                TriggerType = config.BuffTriggerType.ToString(),
                ParallelStorageMode = config.ParallelStorageMode.ToString(),
                Unlimited = config.Unlimited.ToString(),
                MaxStack = config.MaxStack.ToString(),
                Duration = config.Duration.ToString("0.###"),
                TickTime = config.TickTime.ToString("0.###"),
                EffectId = config.EffectId.ToString(),
                EffectRegistered = registryAvailable ? FormatBool(effectRegistered) : BuffAuthoringText.Unknown,
                CompressedEligibility = FormatBool(compressedEligibility.IsEligible),
                Category = GetCategoryLabel(category),
                CategoryValue = category,
                IssuesText = string.Join("\n", issues)
            };
        }

        private static void AddCombinationIssues(BuffConfigData config, List<string> issues)
        {
            if (config.BuffType == BuffInstanceType.normal && config.ParallelStorageMode == ParallelBuffStorageMode.CompressedExpiryFrameList)
                issues.Add("警告：normal Buff 不使用 ParallelStorageMode，当前 compressed 设置不会让 normal Buff 进入 compressed path。");

            if (config.BuffTriggerType == BuffTriggerType.EventTrigger)
                issues.Add("提示：EventTrigger Buff 按设计 fallback EntityPerStack，不应作为 compressed whitelist 候选。");

            if (config.BuffType != BuffInstanceType.parallel && config.ParallelStorageMode == ParallelBuffStorageMode.CompressedExpiryFrameList)
                issues.Add("警告：CompressedExpiryFrameList 只对 parallel Buff 有意义。");
        }

        private static void AddCompressedEligibilityIssues(BuffConfigData config, bool compressedEligibility, List<string> issues)
        {
            if (compressedEligibility)
                return;

            if (config.BuffType != BuffInstanceType.parallel)
                issues.Add("compressed eligibility 未满足：BuffType 不是 parallel。");

            if (config.ParallelStorageMode != ParallelBuffStorageMode.CompressedExpiryFrameList)
                issues.Add("compressed eligibility 未满足：ParallelStorageMode 不是 CompressedExpiryFrameList。");

            if (config.BuffTriggerType != BuffTriggerType.Tick)
                issues.Add("compressed eligibility 未满足：TriggerType 不是 Tick。");

            if (config.Unlimited)
                issues.Add("compressed eligibility 未满足：Unlimited 会阻止 compressed。");

            if (!config.Unlimited && config.MaxStack > CompressedParallelBuffLayerBuffer.Capacity)
                issues.Add($"compressed eligibility 未满足：MaxStack={config.MaxStack} 超过 Capacity={CompressedParallelBuffLayerBuffer.Capacity}。");
        }

        private static void AddLifetimeIssues(BuffConfigData config, List<string> issues)
        {
            if (!config.IsForever && config.Duration <= 0f)
                issues.Add("错误：非永久 Buff 的 Duration 必须大于 0。");

            if (config.BuffTriggerType == BuffTriggerType.Tick && config.TickTime <= 0f)
                issues.Add("错误：Tick Buff 的 TickTime 必须大于 0。");

            if (!config.Unlimited && config.MaxStack <= 0)
                issues.Add("错误：非 Unlimited Buff 的 MaxStack 必须大于 0。");
        }

        private static BuffCategory Classify(
            BuffConfigData config,
            bool duplicateId,
            bool effectRegistered,
            bool compressedEligibility,
            bool smoke)
        {
            if (config.ID <= 0 || duplicateId || string.IsNullOrWhiteSpace(config.Name) || config.EffectId <= 0)
                return BuffCategory.Invalid;

            if (smoke)
                return BuffCategory.SmokeDebugOnly;

            if (compressedEligibility && effectRegistered)
                return BuffCategory.EligibleCandidate;

            if (IsNearMiss(config))
                return BuffCategory.NearMiss;

            return BuffCategory.NotCandidate;
        }

        private static bool IsNearMiss(BuffConfigData config)
        {
            if (config.BuffType != BuffInstanceType.parallel)
                return false;

            if (config.BuffTriggerType != BuffTriggerType.Tick)
                return false;

            return true;
        }

        private static bool IsUnderBuffRoot(string assetPath)
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.StartsWith(BuffAssetRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareResults(BuffValidationResult left, BuffValidationResult right)
        {
            int categoryCompare = left.CategoryValue.CompareTo(right.CategoryValue);
            if (categoryCompare != 0)
                return categoryCompare;

            int.TryParse(left.ConfigId, out int leftId);
            int.TryParse(right.ConfigId, out int rightId);
            return leftId.CompareTo(rightId);
        }

        private void IncrementCategory(BuffCategory category)
        {
            if (!_categoryCounts.ContainsKey(category))
                _categoryCounts.Add(category, 0);

            _categoryCounts[category]++;
        }

        private int GetCategoryCount(BuffCategory category)
        {
            return _categoryCounts.TryGetValue(category, out int count) ? count : 0;
        }

        private static string GetCategoryLabel(BuffCategory category)
        {
            switch (category)
            {
                case BuffCategory.EligibleCandidate:
                    return BuffAuthoringText.CategoryEligibleCandidate;
                case BuffCategory.NearMiss:
                    return BuffAuthoringText.CategoryNearMiss;
                case BuffCategory.NotCandidate:
                    return BuffAuthoringText.CategoryNotCandidate;
                case BuffCategory.SmokeDebugOnly:
                    return BuffAuthoringText.CategorySmokeDebugOnly;
                case BuffCategory.Invalid:
                    return BuffAuthoringText.CategoryInvalid;
                default:
                    return category.ToString();
            }
        }

        private static string FormatBool(bool value)
        {
            return value ? BuffAuthoringText.True : BuffAuthoringText.False;
        }

        private enum BuffCategory
        {
            EligibleCandidate = 0,
            NearMiss = 1,
            NotCandidate = 2,
            SmokeDebugOnly = 3,
            Invalid = 4
        }

        private sealed class BuffValidationResult
        {
            public string ConfigId;
            public string Name;
            public string AssetPath;
            public string BuffType;
            public string TriggerType;
            public string ParallelStorageMode;
            public string Unlimited;
            public string MaxStack;
            public string Duration;
            public string TickTime;
            public string EffectId;
            public string EffectRegistered;
            public string CompressedEligibility;
            public string Category;
            public BuffCategory CategoryValue;
            public string IssuesText;
        }
    }
}
