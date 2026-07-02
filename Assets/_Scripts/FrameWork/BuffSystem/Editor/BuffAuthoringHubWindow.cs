using UnityEditor;
using UnityEngine;
using BuffSystem.Editor.AuthoringGraphs;

namespace BuffSystem
{
    /// <summary>
    /// Buff 制作工具统一入口；只组织 Editor UI，不修改运行时、白名单或资源配置。
    /// </summary>
    public sealed class BuffAuthoringHubWindow : EditorWindow
    {
        private const string MenuPath = "Tools/BuffSystem/Authoring Hub";
        private const int CompositePreviewMaxLines = 200;

        private static readonly string[] TabLabels =
        {
            BuffAuthoringText.ValidatorTab,
            BuffAuthoringText.CreateBuffTab,
            BuffAuthoringText.EffectTemplateTab
        };

        private static readonly string[] ModeLabels =
        {
            BuffAuthoringText.NumericMode,
            BuffAuthoringText.GraphMode,
            BuffAuthoringText.SettingsMode
        };

        private AuthoringHubMode _mode;
        private Tab _tab;
        private BuffAuthoringValidatorWindow _validatorWindow;
        private BuffCreateWizardWindow _createWizardWindow;
        private EffectTemplateGeneratorPanel _effectTemplatePanel;
        private BuffCandidateGraph _candidateGraph;
        private BuffCandidateGraphSummary _candidateSummary;
        private BuffGraphGeneratePlan _graphGeneratePlan;
        private BuffGraphGenerateReport _graphGenerateReport;
        private BuffGraphCompositeEffectPlan _compositePreviewPlan;
        private string _compositePreviewCode = string.Empty;
        private string _compositePreviewError = string.Empty;
        private string _compositePreviewState = BuffAuthoringText.CompositePreviewNotRun;
        private Vector2 _compositePreviewScroll;
        private BuffAuthoringHubSettingsData _settings;
        private BuffAuthoringIdRegistryScanReport _idRegistryReport;

        private enum AuthoringHubMode
        {
            Numeric = 0,
            Graph = 1,
            Settings = 2
        }

        private enum Tab
        {
            Validator = 0,
            CreateBuff = 1,
            EffectTemplate = 2
        }

        private enum GraphGenerateMode
        {
            EffectOnly,
            BuffOnly,
            BuffAndEffect
        }

        [MenuItem(MenuPath)]
        private static void OpenHub()
        {
            Open(Tab.Validator);
        }

        internal static void OpenValidator()
        {
            Open(Tab.Validator);
        }

        internal static void OpenCreateBuff()
        {
            Open(Tab.CreateBuff);
        }

        private static void Open(Tab tab)
        {
            BuffAuthoringHubWindow window = GetWindow<BuffAuthoringHubWindow>(BuffAuthoringText.HubTitle);
            window.minSize = new Vector2(900f, 620f);
            window._mode = AuthoringHubMode.Numeric;
            window._tab = tab;
            window.EnsurePanels();
            window.Show();
        }

        private void OnEnable()
        {
            _settings = BuffAuthoringHubSettings.Load();
            EnsurePanels();
        }

        private void OnDisable()
        {
            DestroyPanel(_validatorWindow);
            DestroyPanel(_createWizardWindow);
            _validatorWindow = null;
            _createWizardWindow = null;
        }

        private void OnGUI()
        {
            EnsurePanels();

            EditorGUILayout.LabelField(BuffAuthoringText.HubTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                BuffAuthoringText.HubHelp,
                MessageType.Info);

            _mode = (AuthoringHubMode)GUILayout.Toolbar((int)_mode, ModeLabels, GUILayout.Height(30f));
            EditorGUILayout.Space(8f);

            switch (_mode)
            {
                case AuthoringHubMode.Numeric:
                    DrawNumericMode();
                    break;
                case AuthoringHubMode.Graph:
                    DrawGraphMode();
                    break;
                case AuthoringHubMode.Settings:
                    DrawSettingsMode();
                    break;
            }
        }

