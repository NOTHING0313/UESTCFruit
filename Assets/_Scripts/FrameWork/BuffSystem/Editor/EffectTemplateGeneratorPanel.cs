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

        internal void OnGUI()
        {
            OnGUI(null);
        }

        internal void OnGUI(BuffCandidateGraphSummary candidateSummary)
        {
            _candidateSummary = candidateSummary;
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
            _effectId = EditorGUILayout.IntField(BuffAuthoringText.EffectId, _effectId);
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

        private void DrawActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_candidateSummary == null || _candidateSummary.Graph == null))
                {
                    if (GUILayout.Button(BuffAuthoringText.ImportEffectFromGraph, GUILayout.Height(28f)))
                        ImportFromCandidateGraph();
                }
            }

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
            Validate();

            if (!string.IsNullOrWhiteSpace(warning))
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, warning, "OK");
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

            if (_effectId <= 0)
                _errors.Add("EffectId 必须大于 0。");

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

            if (LooksLikeDebugEffectId(_effectId))
                _warnings.Add("EffectId 看起来属于 Debug / Smoke 区间，请确认不是正式 gameplay Effect。");

            if (BuffAuthoringValidationUtility.IsDebugOrSmoke(0, _className))
                _warnings.Add("类名包含 Debug / Smoke，建议只作为调试或临时模板。");

            if (!PathsEqual(_targetFolder, EffectFolder))
                _warnings.Add($"Target Folder 不是默认推荐路径：{EffectFolder}。");

            if (!HasAnyCallback())
                _warnings.Add("未选择任何 callback，将生成空 Effect 类。");
        }

        private void AddRecommendations()
        {
            _recommendations.Add("生成后需要手动实现 Effect 逻辑。");
            _recommendations.Add("生成后需要手动注册到 BuffEffectRegistryBootstrap。");
            _recommendations.Add("注册后建议运行 BuffAuthoringValidator。");
            _recommendations.Add("真实 Buff 进入 whitelist 前仍需候选审查。");
            _recommendations.Add("EventTrigger 当前不进入 compressed whitelist。");
        }

        private void GenerateTemplate()
        {
            Validate();

            if (_errors.Count > 0)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, "存在错误，已阻止生成模板。", "OK");
                return;
            }

            if (_warnings.Count > 0)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    BuffAuthoringText.EffectTemplateTitle,
                    "当前模板存在 Warning。是否仍要生成 .cs 草稿？\n\n" + string.Join("\n", _warnings),
                    BuffAuthoringText.GenerateTemplate,
                    "取消");

                if (!confirm)
                    return;
            }

            try
            {
                Directory.CreateDirectory(_targetFolder);
                File.WriteAllText(ToAbsolutePath(_targetFilePath), BuildTemplate(), Encoding.UTF8);
                AssetDatabase.Refresh();
                UnityEngine.Object generatedAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_targetFilePath);
                Selection.activeObject = generatedAsset;
                if (generatedAsset != null)
                    EditorGUIUtility.PingObject(generatedAsset);

                Debug.Log($"[EffectTemplateGenerator] Generated effect template: {_targetFilePath}");
                EditorUtility.DisplayDialog(
                    BuffAuthoringText.EffectTemplateTitle,
                    $"模板已生成：{_targetFilePath}\n\n请手动实现逻辑，并按需注册到 BuffEffectRegistryBootstrap。",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EffectTemplateGenerator] 无法创建 Effect 模板：{exception.Message}");
                EditorUtility.DisplayDialog(BuffAuthoringText.EffectTemplateTitle, $"无法创建模板：{exception.Message}", "OK");
            }
        }

        private void CopyRegistrySnippet()
        {
            string snippet = $"registry.Register({_effectId}, new {_className}());";
            EditorGUIUtility.systemCopyBuffer = snippet;
            Debug.Log($"[EffectTemplateGenerator] Registry snippet copied: {snippet}");
            EditorUtility.DisplayDialog(
                BuffAuthoringText.EffectTemplateTitle,
                $"已复制注册片段：\n\n{snippet}\n\n请手动加入 BuffEffectRegistryBootstrap.RegisterProductionEffects(...)。",
                "OK");
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

        private static bool LooksLikeDebugEffectId(int effectId)
        {
            return effectId >= 990000;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(NormalizeAssetPath(assetPath));
        }

        private static string NormalizeAssetPath(string path)
        {
            return BuffAuthoringValidationUtility.NormalizeAssetPath(path);
        }
    }
}
