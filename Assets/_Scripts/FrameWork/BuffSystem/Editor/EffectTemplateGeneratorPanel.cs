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
    /// Effect 模板生成面板；只生成可编译的草稿代码，不自动注册 Effect。
    /// </summary>
    internal sealed class EffectTemplateGeneratorPanel
    {
        private const string EffectFolder = "Assets/_Scripts/FrameWork/BuffSystem/Effects";
        private const string BuffAssetRoot = "Assets/Resources/BuffSystem/Buff";
        private const string BuffSystemRoot = "Assets/_Scripts/FrameWork/BuffSystem";
        private const string DefaultNamespace = "BuffSystem";

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

        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _recommendations = new List<string>();
        private readonly List<string> _effectIdSourceHits = new List<string>();

        private Vector2 _scroll;
        private int _effectId;
        private string _className = "NewBuffEffect";
        private string _displayNote = string.Empty;
        private string _targetFolder = EffectFolder;
        private string _namespace = DefaultNamespace;
        private bool _onApply = true;
        private bool _onTick = true;
        private bool _onRemove = true;
        private bool _onRefresh;
        private bool _onStackChanged;
        private bool _hasValidated;
        private bool _canGenerate;
        private bool _registeredInProduction;
        private bool _productionRegistryKnown;
        private bool _usedByBuffConfig;
        private bool _effectIdConstFound;
        private bool _classNameValid;
        private bool _fileExists;
        private string _registryStatus = "Not checked";
        private string _targetFilePath = string.Empty;
        private BuffCandidateGraphSummary _candidateSummary;
        private BuffGraphEffectCodegenPlan _graphCodegenPlan;
        private bool _useGraphEffectCodegen;
        private bool _autoIdInitialized;
        private string _pendingAutoIdWarning = string.Empty;

        internal void OnGUI()
        {
            OnGUI(null);
        }

        internal void OnGUI(BuffCandidateGraphSummary candidateSummary)
        {
            _candidateSummary = candidateSummary;
            EnsureAutoEffectId();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField(BuffAuthoringText.EffectTemplateTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                BuffAuthoringText.EffectTemplateHelp,
                MessageType.Info);

            DrawBasicInfo();
            DrawCallbackSelection();
            DrawValidationPreview();
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBasicInfo()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.BasicInfo, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _effectId = EditorGUILayout.IntField(BuffAuthoringText.EffectId, _effectId);
            if (EditorGUI.EndChangeCheck())
            {
                _pendingAutoIdWarning = string.Empty;
                Validate();
            }

            if (GUILayout.Button(BuffAuthoringText.ReallocateEffectId, GUILayout.Height(24f)))
                ReallocateEffectId();

            _className = EditorGUILayout.TextField(BuffAuthoringText.EffectClassName, _className);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectDisplayNameNote);
            _displayNote = EditorGUILayout.TextArea(_displayNote, GUILayout.MinHeight(42f));
            _targetFolder = EditorGUILayout.TextField(BuffAuthoringText.TargetFolder, _targetFolder);
            _namespace = EditorGUILayout.TextField(BuffAuthoringText.Namespace, _namespace);
            EditorGUILayout.LabelField(BuffAuthoringText.TargetFile, string.IsNullOrWhiteSpace(_targetFilePath) ? BuildTargetFilePath() : _targetFilePath);
        }

        private void DrawCallbackSelection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.CallbackSelection, EditorStyles.boldLabel);
            _onApply = EditorGUILayout.Toggle("OnApply", _onApply);
            _onTick = EditorGUILayout.Toggle("OnTick", _onTick);
            _onRemove = EditorGUILayout.Toggle("OnRemove", _onRemove);
            _onRefresh = EditorGUILayout.Toggle("OnRefresh", _onRefresh);
            _onStackChanged = EditorGUILayout.Toggle("OnStackChanged", _onStackChanged);
            EditorGUILayout.HelpBox(BuffAuthoringText.EventEffectTemplateHelp, MessageType.Info);
        }

        private void DrawValidationPreview()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(BuffAuthoringText.ValidationPreview, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(BuffAuthoringText.CanGenerate, _hasValidated ? FormatBool(_canGenerate) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectRegistered, _hasValidated ? FormatBool(_registeredInProduction) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.ProductionRegistryStatus, _registryStatus);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectIdUsedByBuffConfigData, _hasValidated ? FormatBool(_usedByBuffConfig) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.EffectIdConstFoundInEffects, _hasValidated ? FormatBool(_effectIdConstFound) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.ClassNameValid, _hasValidated ? FormatBool(_classNameValid) : BuffAuthoringText.NotValidated);
            EditorGUILayout.LabelField(BuffAuthoringText.FileExists, _hasValidated ? FormatBool(_fileExists) : BuffAuthoringText.NotValidated);

            DrawMessageList(BuffAuthoringText.Errors, _errors, MessageType.Error);
            DrawMessageList(BuffAuthoringText.Warnings, _warnings, MessageType.Warning);
            DrawMessageList(BuffAuthoringText.Recommendations, _recommendations, MessageType.Info);
            DrawSourceHits();
            DrawGraphCodegenPreview();
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

        private void DrawSourceHits()
        {
            if (_effectIdSourceHits.Count == 0)
                return;

            EditorGUILayout.LabelField(BuffAuthoringText.EffectIdSourceHits);
            EditorGUILayout.TextArea(string.Join("\n", _effectIdSourceHits), GUILayout.MinHeight(48f));
        }

        private void DrawGraphCodegenPreview()
        {
            if (!_useGraphEffectCodegen || _graphCodegenPlan == null)
                return;

            EditorGUILayout.LabelField(BuffAuthoringText.GraphEffectCallChainPreview, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(BuffAuthoringText.GraphEffectSource, string.IsNullOrWhiteSpace(_graphCodegenPlan.SelectedEffectNodeSummary) ? "Legacy / Field Only" : _graphCodegenPlan.SelectedEffectNodeSummary);
            EditorGUILayout.TextArea(_graphCodegenPlan.BuildActionPreview(), GUILayout.MinHeight(96f));
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.Validate, GUILayout.Height(28f)))
                    Validate();

                if (GUILayout.Button(BuffAuthoringText.GenerateTemplate, GUILayout.Height(28f)))
                    GenerateTemplate();

                if (GUILayout.Button(BuffAuthoringText.CopyRegistrySnippet, GUILayout.Height(28f)))
                    CopyRegistrySnippet();

                if (GUILayout.Button(BuffAuthoringText.OpenEffectFolder, GUILayout.Height(28f)))
                    OpenEffectFolder();

                if (GUILayout.Button(BuffAuthoringText.Clear, GUILayout.Height(28f)))
                    Clear();
            }
        }

        private void ImportFromCandidateGraph()
        {
            if (_candidateSummary == null || _candidateSummary.Graph == null)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, "请先在 Authoring Hub 顶部选择候选图。", "OK");
                return;
            }

            if (!BuffCandidateGraphBridge.TryBuildEffectTemplateDraft(_candidateSummary.Graph, out BuffCandidateEffectTemplateDraft draft, out string warning))
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, warning, "OK");
                return;
            }

            _effectId = draft.EffectId;
            if (!string.IsNullOrWhiteSpace(draft.EffectClassName))
                _className = draft.EffectClassName;

            _displayNote = draft.Note ?? string.Empty;
            ApplyAutoEffectIdAfterImport();
            Validate();

            if (!string.IsNullOrWhiteSpace(warning))
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, warning, "OK");
        }

        private void ImportCallChainFromCandidateGraph()
        {
            if (_candidateSummary == null || _candidateSummary.Graph == null)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, "请先在 Authoring Hub 顶部选择候选图。", "OK");
                return;
            }

            _targetFilePath = BuildTargetFilePath();
            BuffGraphEffectCodegenPlan plan = BuildGraphCodegenPlan();
            _graphCodegenPlan = plan;
            _useGraphEffectCodegen = true;

            if (plan.EffectId > 0)
                _effectId = plan.EffectId;

            if (!string.IsNullOrWhiteSpace(plan.EffectClassName))
                _className = plan.EffectClassName;

            ApplyLifecycleSelectionFromPlan(plan);
            Validate();
            AppendGraphCodegenIssues(plan);
            _canGenerate = _errors.Count == 0;
            _hasValidated = true;

            string message = plan.HasErrors
                ? "调用链存在错误，已导入预览但会阻止生成。\n\n" + string.Join("\n", plan.Errors)
                : "已从候选图导入 Effect 调用链。\n\n" + plan.BuildActionPreview();
            EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, message, "OK");
        }

        private void Validate()
        {
            _errors.Clear();
            _warnings.Clear();
            _recommendations.Clear();
            _effectIdSourceHits.Clear();
            _targetFilePath = BuildTargetFilePath();

            _classNameValid = IsValidClassName(_className);
            _fileExists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_targetFilePath) != null || File.Exists(ToAbsolutePath(_targetFilePath));
            EffectRegistryCheckResult registryCheck = BuffAuthoringValidationUtility.CheckProductionEffectRegistered(_effectId);
            _registeredInProduction = registryCheck.IsRegistered;
            _productionRegistryKnown = !registryCheck.IsUnknown;
            _registryStatus = registryCheck.Status;

            List<BuffAssetSummary> buffSummaries = BuffAuthoringValidationUtility.ScanBuffAssets();
            List<BuffAssetSummary> buffHits = BuffAuthoringValidationUtility.GetBuffConfigEffectHits(_effectId, buffSummaries);
            _usedByBuffConfig = buffHits.Count > 0;
            for (int i = 0; i < buffHits.Count; i++)
                _effectIdSourceHits.Add($"BuffConfigData: {buffHits[i].ConfigId} - {buffHits[i].Name} ({buffHits[i].AssetPath})");

            List<EffectIdConstantHit> constHits = BuffAuthoringValidationUtility.ScanEffectIdConstants(EffectFolder, _effectId);
            _effectIdConstFound = constHits.Count > 0;
            for (int i = 0; i < constHits.Count; i++)
                _effectIdSourceHits.Add($"Effect const: {constHits[i].EffectId} ({constHits[i].FilePath})");

            BuffAuthoringIdValidationResult idValidation = BuffAuthoringIdService.ValidateEffectId(_effectId, BuffAuthoringHubSettings.Load());
            _errors.AddRange(idValidation.Errors);
            _warnings.AddRange(idValidation.Warnings);

            if (string.IsNullOrWhiteSpace(_className))
                _errors.Add("Effect Class Name 不能为空。");

            if (!_classNameValid)
                _errors.Add("Effect Class Name 必须是合法 C# 类型名。");

            if (!string.IsNullOrWhiteSpace(_className) && !_className.EndsWith("Effect", StringComparison.Ordinal))
                _errors.Add("Effect Class Name 必须以 Effect 结尾。");

            if (_fileExists)
                _errors.Add($"目标 .cs 文件已存在：{_targetFilePath}。");

            if (!IsUnderBuffSystemRoot(_targetFolder))
                _errors.Add($"Target Folder 必须位于 {BuffSystemRoot} 下。");

            if (_registeredInProduction)
                _errors.Add("EffectId 已注册到 production registry，禁止生成重复模板。");

            AddWarnings();
            AddRecommendations();

            _canGenerate = _errors.Count == 0;
            _hasValidated = true;
        }

        private void AddWarnings()
        {
            if (!_productionRegistryKnown)
                _warnings.Add($"无法完整检查 production registry：{_registryStatus}");

            if (_effectId > 0 && !_usedByBuffConfig)
                _warnings.Add("EffectId 未被任何 BuffConfigData 使用；请确认这是预期草稿。");

            if (_effectIdConstFound)
                _warnings.Add("EffectId 已在 Effects 目录的 .cs 常量中出现；请确认不会冲突。");

            if (BuffAuthoringValidationUtility.IsDebugOrSmoke(0, _className))
                _warnings.Add("类名包含 Debug / Smoke，建议只作为调试或临时模板。");

            if (!PathsEqual(_targetFolder, EffectFolder))
                _warnings.Add($"Target Folder 不是默认推荐路径：{EffectFolder}。");

            if (!HasAnyCallback())
                _warnings.Add("未选择任何 callback，将生成空 Effect 类。");

            if (!string.IsNullOrWhiteSpace(_pendingAutoIdWarning))
                _warnings.Add(_pendingAutoIdWarning);
        }

        private void AddRecommendations()
        {
            if (_useGraphEffectCodegen)
                _recommendations.Add("生成的 Effect 会调用图中有效 ScriptActionNode；具体玩法逻辑仍需要在 Action 脚本的 Execute(in context) 中实现。");
            else
                _recommendations.Add("普通 Effect 模板需要用户手写生命周期逻辑；如果希望由图生成调用链，请先从候选图导入 Effect 调用链。");
            _recommendations.Add("生成后需要手动注册到 BuffEffectRegistryBootstrap。");
            _recommendations.Add("注册后建议运行 BuffAuthoringValidator。");
            _recommendations.Add("真实 Buff 进入 whitelist 前仍需候选审查。");
            _recommendations.Add("EventTrigger 当前不进入 compressed whitelist。");
            if (_useGraphEffectCodegen)
                _recommendations.Add("Graph 调用链只生成 Effect 草稿代码，不会自动注册 Effect 或加入 whitelist。");
        }

        private void GenerateTemplate()
        {
            BuffAuthoringPreflightResult preflightResult = RunPreflightBeforeGenerate();

            if (preflightResult.HasError || _errors.Count > 0)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, "Preflight 存在错误，已阻止生成模板。\n\n" + BuildGenerationBlockMessage(preflightResult), "OK");
                return;
            }

            try
            {
                Directory.CreateDirectory(_targetFolder);
                string source = BuildTemplate();
                if (_useGraphEffectCodegen && _graphCodegenPlan != null)
                {
                    AppendGraphCodegenIssues(_graphCodegenPlan);
                    if (_errors.Count > 0)
                    {
                        EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, "Graph codegen 自检失败，已阻止生成模板。\n\n" + BuildGenerationBlockMessage(preflightResult), "OK");
                        return;
                    }
                }

                File.WriteAllText(ToAbsolutePath(_targetFilePath), source, Encoding.UTF8);
                AssetDatabase.Refresh();
                UnityEngine.Object generatedAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_targetFilePath);
                Selection.activeObject = generatedAsset;
                if (generatedAsset != null)
                    EditorGUIUtility.PingObject(generatedAsset);

                string registryMessage = WriteGeneratedEffectRegistryEntry();
                string bootstrapMessage = TryAutoRegisterEffectToBootstrap(!registryMessage.StartsWith("Warning"));
                Debug.Log($"[EffectTemplateGenerator] Generated effect template: {_targetFilePath}");
                EditorUtility.DisplayDialog(
                    BuffAuthoringText.EffectTemplateTitle,
                    $"模板已生成：{_targetFilePath}\n\n{registryMessage}\n\n{bootstrapMessage}\n\nEffect 生命周期调用链已由工具生成；请在 Action 脚本 Execute(in context) 中实现玩法逻辑，并等待 Unity 编译完成。",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EffectTemplateGenerator] 无法创建 Effect 模板：{exception.Message}");
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, $"无法创建模板：{exception.Message}", "OK");
            }
        }

        private BuffAuthoringPreflightResult RunPreflightBeforeGenerate()
        {
            BuffAuthoringEffectPreflightDraft draft = new BuffAuthoringEffectPreflightDraft
            {
                EffectId = _effectId,
                EffectClassName = _className,
                TargetFolder = _targetFolder,
                Namespace = _namespace,
                OnApply = _onApply,
                OnTick = _onTick,
                OnRemove = _onRemove,
                OnRefresh = _onRefresh,
                OnStackChanged = _onStackChanged
            };

            BuffAuthoringPreflightResult result = BuffAuthoringPreflightValidator.RunEffectPreflight(draft, BuffAuthoringHubSettings.Load());
            ApplyPreflightDraft(draft);
            Validate();
            AppendPreflightIssues(result);
            if (_useGraphEffectCodegen)
            {
                _graphCodegenPlan = BuildGraphCodegenPlan();
                ApplyLifecycleSelectionFromPlan(_graphCodegenPlan);
                AppendGraphCodegenIssues(_graphCodegenPlan);
            }

            _canGenerate = !result.HasError && _errors.Count == 0;
            _hasValidated = true;
            return result;
        }

        private void ApplyPreflightDraft(BuffAuthoringEffectPreflightDraft draft)
        {
            _effectId = draft.EffectId;
            _className = draft.EffectClassName;
            _targetFolder = draft.TargetFolder;
            _namespace = draft.Namespace;
            _targetFilePath = draft.TargetFilePath;
        }

        private void AppendPreflightIssues(BuffAuthoringPreflightResult result)
        {
            for (int i = 0; i < result.Issues.Count; i++)
            {
                BuffAuthoringPreflightIssue issue = result.Issues[i];
                string message = string.IsNullOrWhiteSpace(issue.Code)
                    ? issue.Message
                    : $"{issue.Code}: {issue.Message}";

                if (issue.Severity == BuffAuthoringPreflightSeverity.Error)
                    _errors.Add(message);
                else if (issue.Severity == BuffAuthoringPreflightSeverity.Warning)
                    _warnings.Add(message);
                else
                    _recommendations.Add(message);
            }
        }

        private void AppendGraphCodegenIssues(BuffGraphEffectCodegenPlan plan)
        {
            if (plan == null)
                return;

            for (int i = 0; i < plan.Errors.Count; i++)
                _errors.Add("GRAPH_CODEGEN: " + plan.Errors[i]);

            for (int i = 0; i < plan.Warnings.Count; i++)
                _warnings.Add("GRAPH_CODEGEN: " + plan.Warnings[i]);

            if (!plan.HasErrors)
                _recommendations.Add("GRAPH_CODEGEN: 调用链 Preflight 通过，将按候选图生成 action 字段与 Execute(in context) 调用。");
        }

        private string BuildGenerationBlockMessage(BuffAuthoringPreflightResult preflightResult)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(preflightResult.ToDisplayText());

            if (_errors.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("当前错误：");
                for (int i = 0; i < _errors.Count; i++)
                    builder.AppendLine("- " + _errors[i]);
            }

            return builder.ToString();
        }

        private string WriteGeneratedEffectRegistryEntry()
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            string effectName = string.IsNullOrWhiteSpace(_displayNote) ? _className : _displayNote.Trim();
            bool success = BuffAuthoringIdRegistryAllocator.UpsertGeneratedEffectEntry(
                settings.IdRegistryJsonPath,
                _effectId,
                effectName,
                _className,
                _candidateSummary != null ? _candidateSummary.Graph : null,
                _targetFilePath,
                out string error);

            if (success)
                return $"ID Registry 已更新：{settings.IdRegistryJsonPath}";

            Debug.LogWarning($"[EffectTemplateGenerator] Effect 模板已生成，但 ID Registry 写入失败：{error}");
            return $"Warning：Effect 模板已生成，但 ID Registry 写入失败，请检查路径。\n{error}";
        }

        private string TryAutoRegisterEffectToBootstrap(bool idRegistrySucceeded)
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            string snippet = BuildRegistrySnippet();
            if (!settings.AutoRegisterEffectsToBootstrap)
                return "Bootstrap 自动注册已关闭，请按需手动注册：\n" + snippet;

            if (!idRegistrySucceeded)
                return "ID Registry 写入未成功，已跳过 Bootstrap 自动注册。\n可手动注册片段：\n" + snippet;

            bool success = BuffEffectBootstrapAutoRegistryPatcher.TryUpsertAutoRegistration(
                _effectId,
                _className,
                out BuffEffectBootstrapAutoRegistryReport report);

            string message = report.ToDisplayText();
            if (success)
            {
                Debug.Log("[EffectTemplateGenerator] " + message);
                return message;
            }

            Debug.LogWarning("[EffectTemplateGenerator] Bootstrap 自动注册失败：\n" + message);
            return message + "\n可手动注册片段：\n" + snippet;
        }

        private void CopyRegistrySnippet()
        {
            string snippet = BuildRegistrySnippet();
            EditorGUIUtility.systemCopyBuffer = snippet;
            Debug.Log($"[EffectTemplateGenerator] Registry snippet copied: {snippet}");
            EditorUtility.DisplayDialog(
                BuffAuthoringText.EffectTemplateTitle,
                $"已复制注册片段：\n\n{snippet}\n\n请手动加入 BuffEffectRegistryBootstrap.RegisterProductionEffects(...)。",
                "OK");
        }

        private string BuildRegistrySnippet()
        {
            return $"registry.Register({_effectId}, new {_className}());";
        }

        private void OpenEffectFolder()
        {
            Directory.CreateDirectory(_targetFolder);
            EditorUtility.RevealInFinder(_targetFolder);
        }

        private void Clear()
        {
            _effectId = 0;
            _className = "NewBuffEffect";
            _displayNote = string.Empty;
            _targetFolder = EffectFolder;
            _namespace = DefaultNamespace;
            _onApply = true;
            _onTick = true;
            _onRemove = true;
            _onRefresh = false;
            _onStackChanged = false;
            _hasValidated = false;
            _canGenerate = false;
            _registeredInProduction = false;
            _productionRegistryKnown = false;
            _usedByBuffConfig = false;
            _effectIdConstFound = false;
            _classNameValid = false;
            _fileExists = false;
            _registryStatus = "Not checked";
            _targetFilePath = string.Empty;
            _graphCodegenPlan = null;
            _useGraphEffectCodegen = false;
            _pendingAutoIdWarning = string.Empty;
            _autoIdInitialized = false;
            EnsureAutoEffectId();
            _errors.Clear();
            _warnings.Clear();
            _recommendations.Clear();
            _effectIdSourceHits.Clear();
        }

        private string BuildTemplate()
        {
            string classSummary = string.IsNullOrWhiteSpace(_displayNote)
                ? "TODO: Fill effect description."
                : _displayNote.Trim();
            if (_useGraphEffectCodegen && _graphCodegenPlan != null)
            {
                _graphCodegenPlan.EffectId = _effectId;
                _graphCodegenPlan.EffectClassName = _className;
                _graphCodegenPlan.Namespace = _namespace;
                _graphCodegenPlan.TargetFolder = _targetFolder;
                _graphCodegenPlan.TargetFilePath = _targetFilePath;
                return BuffGraphEffectCodegenEmitter.Emit(_graphCodegenPlan, classSummary);
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"namespace {_namespace}");
            builder.AppendLine("{");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine($"    /// {classSummary}");
            builder.AppendLine("    /// Generated by Buff Authoring Hub.");
            builder.AppendLine("    /// Note: manually register this effect in BuffEffectRegistryBootstrap before production use.");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine($"    internal sealed class {_className} : BuffEffectExecutorBase");
            builder.AppendLine("    {");
            builder.AppendLine($"        internal const int EffectId = {_effectId};");

            if (_onApply)
                AppendOnApply(builder);

            if (_onTick)
                AppendOnTick(builder);

            if (_onRemove)
                AppendOnRemove(builder);

            if (_onRefresh)
                AppendOnRefresh(builder);

            if (_onStackChanged)
                AppendOnStackChanged(builder);

            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendOnApply(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        public override void OnApply(in BuffEffectContext context)");
            builder.AppendLine("        {");
            builder.AppendLine("            // TODO: Apply ECS state changes here.");
            builder.AppendLine("            // Do not directly depend on View or Unity object components.");
            builder.AppendLine("        }");
        }

        private static void AppendOnTick(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        public override void OnTick(in BuffEffectContext context)");
            builder.AppendLine("        {");
            builder.AppendLine("            // TODO: Implement tick logic here.");
            builder.AppendLine("            // Do not use Unity frame-time APIs as Buff runtime logic time.");
            builder.AppendLine("            // Prefer frame-based data from Buff runtime / SimulationContext where available.");
            builder.AppendLine("        }");
        }

        private static void AppendOnRemove(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        public override void OnRemove(in BuffEffectContext context)");
            builder.AppendLine("        {");
            builder.AppendLine("            // TODO: Clean up ECS state changes here.");
            builder.AppendLine("        }");
        }

        private static void AppendOnRefresh(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        public override void OnRefresh(in BuffEffectContext context)");
            builder.AppendLine("        {");
            builder.AppendLine("            // TODO: Refresh ECS state here.");
            builder.AppendLine("        }");
        }

        private static void AppendOnStackChanged(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("        public override void OnStackChanged(in BuffEffectContext context, int delta)");
            builder.AppendLine("        {");
            builder.AppendLine("            // TODO: React to stack changes here.");
            builder.AppendLine("        }");
        }

        private string BuildTargetFilePath()
        {
            string safeClassName = string.IsNullOrWhiteSpace(_className) ? "NewBuffEffect" : _className.Trim();
            return $"{_targetFolder.TrimEnd('/', '\\')}/{safeClassName}.cs";
        }

        private bool HasAnyCallback()
        {
            return _onApply || _onTick || _onRemove || _onRefresh || _onStackChanged;
        }

        private static string FormatBool(bool value)
        {
            return value ? BuffAuthoringText.True : BuffAuthoringText.False;
        }

        private static bool IsValidClassName(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return false;

            string trimmed = className.Trim();
            if (CSharpKeywords.Contains(trimmed))
                return false;

            if (!IsIdentifierStart(trimmed[0]))
                return false;

            for (int i = 1; i < trimmed.Length; i++)
            {
                if (!IsIdentifierPart(trimmed[i]))
                    return false;
            }

            return true;
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        private static bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static bool IsUnderBuffSystemRoot(string path)
        {
            string normalizedPath = NormalizeAssetPath(path);
            return normalizedPath.StartsWith(BuffSystemRoot + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedPath, BuffSystemRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(NormalizeAssetPath(left).TrimEnd('/'), NormalizeAssetPath(right).TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(NormalizeAssetPath(assetPath));
        }

        private static string NormalizeAssetPath(string path)
        {
            return BuffAuthoringValidationUtility.NormalizeAssetPath(path);
        }

        private void EnsureAutoEffectId()
        {
            if (_autoIdInitialized)
                return;

            _autoIdInitialized = true;
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            if (!settings.AutoAllocateIds)
                return;

            if (_effectId <= 0)
                _effectId = BuffAuthoringIdService.GetNextAvailableEffectId(settings);
        }

        private void ApplyAutoEffectIdAfterImport()
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            if (!settings.AutoAllocateIds)
                return;

            if (!BuffAuthoringIdService.ShouldReplaceEffectId(_effectId, settings))
                return;

            int oldId = _effectId;
            _effectId = BuffAuthoringIdService.GetNextAvailableEffectId(settings);
            _pendingAutoIdWarning = $"候选图 EffectId={oldId} 缺失或冲突，已自动替换为 {_effectId}。";
        }

        private void ReallocateEffectId()
        {
            _effectId = BuffAuthoringIdService.GetNextAvailableEffectId(BuffAuthoringHubSettings.Load());
            _pendingAutoIdWarning = string.Empty;
            Validate();
        }

        private BuffGraphEffectCodegenPlan BuildGraphCodegenPlan()
        {
            BuffGraphEffectCodegenRequest request = new BuffGraphEffectCodegenRequest
            {
                EffectId = _effectId,
                EffectClassName = _className,
                Namespace = _namespace,
                TargetFolder = _targetFolder,
                TargetFilePath = string.IsNullOrWhiteSpace(_targetFilePath) ? BuildTargetFilePath() : _targetFilePath,
                OnApply = _onApply,
                OnTick = _onTick,
                OnRemove = _onRemove,
                OnRefresh = _onRefresh,
                OnStackChanged = _onStackChanged
            };

            BuffGraphEffectCodegenBuilder.TryBuild(
                _candidateSummary != null ? _candidateSummary.Graph : null,
                request,
                out BuffGraphEffectCodegenPlan plan);
            return plan;
        }

        private void ApplyLifecycleSelectionFromPlan(BuffGraphEffectCodegenPlan plan)
        {
            if (plan == null)
                return;

            for (int i = 0; i < plan.LifecyclePlans.Count; i++)
            {
                BuffGraphEffectLifecyclePlan lifecycle = plan.LifecyclePlans[i];
                if (!lifecycle.IncludeOverride)
                    continue;

                if (lifecycle.LifecycleName == "OnApply")
                    _onApply = true;
                else if (lifecycle.LifecycleName == "OnTick")
                    _onTick = true;
                else if (lifecycle.LifecycleName == "OnRemove")
                    _onRemove = true;
                else if (lifecycle.LifecycleName == "OnRefresh")
                    _onRefresh = true;
                else if (lifecycle.LifecycleName == "OnStackChanged")
                    _onStackChanged = true;
            }
        }
    }
}