        private void DrawNumericMode()
        {
            EditorGUILayout.HelpBox(BuffAuthoringText.NumericModeHelp, MessageType.Info);

            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabLabels, GUILayout.Height(28f));
            EditorGUILayout.Space(8f);

            switch (_tab)
            {
                case Tab.Validator:
                    _validatorWindow.DrawEmbedded(_candidateSummary);
                    break;
                case Tab.CreateBuff:
                    _createWizardWindow.DrawEmbedded(_candidateSummary);
                    break;
                case Tab.EffectTemplate:
                    _effectTemplatePanel.OnGUI(_candidateSummary);
                    break;
            }
        }

        private void DrawGraphMode()
        {
            EditorGUILayout.HelpBox(BuffAuthoringText.GraphModeHelp, MessageType.Info);
            DrawCandidateGraphLink();
        }

        private void DrawSettingsMode()
        {
            if (_settings == null)
                _settings = BuffAuthoringHubSettings.Load();

            EditorGUILayout.LabelField(BuffAuthoringText.SettingsTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(BuffAuthoringText.SettingsHelp, MessageType.Info);

            _settings.GraphDefaultFolder = EditorGUILayout.TextField(BuffAuthoringText.GraphDefaultFolder, _settings.GraphDefaultFolder);
            DrawPathState(_settings.GraphDefaultFolder, true, true);

            _settings.BuffConfigDataDefaultFolder = EditorGUILayout.TextField(BuffAuthoringText.BuffConfigDataDefaultFolder, _settings.BuffConfigDataDefaultFolder);
            DrawPathState(_settings.BuffConfigDataDefaultFolder, true, false);

            _settings.EffectScriptDefaultFolder = EditorGUILayout.TextField(BuffAuthoringText.EffectScriptDefaultFolder, _settings.EffectScriptDefaultFolder);
            DrawPathState(_settings.EffectScriptDefaultFolder, true, false);

            _settings.IdRegistryJsonPath = EditorGUILayout.TextField(BuffAuthoringText.IdRegistryJsonPath, _settings.IdRegistryJsonPath);
            DrawPathState(_settings.IdRegistryJsonPath, false, false);
            _settings.AutoAllocateIds = EditorGUILayout.Toggle(BuffAuthoringText.AutoAllocateIds, _settings.AutoAllocateIds);
            EditorGUILayout.HelpBox(BuffAuthoringText.AutoAllocateIdsHelp, MessageType.Info);
            _settings.AutoRegisterEffectsToBootstrap = EditorGUILayout.Toggle(BuffAuthoringText.AutoRegisterEffectsToBootstrap, _settings.AutoRegisterEffectsToBootstrap);
            EditorGUILayout.HelpBox(BuffAuthoringText.AutoRegisterEffectsToBootstrapHelp, MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.SaveSettings, GUILayout.Height(28f)))
                {
                    BuffAuthoringHubSettings.Save(_settings);
                    EditorUtility.DisplayDialog(BuffAuthoringText.SettingsTitle, BuffAuthoringText.SettingsSaved, "OK");
                }

                if (GUILayout.Button(BuffAuthoringText.ResetDefaults, GUILayout.Height(28f)))
                {
                    _settings = BuffAuthoringHubSettings.ResetToDefaults();
                    EditorUtility.DisplayDialog(BuffAuthoringText.SettingsTitle, BuffAuthoringText.SettingsReset, "OK");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.OpenGraphFolder, GUILayout.Height(26f)))
                    RevealFolderIfExists(_settings.GraphDefaultFolder);

                if (GUILayout.Button(BuffAuthoringText.OpenBuffFolder, GUILayout.Height(26f)))
                    RevealFolderIfExists(_settings.BuffConfigDataDefaultFolder);

