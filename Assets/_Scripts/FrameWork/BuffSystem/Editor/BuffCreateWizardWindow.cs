using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using BuffSystem.Editor.AuthoringGraphs;

namespace BuffSystem
{
    /// <summary>
    /// BuffConfigData 草稿创建向导；只创建配置草稿，不修改运行时、白名单或 Effect 注册。
    /// </summary>
    public sealed class BuffCreateWizardWindow : EditorWindow
    {
        private const string MenuPath = "Tools/BuffSystem/Buff Create Wizard";
        private const string BuffAssetRoot = "Assets/Resources/BuffSystem/Buff";

        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _recommendations = new List<string>();

        private Vector2 _scroll;
        private int _configId = 100001;
        private string _buffName = "NewBuff";
        private string _description = string.Empty;
        private BuffInstanceType _buffType = BuffInstanceType.parallel;
        private BuffTriggerType _triggerType = BuffTriggerType.Tick;
        private ParallelBuffStorageMode _parallelStorageMode = ParallelBuffStorageMode.EntityPerStack;
        private bool _unlimited;
        private int _maxStack = 1;
        private float _duration = 1f;
        private float _tickTime = 1f;
        private ParallelBuffStackUpPolicy _stackUpPolicy = ParallelBuffStackUpPolicy.Append;
        private ParallelBuffStackDownPolicy _stackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest;
        private int _effectId;
        private string _effectNote = string.Empty;
        private bool _hasValidated;
        private bool _canCreate;
        private bool _configIdDuplicate;
        private bool _effectRegistered;
        private bool _effectRegistrationKnown;
        private bool _compressedEligible;
        private string _category = BuffAuthoringText.NotValidated;
        private string _targetAssetPath = string.Empty;
        private BuffCandidateGraphSummary _candidateSummary;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            BuffAuthoringHubWindow.OpenCreateBuff();
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
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField(BuffAuthoringText.CreateBuffTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                BuffAuthoringText.CreateBuffHelp,
                MessageType.Info);

