#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AIImagePipelineKit.Editor
{
    /// <summary>
    /// TextMeshPro 初始化、中文字体资源创建与现有 UI 文本修复工具。
    /// </summary>
    public sealed class AITMPFontSetupWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _sampleText = AITMPFontSetupTool.DefaultChineseSample;

        [MenuItem("Tools/AI Image Pipeline/TextMeshPro Setup")]
        public static void Open()
        {
            AITMPFontSetupWindow window = GetWindow<AITMPFontSetupWindow>("TMP Setup");
            window.minSize = new Vector2(620, 420);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("TextMeshPro / Chinese Font Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这个窗口用于初始化 TMP Essentials、从你项目中的中文字体文件创建动态 TMP_FontAsset，并批量修复 UI 中 font 为空的 TextMeshProUGUI。包内不会自带字体文件，请先将可合法使用的 .ttf/.otf 字体导入项目。", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Step 1 - TMP Essentials", EditorStyles.boldLabel);
            if (GUILayout.Button("Import TMP Essentials", GUILayout.Height(30)))
                AITMPFontSetupTool.ImportTMPEssentials();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Step 2 - Chinese Font Asset", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("先在 Project 面板中选中一个支持中文的 Font 文件，例如思源黑体 / Noto Sans CJK / Microsoft YaHei 等，然后点击下面按钮创建动态 TMP 字体资源。", MessageType.None);
            _sampleText = EditorGUILayout.TextField("Sample Characters", _sampleText);

            if (GUILayout.Button("Create Dynamic Chinese TMP Font From Selected Font", GUILayout.Height(30)))
                AITMPFontSetupTool.CreateDynamicTMPFontAssetFromSelectedFont(_sampleText);

            if (GUILayout.Button("Set Selected TMP FontAsset As Project Default + Fallback", GUILayout.Height(30)))
                AITMPFontSetupTool.SetSelectedTMPFontAssetAsDefaultAndFallback();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Step 3 - Repair Existing UI", EditorStyles.boldLabel);
            if (GUILayout.Button("Repair TMP Font Assets In Selected Object", GUILayout.Height(28)))
                AITMPFontSetupTool.RepairSelectedObject();

            if (GUILayout.Button("Repair TMP Font Assets In All Prefabs Under Assets", GUILayout.Height(28)))
                AITMPFontSetupTool.RepairAllPrefabs();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
            DrawReadonly("TMP Settings", TMP_Settings.instance != null ? AssetDatabase.GetAssetPath(TMP_Settings.instance) : "Missing / not imported");
            DrawReadonly("Default TMP Font", TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset.name : "None");

            EditorGUILayout.EndScrollView();
        }

        private static void DrawReadonly(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150));
            EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// TMP 字体初始化与修复的实际实现。所有操作都是增量式，不会重建 UI Prefab。
    /// </summary>
    public static class AITMPFontSetupTool
    {
        public const string DefaultChineseSample = "中文测试 建造面板 基础设施 生产 军事 防御 关闭 确认 取消 返回 搜索 筛选 排序 资源 消耗 说明";

        [MenuItem("Tools/AI Image Pipeline/TextMeshPro/Import TMP Essentials")]
        public static void ImportTMPEssentials()
        {
            bool executed = EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
            if (!executed)
                executed = EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essentials");

            if (executed)
                Debug.Log("TMP Essentials importer opened. Please confirm the import dialog if Unity shows one.");
            else
                EditorUtility.DisplayDialog("TMP Essentials", "Could not open TMP Essentials importer automatically. Please use Window > TextMeshPro > Import TMP Essential Resources.", "OK");
        }

        [MenuItem("Tools/AI Image Pipeline/TextMeshPro/Create Dynamic Chinese TMP Font From Selected Font")]
        public static void CreateDynamicTMPFontAssetFromSelectedFontMenu()
        {
            CreateDynamicTMPFontAssetFromSelectedFont(DefaultChineseSample);
        }

        public static void CreateDynamicTMPFontAssetFromSelectedFont(string sampleCharacters)
        {
            Font sourceFont = Selection.activeObject as Font;
            if (sourceFont == null)
            {
                EditorUtility.DisplayDialog("No Font Selected", "Please select a .ttf or .otf Font asset in the Project window first.", "OK");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceFont);
            string defaultDirectory = string.IsNullOrEmpty(sourcePath) ? "Assets" : Path.GetDirectoryName(sourcePath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(defaultDirectory))
                defaultDirectory = "Assets";

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Dynamic Chinese TMP Font Asset",
                sourceFont.name + "_Chinese_Dynamic_SDF.asset",
                "asset",
                "Choose where to save the generated TMP_FontAsset.",
                defaultDirectory);

            if (string.IsNullOrEmpty(assetPath))
                return;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("Create Failed", "TMP_FontAsset.CreateFontAsset returned null.", "OK");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TryAddSampleCharacters(fontAsset, sampleCharacters);
            SetDefaultFontAsset(fontAsset, addAsFallback: true);

            Selection.activeObject = fontAsset;
            EditorGUIUtility.PingObject(fontAsset);

            Debug.Log($"Created dynamic Chinese TMP font asset: {assetPath}");
        }

        [MenuItem("Tools/AI Image Pipeline/TextMeshPro/Set Selected TMP FontAsset As Default + Fallback")]
        public static void SetSelectedTMPFontAssetAsDefaultAndFallback()
        {
            TMP_FontAsset fontAsset = Selection.activeObject as TMP_FontAsset;
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("No TMP FontAsset Selected", "Please select a TMP_FontAsset first.", "OK");
                return;
            }

            SetDefaultFontAsset(fontAsset, addAsFallback: true);
            Debug.Log($"Set TMP default font asset to: {fontAsset.name}");
        }

        [MenuItem("Tools/AI Image Pipeline/TextMeshPro/Repair TMP Font Assets/In Selected Object")]
        public static void RepairSelectedObject()
        {
            TMP_FontAsset fontAsset = ResolveDefaultFontAsset();
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Font Missing", "No TMP_FontAsset found. Import TMP Essentials or create a Chinese TMP font asset first.", "OK");
                return;
            }

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a UI root or Prefab instance first.", "OK");
                return;
            }

            TMP_Text[] texts = selected.GetComponentsInChildren<TMP_Text>(true);
            int fixedCount = RepairTexts(texts, fontAsset);

            EditorUtility.SetDirty(selected);
            Debug.Log($"TMP font repair finished. Fixed {fixedCount} TMP text components under selected object: {selected.name}");
        }

        [MenuItem("Tools/AI Image Pipeline/TextMeshPro/Repair TMP Font Assets/In All Prefabs Under Assets")]
        public static void RepairAllPrefabs()
        {
            TMP_FontAsset fontAsset = ResolveDefaultFontAsset();
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("TMP Font Missing", "No TMP_FontAsset found. Import TMP Essentials or create a Chinese TMP font asset first.", "OK");
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int fixedPrefabCount = 0;
            int fixedTextCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                    int fixedCount = RepairTexts(texts, fontAsset);

                    if (fixedCount > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        fixedPrefabCount++;
                        fixedTextCount += fixedCount;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"TMP prefab repair finished. Fixed {fixedTextCount} TMP text components in {fixedPrefabCount} prefabs.");
        }

        /// <summary>查找项目中的默认 TMP 字体资源。</summary>
        public static TMP_FontAsset ResolveDefaultFontAsset()
        {
            if (TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;

            string[] chineseNames =
            {
                "Chinese_Dynamic_SDF t:TMP_FontAsset",
                "NotoSansCJK t:TMP_FontAsset",
                "SourceHanSans t:TMP_FontAsset",
                "MicrosoftYaHei t:TMP_FontAsset",
                "微软雅黑 t:TMP_FontAsset"
            };

            foreach (string query in chineseNames)
            {
                TMP_FontAsset font = LoadFirstFontAsset(AssetDatabase.FindAssets(query));
                if (font != null)
                    return font;
            }

            TMP_FontAsset liberation = LoadFirstFontAsset(AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset"));
            if (liberation != null)
                return liberation;

            return LoadFirstFontAsset(AssetDatabase.FindAssets("t:TMP_FontAsset"));
        }

        /// <summary>给所有 font 为空的 TMP_Text 组件补充字体。</summary>
        public static int RepairTexts(IEnumerable<TMP_Text> texts, TMP_FontAsset fontAsset)
        {
            int fixedCount = 0;

            foreach (TMP_Text text in texts)
            {
                if (text == null || text.font != null)
                    continue;

                Undo.RecordObject(text, "Repair TMP Font Asset");
                text.font = fontAsset;

                if (fontAsset.material != null)
                    text.fontSharedMaterial = fontAsset.material;

                EditorUtility.SetDirty(text);
                fixedCount++;
            }

            return fixedCount;
        }

        private static void TryAddSampleCharacters(TMP_FontAsset fontAsset, string sampleCharacters)
        {
            if (fontAsset == null || string.IsNullOrEmpty(sampleCharacters))
                return;

            bool added = fontAsset.TryAddCharacters(sampleCharacters, out string missingCharacters);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            if (!added || !string.IsNullOrEmpty(missingCharacters))
                Debug.LogWarning($"TMP font asset was created, but some sample Chinese characters are missing: {missingCharacters}");
        }

        private static TMP_FontAsset LoadFirstFontAsset(IEnumerable<string> guids)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset != null)
                    return fontAsset;
            }

            return null;
        }

        private static void SetDefaultFontAsset(TMP_FontAsset fontAsset, bool addAsFallback)
        {
            if (fontAsset == null)
                return;

            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("TMP Settings Missing", "TMP Settings not found. Please import TMP Essentials first.", "OK");
                return;
            }

            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty defaultFontAsset = serializedSettings.FindProperty("m_defaultFontAsset");
            if (defaultFontAsset != null)
                defaultFontAsset.objectReferenceValue = fontAsset;

            if (addAsFallback)
                AddFallbackFont(serializedSettings, fontAsset);

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void AddFallbackFont(SerializedObject serializedSettings, TMP_FontAsset fontAsset)
        {
            SerializedProperty fallbackFonts = serializedSettings.FindProperty("m_fallbackFontAssets");
            if (fallbackFonts == null || !fallbackFonts.isArray)
                return;

            for (int i = 0; i < fallbackFonts.arraySize; i++)
            {
                SerializedProperty element = fallbackFonts.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == fontAsset)
                    return;
            }

            fallbackFonts.InsertArrayElementAtIndex(fallbackFonts.arraySize);
            fallbackFonts.GetArrayElementAtIndex(fallbackFonts.arraySize - 1).objectReferenceValue = fontAsset;
        }
    }
}
#endif