                if (GUILayout.Button(BuffAuthoringText.OpenEffectFolderInSettings, GUILayout.Height(26f)))
                    RevealFolderIfExists(_settings.EffectScriptDefaultFolder);
            }

            DrawIdRegistryReadOnlyCheck();
        }

        private void DrawIdRegistryReadOnlyCheck()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(BuffAuthoringText.IdRegistryReadOnlyCheck, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(BuffAuthoringText.IdRegistryReadOnlyHelp, MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.ScanIdUsage, GUILayout.Height(26f)))
                    _idRegistryReport = BuffAuthoringIdRegistryScanner.Scan(_settings);

                using (new EditorGUI.DisabledScope(_idRegistryReport == null))
                {
                    if (GUILayout.Button(BuffAuthoringText.CopyIdRegistryReport, GUILayout.Height(26f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _idRegistryReport.ToPlainText();
                        EditorUtility.DisplayDialog(BuffAuthoringText.IdRegistryReadOnlyCheck, BuffAuthoringText.IdRegistryReportCopied, "OK");
                    }
                }
            }

            if (_idRegistryReport == null)
            {
                EditorGUILayout.HelpBox(BuffAuthoringText.IdRegistryNotScanned, MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.RegistryPath, _idRegistryReport.RegistryPath);
                DrawSummaryField(BuffAuthoringText.RegistryExists, FormatBool(_idRegistryReport.RegistryExists));
                DrawSummaryField(BuffAuthoringText.RegistryParseSucceeded, FormatBool(_idRegistryReport.RegistryParseSucceeded));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.RecommendedNextBuffConfigId, _idRegistryReport.RecommendedNextBuffConfigId.ToString());
                DrawSummaryField(BuffAuthoringText.RecommendedNextEffectId, _idRegistryReport.RecommendedNextEffectId.ToString());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.ScannedBuffIdCount, _idRegistryReport.BuffEntries.Count.ToString());
                DrawSummaryField(BuffAuthoringText.ScannedEffectIdCount, _idRegistryReport.EffectEntries.Count.ToString());
                DrawSummaryField(BuffAuthoringText.ErrorCount, _idRegistryReport.Errors.Count.ToString());
                DrawSummaryField(BuffAuthoringText.WarningCount, _idRegistryReport.Warnings.Count.ToString());
            }

            DrawTextArea(BuffAuthoringText.Errors, JoinLines(_idRegistryReport.Errors));
            DrawTextArea(BuffAuthoringText.Warnings, JoinLines(_idRegistryReport.Warnings));
            DrawTextArea(BuffAuthoringText.Recommendations, JoinLines(_idRegistryReport.Infos));
            DrawTextArea(BuffAuthoringText.BuffIdUsage, FormatIdEntries(_idRegistryReport.BuffEntries));
            DrawTextArea(BuffAuthoringText.EffectIdUsage, FormatIdEntries(_idRegistryReport.EffectEntries));
            EditorGUILayout.EndVertical();
        }

        private void RefreshIdRegistryReport()
        {
            _idRegistryReport = BuffAuthoringIdRegistryScanner.Scan(_settings);
        }

        private void DrawCandidateGraphLink()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(BuffAuthoringText.CandidateGraphLinkTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(BuffAuthoringText.CandidateGraphLinkHelp, MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _candidateGraph = (BuffCandidateGraph)EditorGUILayout.ObjectField(
                BuffAuthoringText.CurrentCandidateGraph,
                _candidateGraph,
                typeof(BuffCandidateGraph),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                ClearCompositeEffectPreview();
                RefreshCandidateSummary();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.CreateGraph, GUILayout.Height(24f)))
                    CreateCandidateGraphFromHub();

                using (new EditorGUI.DisabledScope(_candidateGraph == null))
                {
                    if (GUILayout.Button(BuffAuthoringText.OpenGraph, GUILayout.Height(24f)))
                        AssetDatabase.OpenAsset(_candidateGraph);

                    if (GUILayout.Button(BuffAuthoringText.PingGraph, GUILayout.Height(24f)))
                    {
                        Selection.activeObject = _candidateGraph;
                        EditorGUIUtility.PingObject(_candidateGraph);
                    }

                    if (GUILayout.Button(BuffAuthoringText.RefreshCandidateSummary, GUILayout.Height(24f)))
                        RefreshCandidateSummary();
                }
            }

            DrawCandidateSummary();
            DrawGraphGenerateArea();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8f);
        }

        private void CreateCandidateGraphFromHub()
        {
            if (_settings == null)
                _settings = BuffAuthoringHubSettings.Load();

            string graphFolder = BuffAuthoringHubSettings.NormalizePath(_settings.GraphDefaultFolder);
            BuffAuthoringHubSettings.EnsureGraphFolderExists(graphFolder);

            if (!BuffAuthoringHubSettings.FolderExists(graphFolder))
            {
                EditorUtility.DisplayDialog(
                    BuffAuthoringText.CandidateGraphLinkTitle,
                    $"图默认目录无效或无法创建：{graphFolder}",
                    "OK");
                return;
            }

            _candidateGraph = BuffCandidateGraphCreateMenu.CreateGraphAsset(graphFolder, true, false);
            ClearCompositeEffectPreview();
            RefreshCandidateSummary();
        }

        private void DrawCandidateSummary()
        {
            if (_candidateGraph == null)
            {
                EditorGUILayout.HelpBox(BuffAuthoringText.NoCandidateGraphSelected, MessageType.None);
                return;
            }

            if (_candidateSummary == null || _candidateSummary.Graph != _candidateGraph)
                RefreshCandidateSummary();

            if (_candidateSummary == null)
            {
                EditorGUILayout.HelpBox(BuffAuthoringText.CandidateSummaryUnavailable, MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.GraphVersion, _candidateSummary.GraphVersion.ToString());
                DrawSummaryField(BuffAuthoringText.ConfigId, _candidateSummary.ConfigId.ToString());
                DrawSummaryField(BuffAuthoringText.BuffName, _candidateSummary.BuffName);
                DrawSummaryField(BuffAuthoringText.EffectId, _candidateSummary.EffectId.ToString());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.GraphComplete, FormatBool(_candidateSummary.IsComplete));
                DrawSummaryField(BuffAuthoringText.CanSubmitForReview, FormatBool(_candidateSummary.CanSubmitForReview));
                DrawSummaryField(BuffAuthoringText.EffectRegistered, FormatBool(_candidateSummary.EffectRegistered));
                DrawSummaryField(BuffAuthoringText.CompressedEligibility, FormatBool(_candidateSummary.Eligibility));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.EffectNodeCount, _candidateSummary.EffectNodeCount.ToString());
                DrawSummaryField(BuffAuthoringText.EffectCompositionRootExists, FormatBool(_candidateSummary.EffectCompositionRootExists));
                DrawSummaryField(BuffAuthoringText.EffectOrderMode, _candidateSummary.EffectOrderMode);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.UsesLegacyBuffRoot, FormatBool(_candidateSummary.UsesLegacyBuffRoot));
                DrawSummaryField(BuffAuthoringText.UsesLegacyEffectBindingNode, FormatBool(_candidateSummary.UsesLegacyEffectBindingNode));
                DrawSummaryField(BuffAuthoringText.HasMultipleEffectNodes, FormatBool(_candidateSummary.HasMultipleEffectNodes));
                DrawSummaryField(BuffAuthoringText.DeprecatedPlaceholderCount, _candidateSummary.DeprecatedPlaceholderCount.ToString());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryField(BuffAuthoringText.ScriptActionNodeCount, _candidateSummary.ScriptActionNodeCount.ToString());
                DrawSummaryField(BuffAuthoringText.ValidScriptActionNodeCount, _candidateSummary.ValidScriptActionNodeCount.ToString());
                DrawSummaryField(BuffAuthoringText.InvalidScriptActionNodeCount, _candidateSummary.InvalidScriptActionNodeCount.ToString());
                DrawSummaryField(BuffAuthoringText.ScriptActionWarningCount, _candidateSummary.ScriptActionWarningCount.ToString());
            }

            DrawTextArea(BuffAuthoringText.EffectOrderSummary, _candidateSummary.EffectOrderSummary);
            DrawTextArea(BuffAuthoringText.LifecycleSummary, _candidateSummary.LifecycleSummary);
            DrawTextArea(BuffAuthoringText.ScriptActionSummary, _candidateSummary.ScriptActionSummary);
            DrawTextArea(BuffAuthoringText.ScriptActionWarnings, _candidateSummary.ScriptActionWarnings);
            EditorGUILayout.LabelField(BuffAuthoringText.CandidateDiagnosis, _candidateSummary.Diagnosis);
            DrawTextArea(BuffAuthoringText.RejectReasons, _candidateSummary.RejectReasons);
            DrawTextArea(BuffAuthoringText.Warnings, _candidateSummary.Warnings);
            DrawTextArea(BuffAuthoringText.NextActions, _candidateSummary.NextActions);
        }

        private void DrawGraphGenerateArea()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(BuffAuthoringText.GraphGenerateTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(BuffAuthoringText.GraphGenerateHelp, MessageType.Info);

            if (_candidateGraph == null)
            {
                EditorGUILayout.HelpBox(BuffAuthoringText.NoCandidateGraphSelected, MessageType.None);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_graphGeneratePlan == null || _graphGeneratePlan.Graph != _candidateGraph)
                RefreshGraphGeneratePlan();

            if (_graphGeneratePlan != null)
                DrawTextArea(BuffAuthoringText.GraphGeneratePlan, _graphGeneratePlan.ToDisplayText());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.CreatePrimaryEffectFromGraph, GUILayout.Height(28f)))
                    RunGraphGenerate(GraphGenerateMode.EffectOnly);

                if (GUILayout.Button(BuffAuthoringText.CreateBuffDraftFromGraph, GUILayout.Height(28f)))
                    RunGraphGenerate(GraphGenerateMode.BuffOnly);

                if (GUILayout.Button(BuffAuthoringText.CreateBuffAndEffectFromGraph, GUILayout.Height(28f)))
                    RunGraphGenerate(GraphGenerateMode.BuffAndEffect);
            }

            DrawCompositeEffectPreviewArea();

            if (_graphGenerateReport != null)
                DrawTextArea(BuffAuthoringText.GraphGenerateLastResult, _graphGenerateReport.ToDisplayText());

            EditorGUILayout.EndVertical();
        }

        private void DrawCompositeEffectPreviewArea()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(BuffAuthoringText.CompositePreviewTitle, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(BuffAuthoringText.CompositePreviewHelp, MessageType.Info);

            if (_compositePreviewPlan != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSummaryField(BuffAuthoringText.CompositeEffectId, FormatCompositeEffectId(_compositePreviewPlan.CompositeEffectId));
                    DrawSummaryField(BuffAuthoringText.CompositeEffectClassName, _compositePreviewPlan.CompositeEffectClassName);
                    DrawSummaryField(BuffAuthoringText.EffectNodeCount, _compositePreviewPlan.EffectNodeCount.ToString());
                    DrawSummaryField(BuffAuthoringText.ActionTotalCount, _compositePreviewPlan.ExpectedActionCallCount.ToString());
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSummaryField(BuffAuthoringText.OrderMode, _compositePreviewPlan.EffectOrderMode);
                    DrawSummaryField(BuffAuthoringText.PreviewState, _compositePreviewState);
                }

                DrawTextArea(BuffAuthoringText.CompositeLifecycleSummary, _compositePreviewPlan.BuildActionPreview());
            }
            else
            {
                DrawSummaryField(BuffAuthoringText.PreviewState, _compositePreviewState);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(BuffAuthoringText.PreviewCompositeEffectCode, GUILayout.Height(26f)))
                    RunCompositeEffectPreview();

                if (GUILayout.Button(BuffAuthoringText.CreateCompositeEffectDraftFromGraph, GUILayout.Height(26f)))
                    RunCompositeEffectGenerate();

                if (GUILayout.Button(BuffAuthoringText.CreateBuffAndCompositeEffectDraftFromGraph, GUILayout.Height(26f)))
                    RunBuffAndCompositeEffectGenerate();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_compositePreviewCode)))
                {
                    if (GUILayout.Button(BuffAuthoringText.CopyCompositeEffectCode, GUILayout.Height(26f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _compositePreviewCode;
                        EditorUtility.DisplayDialog(BuffAuthoringText.CompositePreviewTitle, BuffAuthoringText.CompositePreviewCopied, "OK");
                    }
                }

                if (GUILayout.Button(BuffAuthoringText.ClearCompositeEffectPreview, GUILayout.Height(26f)))
                    ClearCompositeEffectPreview();
            }

            if (_compositePreviewPlan != null)
            {
                DrawTextArea(BuffAuthoringText.Errors, JoinLines(_compositePreviewPlan.Errors));
                DrawTextArea(BuffAuthoringText.Warnings, JoinLines(_compositePreviewPlan.Warnings));
                DrawTextArea(BuffAuthoringText.Recommendations, JoinLines(_compositePreviewPlan.Infos));
            }

            DrawTextArea(BuffAuthoringText.Errors, _compositePreviewError);

            if (!string.IsNullOrWhiteSpace(_compositePreviewCode))
            {
                EditorGUILayout.LabelField(BuffAuthoringText.CompositeEffectCodePreview, EditorStyles.miniBoldLabel);
                _compositePreviewScroll = EditorGUILayout.BeginScrollView(_compositePreviewScroll, GUILayout.MinHeight(160f), GUILayout.MaxHeight(260f));
                EditorGUILayout.TextArea(BuildLimitedPreviewText(_compositePreviewCode, CompositePreviewMaxLines), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (CountLines(_compositePreviewCode) > CompositePreviewMaxLines)
                    EditorGUILayout.HelpBox(BuffAuthoringText.CompositePreviewDisplayLimited, MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void RefreshCandidateSummary()
        {
            if (_candidateGraph == null)
            {
                _candidateSummary = null;
                _graphGeneratePlan = null;
                return;
            }

            BuffCandidateGraphBridge.TryBuildSummary(_candidateGraph, out _candidateSummary);
            RefreshGraphGeneratePlan();
        }

        private void RefreshGraphGeneratePlan()
        {
            if (_candidateGraph == null)
            {
                _graphGeneratePlan = null;
                return;
            }

            BuffGraphGenerateService.BuildPlan(_candidateGraph, out _graphGeneratePlan);
        }

        private void RunGraphGenerate(GraphGenerateMode mode)
        {
            RefreshCandidateSummary();
            if (_graphGeneratePlan == null)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.GraphGenerateTitle, "无法构建 Graph Generate Plan。", "OK");
                return;
            }

            _graphGeneratePlan.WillGenerateEffect = mode != GraphGenerateMode.BuffOnly;
            _graphGeneratePlan.WillCreateBuff = mode != GraphGenerateMode.EffectOnly;

            if (mode == GraphGenerateMode.EffectOnly)
                _graphGenerateReport = BuffGraphGenerateService.CreatePrimaryEffectDraft(_graphGeneratePlan);
            else if (mode == GraphGenerateMode.BuffOnly)
                _graphGenerateReport = BuffGraphGenerateService.CreateBuffDraft(_graphGeneratePlan);
            else
                _graphGenerateReport = BuffGraphGenerateService.CreateBuffAndPrimaryEffectDraft(_graphGeneratePlan);

            RefreshCandidateSummary();
            string title = _graphGenerateReport.HasError ? "Graph Generate 失败" : "Graph Generate 完成";
            EditorUtility.DisplayDialog(BuffAuthoringText.GraphGenerateTitle, title + "\n\n" + _graphGenerateReport.ToDisplayText(), "OK");
        }

        private void RunCompositeEffectPreview()
        {
            _compositePreviewCode = string.Empty;
            _compositePreviewError = string.Empty;
            _compositePreviewScroll = Vector2.zero;

            if (_candidateGraph == null)
            {
                _compositePreviewPlan = null;
                _compositePreviewState = BuffAuthoringText.CompositePreviewFailed;
                _compositePreviewError = BuffAuthoringText.NoCandidateGraphSelected;
                return;
            }

            bool succeeded = BuffGraphGenerateService.TryPreviewCompositeEffectCode(
                _candidateGraph,
                out BuffGraphGeneratePlan generatePlan,
                out BuffGraphCompositeEffectPlan compositePlan,
                out string code,
                out string error);

            _graphGeneratePlan = generatePlan;
            _compositePreviewPlan = compositePlan;
            _compositePreviewCode = succeeded ? code : string.Empty;
            _compositePreviewError = succeeded ? string.Empty : error;
            _compositePreviewState = succeeded ? BuffAuthoringText.CompositePreviewSucceeded : BuffAuthoringText.CompositePreviewFailed;
        }

        private void RunCompositeEffectGenerate()
        {
            RefreshCandidateSummary();
            if (_graphGeneratePlan == null)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.GraphGenerateTitle, "无法构建 Graph Generate Plan。", "OK");
                return;
            }

            _graphGeneratePlan.WillGenerateEffect = true;
            _graphGeneratePlan.WillCreateBuff = false;
            _graphGenerateReport = BuffGraphGenerateService.CreateCompositeEffectDraft(_graphGeneratePlan);
            RefreshCandidateSummary();

            string title = _graphGenerateReport.HasError ? "CompositeEffect 生成失败" : "CompositeEffect 生成完成";
            EditorUtility.DisplayDialog(BuffAuthoringText.GraphGenerateTitle, title + "\n\n" + _graphGenerateReport.ToDisplayText(), "OK");
        }

        private void RunBuffAndCompositeEffectGenerate()
        {
            RefreshCandidateSummary();
            if (_graphGeneratePlan == null)
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.GraphGenerateTitle, "无法构建 Graph Generate Plan。", "OK");
                return;
            }

            _graphGeneratePlan.WillGenerateEffect = true;
            _graphGeneratePlan.WillCreateBuff = true;
            _graphGenerateReport = BuffGraphGenerateService.CreateBuffAndCompositeEffectDraft(_graphGeneratePlan);
            RefreshCandidateSummary();

            string title = _graphGenerateReport.HasError ? "Buff + CompositeEffect 生成失败" : "Buff + CompositeEffect 生成完成";
            EditorUtility.DisplayDialog(BuffAuthoringText.GraphGenerateTitle, title + "\n\n" + _graphGenerateReport.ToDisplayText(), "OK");
        }

        private void ClearCompositeEffectPreview()
        {
            _compositePreviewPlan = null;
            _compositePreviewCode = string.Empty;
            _compositePreviewError = string.Empty;
            _compositePreviewState = BuffAuthoringText.CompositePreviewNotRun;
            _compositePreviewScroll = Vector2.zero;
        }

        private static void DrawSummaryField(string label, string value)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(120f)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(value) ? BuffAuthoringText.None : value,
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static void DrawTextArea(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == BuffAuthoringText.None)
                return;

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.TextArea(value, GUILayout.MinHeight(32f));
        }

        private static string FormatBool(bool value)
        {
            return value ? BuffAuthoringText.True : BuffAuthoringText.False;
        }

        private static string FormatCompositeEffectId(int effectId)
        {
            return effectId > 0 ? effectId.ToString() : "<auto>";
        }

        private static string BuildLimitedPreviewText(string value, int maxLines)
        {
            if (string.IsNullOrEmpty(value) || maxLines <= 0)
                return string.Empty;

            string[] lines = value.Replace("\r\n", "\n").Split('\n');
            if (lines.Length <= maxLines)
                return value;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < maxLines; i++)
                builder.AppendLine(lines[i]);

            builder.AppendLine("...");
            builder.AppendLine(BuffAuthoringText.CompositePreviewDisplayLimited);
            return builder.ToString();
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            int count = 1;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                    count++;
            }

            return count;
        }

        private static string JoinLines(System.Collections.Generic.List<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return BuffAuthoringText.None;

            return string.Join("\n", lines);
        }

        private static string FormatIdEntries(System.Collections.Generic.List<BuffAuthoringIdEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return BuffAuthoringText.None;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int count = Mathf.Min(entries.Count, 12);
            for (int i = 0; i < count; i++)
            {
                BuffAuthoringIdEntry entry = entries[i];
                string name = string.IsNullOrWhiteSpace(entry.Name) ? entry.ClassName : entry.Name;
                builder.Append(entry.Id)
                    .Append(" | ")
                    .Append(string.IsNullOrWhiteSpace(name) ? BuffAuthoringText.Unknown : name)
                    .Append(" | ")
                    .Append(string.IsNullOrWhiteSpace(entry.Status) ? BuffAuthoringText.Unknown : entry.Status)
                    .Append(" | ")
                    .Append(entry.SourceKind)
                    .Append(" | ")
                    .Append(entry.Path);

                if (entry.IsReserved)
                    builder.Append(" | Reserved");

                builder.AppendLine();
            }

            if (entries.Count > count)
                builder.Append("... 其余 ").Append(entries.Count - count).Append(" 条请复制完整报告查看。");

            return builder.ToString();
        }

        private static void DrawPathState(string path, bool folderPath, bool graphFolder)
        {
            bool exists = folderPath
                ? BuffAuthoringHubSettings.FolderExists(path)
                : BuffAuthoringHubSettings.ParentFolderExistsForFile(path);

            string state = exists ? BuffAuthoringText.PathExists : BuffAuthoringText.PathMissing;
            EditorGUILayout.LabelField(" ", state, EditorStyles.miniLabel);

            if (!exists && graphFolder)
                EditorGUILayout.HelpBox(BuffAuthoringText.GraphFolderAutoCreateHint, MessageType.Info);
            else if (!exists)
                EditorGUILayout.HelpBox("本阶段只提示路径不存在，不会自动创建该路径。", MessageType.Warning);
        }

        private static void RevealFolderIfExists(string folder)
        {
            if (!BuffAuthoringHubSettings.FolderExists(folder))
            {
                EditorUtility.DisplayDialog(BuffAuthoringText.SettingsTitle, $"路径不存在：{folder}", "OK");
                return;
            }

            EditorUtility.RevealInFinder(folder);
        }

        private void EnsurePanels()
        {
            if (_validatorWindow == null)
            {
                _validatorWindow = CreateInstance<BuffAuthoringValidatorWindow>();
                _validatorWindow.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_createWizardWindow == null)
            {
                _createWizardWindow = CreateInstance<BuffCreateWizardWindow>();
                _createWizardWindow.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_effectTemplatePanel == null)
                _effectTemplatePanel = new EffectTemplateGeneratorPanel();
        }

        private static void DestroyPanel(EditorWindow panel)
        {
            if (panel != null)
                DestroyImmediate(panel);
        }
    }
}
