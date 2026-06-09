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

        private static readonly string[] TabLabels =
        {
            BuffAuthoringText.ValidatorTab,
            BuffAuthoringText.CreateBuffTab,
            BuffAuthoringText.EffectTemplateTab
        };

        private Tab _tab;
        private BuffAuthoringValidatorWindow _validatorWindow;
        private BuffCreateWizardWindow _createWizardWindow;
        private EffectTemplateGeneratorPanel _effectTemplatePanel;
        private BuffCandidateGraph _candidateGraph;
        private BuffCandidateGraphSummary _candidateSummary;

        private enum Tab
        {
            Validator = 0,
            CreateBuff = 1,
            EffectTemplate = 2
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
            window._tab = tab;
            window.EnsurePanels();
            window.Show();
        }

        private void OnEnable()
        {
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

            DrawCandidateGraphLink();

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
                RefreshCandidateSummary();

            using (new EditorGUILayout.HorizontalScope())
            {
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
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8f);
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

            EditorGUILayout.LabelField(BuffAuthoringText.CandidateDiagnosis, _candidateSummary.Diagnosis);
            DrawTextArea(BuffAuthoringText.RejectReasons, _candidateSummary.RejectReasons);
            DrawTextArea(BuffAuthoringText.Warnings, _candidateSummary.Warnings);
            DrawTextArea(BuffAuthoringText.NextActions, _candidateSummary.NextActions);
        }

        private void RefreshCandidateSummary()
        {
            if (_candidateGraph == null)
            {
                _candidateSummary = null;
                return;
            }

            BuffCandidateGraphBridge.TryBuildSummary(_candidateGraph, out _candidateSummary);
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
