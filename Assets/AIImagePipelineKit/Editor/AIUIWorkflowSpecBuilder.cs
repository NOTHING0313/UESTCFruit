#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AIImagePipelineKit.Editor
{
    /// <summary>
    /// 基于结构化 UI Spec 的确定性 Prefab 生成工具。
    /// Codex 负责生成 JSON 规格，Unity 负责按规格稳定创建 UI。
    /// </summary>
    public sealed class AIUIWorkflowWindow : EditorWindow
    {
        private const string DefaultSpecPath = "Assets/Arts/AI_Generate/_UISpecs/BuildPanelV17Test/ui_BuildPanelV17Test_spec.json";
        private const string DefaultManifestPath = "Assets/Arts/AI_Generate/_UISpecs/BuildPanelV17Test/ui_BuildPanelV17Test_asset_manifest.json";

        private string _specPath = DefaultSpecPath;
        private string _manifestPath = DefaultManifestPath;
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private string _log = "";

        [MenuItem("Tools/AI Image Pipeline/UI Workflow")]
        public static void Open()
        {
            AIUIWorkflowWindow window = GetWindow<AIUIWorkflowWindow>("AI UI Workflow");
            window.minSize = new Vector2(760, 560);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("AI UI Workflow", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("推荐流程：Codex 先生成 ui_spec.json 与 asset_manifest.json；Unity 侧使用本窗口按结构化规格确定性生成 Prefab，避免仅凭 mockup 图片自由发挥。", MessageType.Info);

            DrawHealthSection();
            DrawSpecSection();
            DrawLogSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHealthSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Project Health", EditorStyles.boldLabel);

            AIUIWorkflowHealthReport report = AIUIWorkflowHealthChecker.Run();
            DrawHealthRow("TMP Essentials", report.tmpEssentialsInstalled, report.tmpEssentialsInstalled ? "Installed" : "Missing");
            DrawHealthRow("Default TMP Font", report.defaultTmpFontExists, report.defaultTmpFontName);
            DrawHealthRow("Chinese TMP Font", report.chineseTmpFontExists, report.chineseTmpFontName);
            DrawHealthRow("Output Root", report.outputRootExists, report.outputRootPath);
            DrawHealthRow("image_assets Config", report.codexConfigHasImageAssets, report.codexConfigPath);
            DrawHealthRow("MCP dist/index.js", report.mcpDistExists, report.mcpDistPath);

            if (GUILayout.Button("Run Full Local Check", GUILayout.Height(28)))
                _log = report.ToMarkdown();

            EditorGUILayout.EndVertical();
        }

        private static void DrawHealthRow(string label, bool ok, string detail)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(ok ? "✓" : "⚠", GUILayout.Width(22));
            GUILayout.Label(label, GUILayout.Width(150));
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(detail) ? (ok ? "OK" : "Missing") : detail, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpecSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Structured UI Spec", EditorStyles.boldLabel);

            _specPath = EditorGUILayout.TextField("Spec JSON", _specPath);
            _manifestPath = EditorGUILayout.TextField("Asset Manifest", _manifestPath);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Sample Spec + Manifest", GUILayout.Height(30)))
                CreateSampleSpecAndManifest();

            if (GUILayout.Button("Validate Spec", GUILayout.Height(30)))
                ValidateSpec();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build UI Prefab From Spec", GUILayout.Height(34)))
                BuildPrefabFromSpec();

            if (GUILayout.Button("Open Spec Folder", GUILayout.Height(34)))
                OpenSpecFolder();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Build UI Prefab From Spec 会按 JSON 创建/覆盖目标 Prefab。它会给 TMP 文本自动分配默认字体；如果 requiresChinese=true 但没有中文 TMP 字体，会停止执行。", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawLogSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Log / Report", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(180));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void CreateSampleSpecAndManifest()
        {
            AIUISpecSampleWriter.WriteSampleSpec(_specPath, _manifestPath);
            AssetDatabase.Refresh();
            _log = "Created sample files:\n" + _specPath + "\n" + _manifestPath;
        }

        private void ValidateSpec()
        {
            AIUISpecValidationResult result = AIUISpecBuilder.ValidateSpecFile(_specPath);
            _log = result.ToMarkdown();
        }

        private void BuildPrefabFromSpec()
        {
            AIUISpecBuildResult result = AIUISpecBuilder.BuildPrefabFromSpecFile(_specPath);
            _log = result.ToMarkdown();
        }

        private void OpenSpecFolder()
        {
            string fullPath = AIUIWorkflowPathUtility.ProjectRelativeToAbsolute(_specPath);
            string folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                EditorUtility.RevealInFinder(folder);
            else
                EditorUtility.DisplayDialog("Folder Missing", "Spec folder does not exist yet.", "OK");
        }
    }

    [Serializable]
    public sealed class AIUISpecDocument
    {
        public string schemaVersion = "1.0";
        public string uiName = "BuildPanelV17Test";
        public string prefabPath = "Assets/Prefabs/UI/BuildPanelV17Test.prefab";
        public string mode = "responsive-first";
        public bool requiresChinese = true;
        public Vector2IntData referenceResolution = new Vector2IntData { x = 1920, y = 1080 };
        public AIUIElementSpec[] elements = new AIUIElementSpec[0];
    }

    [Serializable]
    public sealed class AIUIElementSpec
    {
        public string name;
        public string parent;
        public string type = "panel"; // panel, button, label, image, spacer
        public string text;
        public string symbol;
        public string spritePath;
        public string anchor = "stretch";
        public string layout = "none"; // none, horizontal, vertical, grid
        public string color = "#00000000";
        public string textColor = "#FFFFFFFF";
        public int fontSize = 28;
        public float width;
        public float height;
        public float minWidth;
        public float minHeight;
        public float preferredWidth;
        public float preferredHeight;
        public float flexibleWidth;
        public float flexibleHeight;
        public float spacing = 8;
        public float cellWidth = 180;
        public float cellHeight = 120;
        public int constraintCount = 3;
        public int paddingLeft;
        public int paddingRight;
        public int paddingTop;
        public int paddingBottom;
        public bool raycastTarget = true;
    }

    [Serializable]
    public struct Vector2IntData
    {
        public int x;
        public int y;
    }

    public sealed class AIUISpecBuildResult
    {
        public bool success;
        public string prefabPath;
        public readonly List<string> createdObjects = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> errors = new List<string>();

        public string ToMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(success ? "# UI Build Result: Success" : "# UI Build Result: Failed");
            builder.AppendLine();
            builder.AppendLine("Prefab: " + prefabPath);
            builder.AppendLine();
            AppendList(builder, "Created Objects", createdObjects);
            AppendList(builder, "Warnings", warnings);
            AppendList(builder, "Errors", errors);
            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, string title, List<string> values)
        {
            builder.AppendLine("## " + title);
            if (values.Count == 0)
            {
                builder.AppendLine("- None");
                builder.AppendLine();
                return;
            }

            foreach (string value in values)
                builder.AppendLine("- " + value);
            builder.AppendLine();
        }
    }

    public sealed class AIUISpecValidationResult
    {
        public bool success;
        public string specPath;
        public string prefabPath;
        public int elementCount;
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> errors = new List<string>();

        public string ToMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(success ? "# Spec Validation: Passed" : "# Spec Validation: Failed");
            builder.AppendLine();
            builder.AppendLine("Spec: " + specPath);
            builder.AppendLine("Prefab: " + prefabPath);
            builder.AppendLine("Elements: " + elementCount);
            builder.AppendLine();
            AppendList(builder, "Warnings", warnings);
            AppendList(builder, "Errors", errors);
            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, string title, List<string> values)
        {
            builder.AppendLine("## " + title);
            if (values.Count == 0)
            {
                builder.AppendLine("- None");
                builder.AppendLine();
                return;
            }

            foreach (string value in values)
                builder.AppendLine("- " + value);
            builder.AppendLine();
        }
    }

    public static class AIUISpecBuilder
    {
        public static AIUISpecValidationResult ValidateSpecFile(string specPath)
        {
            AIUISpecValidationResult result = new AIUISpecValidationResult { specPath = specPath };
            AIUISpecDocument spec = LoadSpec(specPath, result.errors);
            if (spec == null)
            {
                result.success = false;
                return result;
            }

            result.prefabPath = spec.prefabPath;
            result.elementCount = spec.elements != null ? spec.elements.Length : 0;
            ValidateSpec(spec, result.errors, result.warnings);
            result.success = result.errors.Count == 0;
            return result;
        }

        public static AIUISpecBuildResult BuildPrefabFromSpecFile(string specPath)
        {
            AIUISpecBuildResult result = new AIUISpecBuildResult();
            AIUISpecDocument spec = LoadSpec(specPath, result.errors);
            if (spec == null)
            {
                result.success = false;
                return result;
            }

            result.prefabPath = spec.prefabPath;
            ValidateSpec(spec, result.errors, result.warnings);
            if (result.errors.Count > 0)
            {
                result.success = false;
                return result;
            }

            TMP_FontAsset fontAsset = AITMPFontSetupTool.ResolveDefaultFontAsset();
            if (fontAsset == null)
            {
                result.errors.Add("No TMP_FontAsset found. Import TMP Essentials and create/select a default TMP font first.");
                result.success = false;
                return result;
            }

            if (spec.requiresChinese && !EnsureChineseCapability(fontAsset))
            {
                result.errors.Add("requiresChinese=true, but current TMP font does not appear to support Chinese. Use Tools > AI Image Pipeline > TextMeshPro Setup to create a dynamic Chinese TMP font asset first.");
                result.success = false;
                return result;
            }

            string prefabAbsolutePath = AIUIWorkflowPathUtility.ProjectRelativeToAbsolute(spec.prefabPath);
            string prefabFolder = Path.GetDirectoryName(prefabAbsolutePath);
            if (!string.IsNullOrEmpty(prefabFolder))
                Directory.CreateDirectory(prefabFolder);

            Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
            Dictionary<string, AIUIElementSpec> specsByName = new Dictionary<string, AIUIElementSpec>();
            GameObject root = null;

            foreach (AIUIElementSpec element in spec.elements)
            {
                GameObject go = CreateElement(element, fontAsset, result);
                if (go == null)
                    continue;

                if (objects.ContainsKey(element.name))
                {
                    result.errors.Add("Duplicate element name: " + element.name);
                    UnityEngine.Object.DestroyImmediate(go);
                    continue;
                }

                objects[element.name] = go;
                specsByName[element.name] = element;
                result.createdObjects.Add(element.name + " (" + element.type + ")");
            }

            foreach (KeyValuePair<string, GameObject> pair in objects)
            {
                AIUIElementSpec element = specsByName[pair.Key];
                GameObject go = pair.Value;

                if (!string.IsNullOrEmpty(element.parent))
                    continue;

                if (root == null)
                    root = go;
                else
                    result.warnings.Add("Multiple root elements detected. Element will be parented under first root: " + element.name);
            }

            if (root == null)
            {
                result.errors.Add("No root element was created. Add one element with empty parent.");
                result.success = false;
                foreach (GameObject go in objects.Values)
                    UnityEngine.Object.DestroyImmediate(go);
                return result;
            }

            foreach (KeyValuePair<string, GameObject> pair in objects)
            {
                AIUIElementSpec element = specsByName[pair.Key];
                GameObject go = pair.Value;

                if (go == root)
                    continue;

                if (!string.IsNullOrEmpty(element.parent) && objects.TryGetValue(element.parent, out GameObject parent))
                    go.transform.SetParent(parent.transform, false);
                else
                {
                    if (!string.IsNullOrEmpty(element.parent))
                        result.warnings.Add("Parent not found. Fallback to root: " + element.name + " -> " + element.parent);
                    go.transform.SetParent(root.transform, false);
                }
            }

            AITMPFontSetupTool.RepairTexts(root.GetComponentsInChildren<TMP_Text>(true), fontAsset);
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteBuildReport(spec, result, specPath);

            result.success = result.errors.Count == 0;
            return result;
        }

        private static AIUISpecDocument LoadSpec(string specPath, List<string> errors)
        {
            string absolutePath = AIUIWorkflowPathUtility.ProjectRelativeToAbsolute(specPath);
            if (!File.Exists(absolutePath))
            {
                errors.Add("Spec file not found: " + specPath);
                return null;
            }

            try
            {
                string json = File.ReadAllText(absolutePath, Encoding.UTF8);
                AIUISpecDocument spec = JsonUtility.FromJson<AIUISpecDocument>(json);
                if (spec == null)
                    errors.Add("JsonUtility returned null. Check JSON structure.");
                return spec;
            }
            catch (Exception ex)
            {
                errors.Add("Failed to read/parse spec: " + ex.Message);
                return null;
            }
        }

        private static void ValidateSpec(AIUISpecDocument spec, List<string> errors, List<string> warnings)
        {
            if (string.IsNullOrEmpty(spec.uiName))
                warnings.Add("uiName is empty.");

            if (string.IsNullOrEmpty(spec.prefabPath) || !spec.prefabPath.StartsWith("Assets/", StringComparison.Ordinal))
                errors.Add("prefabPath must be a project asset path starting with Assets/.");

            if (spec.elements == null || spec.elements.Length == 0)
            {
                errors.Add("elements is empty.");
                return;
            }

            HashSet<string> names = new HashSet<string>();
            int rootCount = 0;
            foreach (AIUIElementSpec element in spec.elements)
            {
                if (string.IsNullOrEmpty(element.name))
                {
                    errors.Add("Element has empty name.");
                    continue;
                }

                if (!names.Add(element.name))
                    errors.Add("Duplicate element name: " + element.name);

                if (string.IsNullOrEmpty(element.parent))
                    rootCount++;
            }

            if (rootCount == 0)
                errors.Add("No root element found. One element must have empty parent.");
            if (rootCount > 1)
                warnings.Add("Multiple root elements found. Only the first one is treated as prefab root.");
        }

        private static GameObject CreateElement(AIUIElementSpec element, TMP_FontAsset fontAsset, AIUISpecBuildResult result)
        {
            GameObject go = new GameObject(string.IsNullOrEmpty(element.name) ? "UIElement" : element.name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyAnchor(rect, element);
            ApplyLayoutElement(go, element);

            string type = (element.type ?? "panel").Trim().ToLowerInvariant();
            if (type == "panel")
            {
                ApplyImage(go, element, false);
                ApplyLayout(go, element);
            }
            else if (type == "button")
            {
                ApplyImage(go, element, true);
                Button button = go.AddComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                ApplyLayout(go, element);
                CreateButtonLabel(go.transform, element, fontAsset);
            }
            else if (type == "label")
            {
                TMP_Text text = go.AddComponent<TextMeshProUGUI>();
                ApplyText(text, element, fontAsset);
            }
            else if (type == "image")
            {
                ApplyImage(go, element, true);
            }
            else if (type == "spacer")
            {
                // LayoutElement only.
            }
            else
            {
                result.warnings.Add("Unknown element type, created as panel: " + element.name + " type=" + element.type);
                ApplyImage(go, element, false);
                ApplyLayout(go, element);
            }

            return go;
        }

        private static void CreateButtonLabel(Transform parent, AIUIElementSpec element, TMP_FontAsset fontAsset)
        {
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(parent, false);
            RectTransform rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TMP_Text text = labelGo.AddComponent<TextMeshProUGUI>();
            ApplyText(text, element, fontAsset);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static void ApplyImage(GameObject go, AIUIElementSpec element, bool createIfTransparent)
        {
            Color color = ParseColor(element.color, new Color(0f, 0f, 0f, 0f));
            bool hasVisibleColor = color.a > 0.001f;
            bool hasSprite = !string.IsNullOrEmpty(element.spritePath);
            if (!createIfTransparent && !hasVisibleColor && !hasSprite)
                return;

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = element.raycastTarget;

            if (hasSprite)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(element.spritePath);
                if (sprite != null)
                    image.sprite = sprite;
            }
        }

        private static void ApplyText(TMP_Text text, AIUIElementSpec element, TMP_FontAsset fontAsset)
        {
            text.font = fontAsset;
            if (fontAsset != null && fontAsset.material != null)
                text.fontSharedMaterial = fontAsset.material;

            string value = !string.IsNullOrEmpty(element.symbol) ? element.symbol : element.text;
            text.text = string.IsNullOrEmpty(value) ? element.name : value;
            text.fontSize = element.fontSize > 0 ? element.fontSize : 28;
            text.color = ParseColor(element.textColor, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
        }

        private static void ApplyLayout(GameObject go, AIUIElementSpec element)
        {
            string layout = (element.layout ?? "none").Trim().ToLowerInvariant();
            if (layout == "horizontal")
            {
                HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
                ApplyHorizontalVerticalLayout(group, element);
            }
            else if (layout == "vertical")
            {
                VerticalLayoutGroup group = go.AddComponent<VerticalLayoutGroup>();
                ApplyHorizontalVerticalLayout(group, element);
            }
            else if (layout == "grid")
            {
                GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(element.cellWidth > 0 ? element.cellWidth : 180, element.cellHeight > 0 ? element.cellHeight : 120);
                grid.spacing = new Vector2(element.spacing, element.spacing);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, element.constraintCount);
                grid.padding = CreatePadding(element);
            }
        }

        private static void ApplyHorizontalVerticalLayout(HorizontalOrVerticalLayoutGroup group, AIUIElementSpec element)
        {
            group.padding = CreatePadding(element);
            group.spacing = element.spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
        }

        private static RectOffset CreatePadding(AIUIElementSpec element)
        {
            return new RectOffset(element.paddingLeft, element.paddingRight, element.paddingTop, element.paddingBottom);
        }

        private static void ApplyLayoutElement(GameObject go, AIUIElementSpec element)
        {
            bool needsLayout = element.minWidth > 0 || element.minHeight > 0 || element.preferredWidth > 0 || element.preferredHeight > 0 || element.flexibleWidth > 0 || element.flexibleHeight > 0;
            if (!needsLayout)
                return;

            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            if (element.minWidth > 0) layoutElement.minWidth = element.minWidth;
            if (element.minHeight > 0) layoutElement.minHeight = element.minHeight;
            if (element.preferredWidth > 0) layoutElement.preferredWidth = element.preferredWidth;
            if (element.preferredHeight > 0) layoutElement.preferredHeight = element.preferredHeight;
            if (element.flexibleWidth > 0) layoutElement.flexibleWidth = element.flexibleWidth;
            if (element.flexibleHeight > 0) layoutElement.flexibleHeight = element.flexibleHeight;
        }

        private static void ApplyAnchor(RectTransform rect, AIUIElementSpec element)
        {
            string anchor = (element.anchor ?? "stretch").Trim().ToLowerInvariant();
            if (anchor == "stretch")
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            else if (anchor == "leftstretch")
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(element.width > 0 ? element.width : 220, 0f);
                rect.anchoredPosition = Vector2.zero;
            }
            else if (anchor == "rightstretch")
            {
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(element.width > 0 ? element.width : 320, 0f);
                rect.anchoredPosition = Vector2.zero;
            }
            else if (anchor == "topright")
            {
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.sizeDelta = new Vector2(element.width > 0 ? element.width : 48, element.height > 0 ? element.height : 48);
                rect.anchoredPosition = Vector2.zero;
            }
            else if (anchor == "center")
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(element.width > 0 ? element.width : 200, element.height > 0 ? element.height : 80);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private static bool EnsureChineseCapability(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
                return false;

            string sample = AITMPFontSetupTool.DefaultChineseSample;
            if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                fontAsset.TryAddCharacters(sample, out _);

            return fontAsset.HasCharacter('中') || fontAsset.HasCharacter('文');
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            string hex = value.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal))
                hex = hex.Substring(1);

            if (hex.Length == 6 || hex.Length == 8)
            {
                byte r;
                byte g;
                byte b;
                byte a = 255;
                if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
                    byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
                    byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                {
                    if (hex.Length == 8)
                        byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a);
                    return new Color32(r, g, b, a);
                }
            }

            return fallback;
        }

        private static void WriteBuildReport(AIUISpecDocument spec, AIUISpecBuildResult result, string specPath)
        {
            string specAbsolutePath = AIUIWorkflowPathUtility.ProjectRelativeToAbsolute(specPath);
            string folder = Path.GetDirectoryName(specAbsolutePath);
            if (string.IsNullOrEmpty(folder))
                return;

            string reportPath = Path.Combine(folder, "implementation_report.md");
            File.WriteAllText(reportPath, result.ToMarkdown(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }
    }

    public static class AIUISpecSampleWriter
    {
        public static void WriteSampleSpec(string specPath, string manifestPath)
        {
            string specAbsolute = AIUIWorkflowPathUtility.ProjectRelativeToAbsolute(specPath);
            string manifestAbsolute = AIUIWorkflowPathUtility.ProjectRelativeToAbsolute(manifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(specAbsolute) ?? Application.dataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestAbsolute) ?? Application.dataPath);

            string specJson = @"{
  ""schemaVersion"": ""1.0"",
  ""uiName"": ""BuildPanelV17Test"",
  ""prefabPath"": ""Assets/Prefabs/UI/BuildPanelV17Test.prefab"",
  ""mode"": ""responsive-first"",
  ""requiresChinese"": true,
  ""referenceResolution"": { ""x"": 1920, ""y"": 1080 },
  ""elements"": [
    { ""name"": ""BuildPanelV17TestRoot"", ""parent"": """", ""type"": ""panel"", ""anchor"": ""center"", ""width"": 1500, ""height"": 860, ""layout"": ""vertical"", ""color"": ""#0B1220DD"", ""paddingLeft"": 24, ""paddingRight"": 24, ""paddingTop"": 24, ""paddingBottom"": 24, ""spacing"": 16 },
    { ""name"": ""Header"", ""parent"": ""BuildPanelV17TestRoot"", ""type"": ""panel"", ""layout"": ""horizontal"", ""preferredHeight"": 70, ""color"": ""#10233FBB"", ""paddingLeft"": 16, ""paddingRight"": 16, ""paddingTop"": 8, ""paddingBottom"": 8, ""spacing"": 12 },
    { ""name"": ""Title"", ""parent"": ""Header"", ""type"": ""label"", ""text"": ""建造面板"", ""fontSize"": 32, ""textColor"": ""#D9F3FFFF"", ""flexibleWidth"": 1, ""preferredHeight"": 50 },
    { ""name"": ""CloseButton"", ""parent"": ""Header"", ""type"": ""button"", ""symbol"": ""×"", ""fontSize"": 36, ""preferredWidth"": 56, ""preferredHeight"": 50, ""color"": ""#1E3A5FDD"", ""textColor"": ""#FFFFFFFF"" },
    { ""name"": ""Body"", ""parent"": ""BuildPanelV17TestRoot"", ""type"": ""panel"", ""layout"": ""horizontal"", ""flexibleHeight"": 1, ""spacing"": 18 },
    { ""name"": ""LeftCategoryPanel"", ""parent"": ""Body"", ""type"": ""panel"", ""layout"": ""vertical"", ""preferredWidth"": 230, ""flexibleHeight"": 1, ""color"": ""#0F1E32AA"", ""paddingLeft"": 12, ""paddingRight"": 12, ""paddingTop"": 12, ""paddingBottom"": 12, ""spacing"": 10 },
    { ""name"": ""CategoryInfrastructure"", ""parent"": ""LeftCategoryPanel"", ""type"": ""button"", ""text"": ""基础设施"", ""preferredHeight"": 54, ""color"": ""#17375CDD"", ""textColor"": ""#D9F3FFFF"" },
    { ""name"": ""CategoryProduction"", ""parent"": ""LeftCategoryPanel"", ""type"": ""button"", ""text"": ""生产"", ""preferredHeight"": 54, ""color"": ""#122842DD"", ""textColor"": ""#C8E8FFFF"" },
    { ""name"": ""CategoryMilitary"", ""parent"": ""LeftCategoryPanel"", ""type"": ""button"", ""text"": ""军事"", ""preferredHeight"": 54, ""color"": ""#122842DD"", ""textColor"": ""#C8E8FFFF"" },
    { ""name"": ""CategoryDefense"", ""parent"": ""LeftCategoryPanel"", ""type"": ""button"", ""text"": ""防御"", ""preferredHeight"": 54, ""color"": ""#122842DD"", ""textColor"": ""#C8E8FFFF"" },
    { ""name"": ""CardListPanel"", ""parent"": ""Body"", ""type"": ""panel"", ""layout"": ""grid"", ""flexibleWidth"": 1, ""flexibleHeight"": 1, ""color"": ""#08111FAA"", ""cellWidth"": 220, ""cellHeight"": 150, ""spacing"": 14, ""constraintCount"": 3, ""paddingLeft"": 16, ""paddingRight"": 16, ""paddingTop"": 16, ""paddingBottom"": 16 },
    { ""name"": ""FactoryCard"", ""parent"": ""CardListPanel"", ""type"": ""button"", ""text"": ""工厂\n金属 120"", ""fontSize"": 24, ""color"": ""#12385DDD"", ""textColor"": ""#FFFFFFFF"" },
    { ""name"": ""PowerCard"", ""parent"": ""CardListPanel"", ""type"": ""button"", ""text"": ""电站\n能源 80"", ""fontSize"": 24, ""color"": ""#12385DDD"", ""textColor"": ""#FFFFFFFF"" },
    { ""name"": ""BarracksCard"", ""parent"": ""CardListPanel"", ""type"": ""button"", ""text"": ""兵营\n金属 160"", ""fontSize"": 24, ""color"": ""#12385DDD"", ""textColor"": ""#FFFFFFFF"" },
    { ""name"": ""DetailPanel"", ""parent"": ""Body"", ""type"": ""panel"", ""layout"": ""vertical"", ""preferredWidth"": 340, ""flexibleHeight"": 1, ""color"": ""#0F1E32CC"", ""paddingLeft"": 18, ""paddingRight"": 18, ""paddingTop"": 18, ""paddingBottom"": 18, ""spacing"": 14 },
    { ""name"": ""DetailTitle"", ""parent"": ""DetailPanel"", ""type"": ""label"", ""text"": ""工厂"", ""fontSize"": 30, ""preferredHeight"": 48, ""textColor"": ""#D9F3FFFF"" },
    { ""name"": ""DetailDescription"", ""parent"": ""DetailPanel"", ""type"": ""label"", ""text"": ""用于生产基础单位和扩展生产链。"", ""fontSize"": 22, ""preferredHeight"": 140, ""textColor"": ""#B8D8E8FF"" },
    { ""name"": ""BuildButton"", ""parent"": ""DetailPanel"", ""type"": ""button"", ""text"": ""建造"", ""preferredHeight"": 58, ""color"": ""#176C9FDD"", ""textColor"": ""#FFFFFFFF"" },
    { ""name"": ""Footer"", ""parent"": ""BuildPanelV17TestRoot"", ""type"": ""panel"", ""layout"": ""horizontal"", ""preferredHeight"": 54, ""color"": ""#10233FAA"", ""paddingLeft"": 16, ""paddingRight"": 16, ""paddingTop"": 8, ""paddingBottom"": 8 },
    { ""name"": ""ResourceHint"", ""parent"": ""Footer"", ""type"": ""label"", ""text"": ""资源：金属 500 / 能源 300"", ""fontSize"": 22, ""flexibleWidth"": 1, ""textColor"": ""#D9F3FFFF"" }
  ]
}";

            string manifestJson = @"{
  ""uiName"": ""BuildPanelV17Test"",
  ""contentIcons"": [
    { ""assetName"": ""icon_factory"", ""outputFolder"": ""BuildPanelV17Test/Icons"", ""generation"": ""image_assets"" },
    { ""assetName"": ""icon_power_plant"", ""outputFolder"": ""BuildPanelV17Test/Icons"", ""generation"": ""image_assets"" },
    { ""assetName"": ""icon_barracks"", ""outputFolder"": ""BuildPanelV17Test/Icons"", ""generation"": ""image_assets"" }
  ],
  ""controlIcons"": [
    { ""assetName"": ""close_x"", ""usage"": ""CloseButton"", ""generation"": ""tmp_symbol"", ""symbol"": ""×"" },
    { ""assetName"": ""selected_marker"", ""usage"": ""Selected card state"", ""generation"": ""unity_image"" }
  ],
  ""structuralAssets"": [
    { ""assetName"": ""panel_background"", ""generation"": ""unity_image"" },
    { ""assetName"": ""card_frame"", ""generation"": ""unity_image"" }
  ]
}";

            File.WriteAllText(specAbsolute, specJson, Encoding.UTF8);
            File.WriteAllText(manifestAbsolute, manifestJson, Encoding.UTF8);
        }
    }

    public sealed class AIUIWorkflowHealthReport
    {
        public bool tmpEssentialsInstalled;
        public bool defaultTmpFontExists;
        public bool chineseTmpFontExists;
        public bool outputRootExists;
        public bool codexConfigHasImageAssets;
        public bool mcpDistExists;
        public string defaultTmpFontName;
        public string chineseTmpFontName;
        public string outputRootPath;
        public string codexConfigPath;
        public string mcpDistPath;

        public string ToMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# AI Image Pipeline Health Check");
            builder.AppendLine();
            Append(builder, "TMP Essentials", tmpEssentialsInstalled, tmpEssentialsInstalled ? "Installed" : "Missing");
            Append(builder, "Default TMP Font", defaultTmpFontExists, defaultTmpFontName);
            Append(builder, "Chinese TMP Font", chineseTmpFontExists, chineseTmpFontName);
            Append(builder, "Output Root", outputRootExists, outputRootPath);
            Append(builder, "Codex image_assets Config", codexConfigHasImageAssets, codexConfigPath);
            Append(builder, "MCP dist/index.js", mcpDistExists, mcpDistPath);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string label, bool ok, string detail)
        {
            builder.AppendLine("- " + (ok ? "✓ " : "⚠ ") + label + ": " + (string.IsNullOrEmpty(detail) ? "None" : detail));
        }
    }

    public static class AIUIWorkflowHealthChecker
    {
        public static AIUIWorkflowHealthReport Run()
        {
            AIUIWorkflowHealthReport report = new AIUIWorkflowHealthReport();
            report.tmpEssentialsInstalled = TMP_Settings.instance != null;
            report.defaultTmpFontExists = TMP_Settings.defaultFontAsset != null;
            report.defaultTmpFontName = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "None";

            TMP_FontAsset chineseFont = FindChineseFont();
            report.chineseTmpFontExists = chineseFont != null;
            report.chineseTmpFontName = chineseFont != null ? chineseFont.name : "Missing";

            report.outputRootPath = EditorPrefs.GetString(AIImageImportPostprocessor.OutputRootPrefsKey, AIImageImportPostprocessor.DefaultTargetFolder);
            report.outputRootExists = AssetDatabase.IsValidFolder(report.outputRootPath);

            report.codexConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "config.toml").Replace("\\", "/");
            report.codexConfigHasImageAssets = File.Exists(report.codexConfigPath) && File.ReadAllText(report.codexConfigPath).Contains("[mcp_servers.image_assets]");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            report.mcpDistPath = Path.Combine(projectRoot, "Tools", "image-mcp-server", "dist", "index.js").Replace("\\", "/");
            report.mcpDistExists = File.Exists(report.mcpDistPath);
            return report;
        }

        private static TMP_FontAsset FindChineseFont()
        {
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null && IsChineseCapable(defaultFont))
                return defaultFont;

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" });
            foreach (string guid in guids)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null && IsChineseCapable(font))
                    return font;
            }

            return null;
        }

        private static bool IsChineseCapable(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                font.TryAddCharacters(AITMPFontSetupTool.DefaultChineseSample, out _);

            return font.HasCharacter('中') || font.HasCharacter('文');
        }
    }

    public static class AIUIWorkflowPathUtility
    {
        public static string ProjectRelativeToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return Application.dataPath;

            string normalized = assetPath.Replace("\\", "/");
            if (Path.IsPathRooted(normalized))
                return normalized;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }
    }
}
#endif
