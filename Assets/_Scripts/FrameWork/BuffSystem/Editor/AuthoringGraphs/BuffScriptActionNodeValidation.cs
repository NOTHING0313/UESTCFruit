using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// ScriptActionNode 的 Editor-only 校验器。只读取脚本元信息和源码文本，不修改 runtime、registry 或 whitelist。
    /// </summary>
    internal static class BuffScriptActionNodeValidator
    {
        private static readonly string[] ForbiddenSourceTokens =
        {
            "Time.time",
            "Time.deltaTime",
            "UnityEngine.Random",
            "System.Random",
            "GameObject",
            "Transform",
            "MonoBehaviour",
            "UnityEngine.Object",
            "Camera",
            "Input"
        };

        internal static BuffScriptActionValidationResult RefreshFromScript(ScriptActionNode node)
        {
            BuffScriptActionValidationResult result = Validate(node);
            if (node == null)
                return result;

            string typeName = result.ActionType != null ? result.ActionType.FullName : string.Empty;
            string displayName = result.ActionType != null ? result.ActionType.Name : string.Empty;
            string actionName = !string.IsNullOrWhiteSpace(node.ActionName) ? node.ActionName : displayName;
            string message = result.ToMessage();

            bool changed = node.ActionTypeName != typeName
                || node.ActionDisplayName != displayName
                || node.ActionName != actionName
                || node.IsValidAction != result.IsValid
                || node.ValidationMessage != message;

            if (changed)
            {
                Undo.RecordObject(node, "Refresh Script Action Node");
                node.ActionTypeName = typeName;
                node.ActionDisplayName = displayName;
                node.ActionName = actionName;
                node.IsValidAction = result.IsValid;
                node.ValidationMessage = message;
                EditorUtility.SetDirty(node);
            }

            return result;
        }

        internal static BuffScriptActionValidationResult Validate(ScriptActionNode node)
        {
            BuffScriptActionValidationResult result = new BuffScriptActionValidationResult();
            if (node == null)
            {
                result.Errors.Add("ScriptActionNode 为空。");
                return result;
            }

            if (node.ActionScript == null)
            {
                bool participates = CountPortConnections(node, "Previous") > 0 || CountPortConnections(node, "Next") > 0;
                if (participates)
                    result.Errors.Add("ActionScript 为空，但节点已经参与生命周期连接。");
                else
                    result.Warnings.Add("ActionScript 为空。");

                return result;
            }

            Type type = node.ActionScript.GetClass();
            result.ActionType = type;
            if (type == null)
            {
                result.Errors.Add("MonoScript.GetClass() 为空，脚本可能无法编译或不包含可识别类型。");
                return result;
            }

            ValidateType(type, result);
            ValidateRecommendedPath(node.ActionScript, result);
            ScanSourceForWarnings(node.ActionScript, result);
            WarnIfConnectedFromStackChanged(node, result);

            if (string.IsNullOrWhiteSpace(node.ActionName) && string.IsNullOrWhiteSpace(type.Name))
                result.Warnings.Add("ActionName 为空。");

            if (CountLifecycleConnections(node) == 0)
                result.Warnings.Add("ScriptActionNode 尚未连接任何生命周期端口。");

            return result;
        }

        internal static string ScanSourceForWarnings(MonoScript script)
        {
            BuffScriptActionValidationResult result = new BuffScriptActionValidationResult();
            ScanSourceForWarnings(script, result);
            return result.Warnings.Count == 0 ? string.Empty : string.Join("\n", result.Warnings);
        }

        private static void ValidateType(Type type, BuffScriptActionValidationResult result)
        {
            if (type.IsAbstract)
                result.Errors.Add("类型是 abstract，不能作为可实例化功能节点。");

            if (type.IsGenericTypeDefinition)
                result.Errors.Add("类型是泛型类型定义，不能作为功能节点。");

            if (typeof(MonoBehaviour).IsAssignableFrom(type))
                result.Errors.Add("不能使用 MonoBehaviour 派生脚本作为 Buff Action。");

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                result.Errors.Add("不能使用 UnityEngine.Object 派生脚本作为 Buff Action。");

            if (!typeof(global::BuffSystem.IBuffGraphAction).IsAssignableFrom(type))
                result.Errors.Add("脚本类型未实现 IBuffGraphAction，当前不能作为可生成调用链的 Buff Graph Action。");

            if (!IsLegalIdentifier(type.Name))
                result.Errors.Add("类名非法。");

            if (!string.IsNullOrWhiteSpace(type.Namespace) && !IsLegalNamespace(type.Namespace))
                result.Errors.Add("namespace 非法。");

            ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null || !constructor.IsPublic)
                result.Warnings.Add("没有 public parameterless constructor；未来生成可运行调用链时可能无法直接实例化。");
        }

        private static void ValidateRecommendedPath(MonoScript script, BuffScriptActionValidationResult result)
        {
            string path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/_Scripts/FrameWork/BuffSystem/Effects", StringComparison.Ordinal)
                && !normalized.StartsWith("Assets/_Scripts/FrameWork/BuffSystem/Actions", StringComparison.Ordinal))
                result.Warnings.Add($"脚本路径不在推荐目录：{normalized}");
        }

        private static void ScanSourceForWarnings(MonoScript script, BuffScriptActionValidationResult result)
        {
            string source = script != null ? script.text : string.Empty;
            if (string.IsNullOrEmpty(source))
                return;

            for (int i = 0; i < ForbiddenSourceTokens.Length; i++)
            {
                string token = ForbiddenSourceTokens[i];
                if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
                    result.Warnings.Add($"脚本中出现可能破坏确定性 / rollback 的 API：{token}。请人工确认。");
            }
        }

        private static void WarnIfConnectedFromStackChanged(ScriptActionNode node, BuffScriptActionValidationResult result)
        {
            XNode.NodePort previous = node.GetPort("Previous");
            if (previous == null || previous.ConnectionCount == 0)
                return;

            List<XNode.NodePort> connections = previous.GetConnections();
            for (int i = 0; i < connections.Count; i++)
            {
                XNode.NodePort connection = connections[i];
                if (connection != null && connection.fieldName == "OnStackChanged")
                {
                    result.Warnings.Add("OnStackChanged Action 当前不会接收 delta；第一版只会在未来调用 Execute(in BuffEffectContext context)。");
                    return;
                }
            }
        }

        private static int CountLifecycleConnections(ScriptActionNode node)
        {
            return CountPortConnections(node, "Previous") + CountPortConnections(node, "Next");
        }

        private static int CountPortConnections(XNode.Node node, string portName)
        {
            XNode.NodePort port = node.GetPort(portName);
            return port != null ? port.ConnectionCount : 0;
        }

        private static bool IsLegalNamespace(string value)
        {
            string[] parts = value.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!IsLegalIdentifier(parts[i]))
                    return false;
            }

            return true;
        }

        private static bool IsLegalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                    return false;
            }

            return true;
        }
    }

    internal sealed class BuffScriptActionValidationResult
    {
        internal Type ActionType;
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();

        internal bool IsValid => Errors.Count == 0 && ActionType != null;

        internal string ToMessage()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < Errors.Count; i++)
                lines.Add("Error: " + Errors[i]);

            for (int i = 0; i < Warnings.Count; i++)
                lines.Add("Warning: " + Warnings[i]);

            if (lines.Count == 0)
            {
                lines.Add("有效：脚本实现了 IBuffGraphAction，可在后续 Effect 代码生成阶段被调用。");
                lines.Add("提示：当前阶段不会自动生成 Effect 调用链。");
            }

            return string.Join("\n", lines);
        }
    }
}