            DrawBasicInfo();
            DrawBehavior();
            DrawEffect();
            DrawValidationPreview();
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBasicInfo()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.BasicInfo, EditorStyles.boldLabel);
            _configId = EditorGUILayout.IntField(BuffAuthoringText.ConfigId, _configId);
            _buffName = EditorGUILayout.TextField(BuffAuthoringText.BuffName, _buffName);
            EditorGUILayout.LabelField(BuffAuthoringText.Description);
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(48f));
            EditorGUILayout.LabelField(BuffAuthoringText.SavePath, BuffAssetRoot);
            EditorGUILayout.LabelField(BuffAuthoringText.TargetAsset, string.IsNullOrEmpty(_targetAssetPath) ? BuildTargetAssetPath() : _targetAssetPath);
        }

        private void DrawBehavior()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.Behavior, EditorStyles.boldLabel);
            _buffType = (BuffInstanceType)EditorGUILayout.EnumPopup(BuffAuthoringText.BuffType, _buffType);
            _triggerType = (BuffTriggerType)EditorGUILayout.EnumPopup(BuffAuthoringText.TriggerType, _triggerType);
            _parallelStorageMode = (ParallelBuffStorageMode)EditorGUILayout.EnumPopup(BuffAuthoringText.ParallelStorageMode, _parallelStorageMode);
            _unlimited = EditorGUILayout.Toggle(BuffAuthoringText.Unlimited, _unlimited);
            using (new EditorGUI.DisabledScope(_unlimited))
                _maxStack = EditorGUILayout.IntField(BuffAuthoringText.MaxStack, _maxStack);
            _duration = EditorGUILayout.FloatField(BuffAuthoringText.Duration, _duration);
            _tickTime = EditorGUILayout.FloatField(BuffAuthoringText.TickTime, _tickTime);
            _stackUpPolicy = (ParallelBuffStackUpPolicy)EditorGUILayout.EnumPopup(BuffAuthoringText.StackUpPolicy, _stackUpPolicy);
            _stackDownPolicy = (ParallelBuffStackDownPolicy)EditorGUILayout.EnumPopup(BuffAuthoringText.StackDownPolicy, _stackDownPolicy);
        }

        private void DrawEffect()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.Effect, EditorStyles.boldLabel);
            _effectId = EditorGUILayout.IntField(BuffAuthoringText.EffectId, _effectId);
            string effectState = !_effectRegistrationKnown ? BuffAuthoringText.Unknown : FormatBool(_effectRegistered);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectRegistered, effectState);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectNote);
            _effectNote = EditorGUILayout.TextArea(_effectNote, GUILayout.MinHeight(36f));
        }

        private void DrawValidationPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.ValidationPreview, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(BuffAuthoringText.CanCreate, _hasValidated ? FormatBool(_canCreate) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.ConfigIdDuplicate, _hasValidated ? FormatBool(_configIdDuplicate) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectRegistered, !_effectRegistrationKnown ? BuffAuthoringText.Unknown : FormatBool(_effectRegistered));
            EditorGUILayout.LabelField(BuffAuthoringText.CompressedEligibility, _hasValidated ? FormatBool(_compressedEligible) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.Category, _category);

            DrawMessageList(BuffAuthoringText.Errors, _errors, MessageType.Error);
            DrawMessageList(BuffAuthoringText.Warnings, _warnings, MessageType.Warning);
            DrawMessageList(BuffAuthoringText.Recommendations, _recommendations, MessageType.Info);
        }

        private static void DrawMessageList(string title, List<string> messages, MessageType type)
        {
            EditorGUILayout.LabelField(title);
            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox(BuffAuthoringText.None, MessageType.None);
                return;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < messages.Count; i++)
                builder.AppendLine(messages[i]);

            EditorGUILayout.HelpBox(builder.ToString(), type);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_candidateSummary == null || _candidateSummary.Graph == null))
                {
                    if (GUILayout.Button(BuffAuthoringText.ImportCreateBuffFromGraph, GUILayout.Height(28f)))
                        ImportFromCandidateGraph();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.Validate, GUILayout.Height(28f)))
                    Validate();

                if (GUILayout.Button(BuffAuthoringText.CreateDraftAsset, GUILayout.Height(28f)))
                    CreateDraftAsset();

                if (GUILayout.Button(BuffAuthoringText.OpenAuthoringValidator, GUILayout.Height(28f)))
                    BuffAuthoringHubWindow.OpenValidator();

                if (GUILayout.Button(BuffAuthoringText.CancelClose, GUILayout.Height(28f)))
                    Close();
            }
        }

        private void ImportFromCandidateGraph()
        {
            if (_candidateSummary == null || _candidateSummary.Graph == null)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.CreateBuffTitle, "请先在 Authoring Hub 顶部选择候选图。", "OK");
                return;
            }

            if (!BuffCandidateGraphBridge.TryBuildCreateBuffDraft(_candidateSummary.Graph, out BuffCandidateCreateBuffDraft draft, out string warning))
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.CreateBuffTitle, warning, "OK");
                return;
            }

            _configId = draft.ConfigId;
            _buffName = string.IsNullOrWhiteSpace(draft.BuffName) ? _buffName : draft.BuffName;
            _description = draft.Description ?? string.Empty;
            _buffType = draft.BuffType;
            _triggerType = draft.TriggerType;
            _parallelStorageMode = draft.ParallelStorageMode;
            _unlimited = draft.Unlimited;
            _maxStack = draft.MaxStack;
            _duration = draft.Duration;
            _tickTime = draft.TickTime;
            _stackUpPolicy = draft.StackUpPolicy;
            _stackDownPolicy = draft.StackDownPolicy;
            _effectId = draft.EffectId;

            Validate();

            if (!string.IsNullOrWhiteSpace(warning))
                EditorUtility.DisplayDialog(BuffAuthoringText.CreateBuffTitle, warning, "OK");
        }

        private void Validate()
        {
            _errors.Clear();
            _warnings.Clear();
            _recommendations.Clear();
            _targetAssetPath = BuildTargetAssetPath();

            List<BuffAssetSummary> summaries = BuffAuthoringValidationUtility.ScanBuffAssets();
            Dictionary<int, int> configIdIndex = BuffAuthoringValidationUtility.BuildConfigIdIndex(summaries);
            _configIdDuplicate = BuffAuthoringValidationUtility.IsConfigIdDuplicate(_configId, configIdIndex);

            EffectRegistryCheckResult registryCheck = BuffAuthoringValidationUtility.CheckProductionEffectRegistered(_effectId);
            _effectRegistered = registryCheck.IsRegistered;
            _effectRegistrationKnown = !registryCheck.IsUnknown;

            _compressedEligible = BuffAuthoringValidationUtility.ComputeCompressedEligibility(
                _buffType,
                _triggerType,
                _parallelStorageMode,
                _unlimited,
                _maxStack).IsEligible;

            if (_configId <= 0)
                _errors.Add("ConfigId 必须大于 0。");

            if (_configIdDuplicate)
                _errors.Add($"ConfigId 重复：当前 Resources Buff 目录中已经存在 ID={_configId}。");

            if (string.IsNullOrWhiteSpace(_buffName))
                _errors.Add("Buff Name 不能为空。");

            if (!IsUnderBuffRoot(BuffAssetRoot))
                _errors.Add($"Save Path 必须位于 {BuffAssetRoot}。");

            if (!_unlimited && _maxStack <= 0)
                _errors.Add("非 Unlimited Buff 的 MaxStack 必须大于 0。");

            if (_duration < 0f)
                _errors.Add("Duration 不能小于 0。");

            if (_tickTime < 0f)
                _errors.Add("TickTime 不能小于 0。");

            if (string.IsNullOrWhiteSpace(_targetAssetPath))
                _errors.Add("无法生成目标 asset 路径。");
            else if (AssetExists(_targetAssetPath))
                _errors.Add($"目标 asset 文件已经存在：{_targetAssetPath}。");

            AddWarnings(registryCheck.Status);
            AddRecommendations();

            _category = Classify();
            _canCreate = _errors.Count == 0;
            _hasValidated = true;
        }

        private void AddWarnings(string effectRegistryWarning)
        {
            if (_effectId <= 0)
                _warnings.Add("EffectId 未设置；草稿可以创建，但不能作为可运行 production Buff。");
            else if (!_effectRegistrationKnown)
                _warnings.Add($"无法稳定检查 Effect 注册状态：{effectRegistryWarning}");
            else if (!_effectRegistered)
                _warnings.Add($"EffectId {_effectId} 尚未注册到 production BuffEffectRegistry。");

            if (_parallelStorageMode == ParallelBuffStorageMode.CompressedExpiryFrameList && !_compressedEligible)
                _warnings.Add("已选择 CompressedExpiryFrameList，但当前字段不满足 compressed eligibility。");

            if (_unlimited)
                _warnings.Add("Unlimited=true 会阻止进入 compressed runtime。");

            if (!_unlimited && _maxStack > CompressedParallelBuffLayerBuffer.Capacity)
                _warnings.Add($"MaxStack={_maxStack} 超过 compressed capacity={CompressedParallelBuffLayerBuffer.Capacity}。");

            if (_triggerType == BuffTriggerType.EventTrigger)
                _warnings.Add("EventTrigger 当前按设计 fallback EntityPerStack，不应作为 compressed whitelist 候选。");

            if (BuffAuthoringValidationUtility.IsDebugOrSmoke(_configId, _buffName))
                _warnings.Add("Buff Name 包含 Debug / Smoke，建议只作为调试草稿，不作为正式玩法 Buff。");

            if (_duration == 0f)
                _warnings.Add("Duration=0，请确认是否为预期。");

            if (_duration > 0f && _tickTime > _duration)
                _warnings.Add("TickTime 大于 Duration，可能导致 Tick 触发次数少于预期。");
        }

        private void AddRecommendations()
        {
            if (_compressedEligible)
                _recommendations.Add("该配置满足 compressed eligibility，但进入 whitelist 前必须走真实候选审查流程。");
            else
                _recommendations.Add("该配置当前只能走 EntityPerStack 或尚不适合 compressed。");

            _recommendations.Add("创建后建议运行 Tools / BuffSystem / Authoring Validator。");

            if (_effectId <= 0 || !_effectRegistered)
                _recommendations.Add("Effect 未注册时，需要后续手动实现 Effect 并注册到 BuffEffectRegistryBootstrap。");

            if (_triggerType == BuffTriggerType.EventTrigger)
                _recommendations.Add("EventTrigger 当前按设计 fallback EntityPerStack。");
        }

        private void CreateDraftAsset()
        {
            Validate();

            if (_errors.Count > 0)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.CreateBuffTitle, "存在错误，已阻止创建。", "OK");
                return;
            }

            if (_warnings.Count > 0)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    BuffAuthoringText.CreateBuffTitle,
                    "当前配置存在 Warning。是否仍要创建草稿 asset？\n\n" + string.Join("\n", _warnings),
                    BuffAuthoringText.CreateDraftAsset,
                    "取消");

                if (!confirm)
                    return;
            }

            Directory.CreateDirectory(BuffAssetRoot);

            BuffConfigData asset = ScriptableObject.CreateInstance<BuffConfigData>();
            asset.ID = _configId;
            asset.Name = _buffName;
            asset.Description = _description;
            asset.BuffType = _buffType;
            asset.BuffTriggerType = _triggerType;
            asset.ParallelStorageMode = _parallelStorageMode;
            asset.Unlimited = _unlimited;
            asset.MaxStack = _unlimited ? 1 : _maxStack;
            asset.Duration = _duration;
            asset.TickTime = _tickTime;
            asset.ParallelStackUpPolicy = _stackUpPolicy;
            asset.ParallelStackDownPolicy = _stackDownPolicy;
            asset.EffectId = _effectId;

            AssetDatabase.CreateAsset(asset, _targetAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[BuffCreateWizard] Created draft BuffConfigData: {_targetAssetPath}");
            EditorUtility.DisplayDialog(
                BuffAuthoringText.CreateBuffTitle,
                $"创建成功：{_targetAssetPath}\n\n建议继续运行 Tools / BuffSystem / Authoring Validator。",
                "OK");
        }

        private string BuildTargetAssetPath()
        {
            string safeName = BuffAuthoringValidationUtility.MakeSafeFileName(_buffName, "Buff");
            return $"{BuffAssetRoot}/{_configId}_{safeName}.asset";
        }

        private string Classify()
        {
            if (_errors.Count > 0)
                return BuffAuthoringText.CategoryInvalid;

            if (BuffAuthoringValidationUtility.IsDebugOrSmoke(_configId, _buffName))
                return BuffAuthoringText.CategorySmokeDebugOnly;

            if (_compressedEligible && _effectRegistered)
                return BuffAuthoringText.CategoryEligibleCandidate;

            if (_buffType == BuffInstanceType.parallel && _triggerType == BuffTriggerType.Tick)
                return BuffAuthoringText.CategoryNearMiss;

            return BuffAuthoringText.CategoryNotCandidate;
        }

        private static string FormatBool(bool value)
        {
            return value ? BuffAuthoringText.True : BuffAuthoringText.False;
        }

        private static bool IsUnderBuffRoot(string path)
        {
            string normalizedPath = path.Replace('\\', '/');
            return normalizedPath.StartsWith(BuffAssetRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AssetExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                return true;

            string absolutePath = Path.GetFullPath(assetPath);
            return File.Exists(absolutePath);
        }
    }
}
