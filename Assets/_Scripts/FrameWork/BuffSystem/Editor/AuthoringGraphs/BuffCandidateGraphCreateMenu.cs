using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// BuffCandidateGraph 的 Editor-only 创建菜单兜底。
    /// 该菜单只在用户手动点击时创建候选审查图，不会自动创建 asset，也不参与 runtime。
    /// </summary>
    internal static class BuffCandidateGraphCreateMenu
    {
        private const string MenuPath = "Assets/Create/BuffSystem/Buff Candidate Graph";
        private const string DefaultAssetName = "NewBuffCandidateGraph.asset";

        [MenuItem(MenuPath, priority = 5100)]
        private static void CreateBuffCandidateGraph()
        {
            CreateGraphAsset(GetSelectedProjectFolder(), true, false);
        }

        /// <summary>
        /// 在指定目录创建候选图 asset。仅由用户点击菜单或 Hub 按钮时调用。
        /// </summary>
        internal static BuffCandidateGraph CreateGraphAsset(string folder, bool ping, bool open)
        {
            string safeFolder = string.IsNullOrWhiteSpace(folder) ? "Assets" : folder.Replace('\\', '/');
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{safeFolder}/{DefaultAssetName}");
            BuffCandidateGraph graph = ScriptableObject.CreateInstance<BuffCandidateGraph>();

            AssetDatabase.CreateAsset(graph, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;
            if (ping)
                EditorGUIUtility.PingObject(graph);

            if (open)
                AssetDatabase.OpenAsset(graph);

            return graph;
        }

        /// <summary>
        /// 获取 Project 面板当前选中的目录；如果选中的是文件，则使用该文件所在目录。
        /// </summary>
        private static string GetSelectedProjectFolder()
        {
            Object selected = Selection.activeObject;
            if (selected == null)
                return "Assets";

            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(selectedPath))
                return "Assets";

            if (Directory.Exists(selectedPath))
                return selectedPath.Replace('\\', '/');

            string directory = Path.GetDirectoryName(selectedPath);
            return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
        }
    }
}
