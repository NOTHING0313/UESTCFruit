using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace BuffSystem
{
    internal enum BuffAuthoringPreflightSeverity
    {
        Info,
        Warning,
        Fixup,
        Error
    }

    internal sealed class BuffAuthoringPreflightIssue
    {
        internal BuffAuthoringPreflightSeverity Severity;
        internal string Code;
        internal string Message;
        internal string FieldName;

        internal BuffAuthoringPreflightIssue(BuffAuthoringPreflightSeverity severity, string code, string message, string fieldName)
        {
            Severity = severity;
            Code = code;
            Message = message;
            FieldName = fieldName;
        }
    }

    internal sealed class BuffAuthoringPreflightResult
    {
        internal readonly List<BuffAuthoringPreflightIssue> Issues = new List<BuffAuthoringPreflightIssue>();

        internal bool HasError
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == BuffAuthoringPreflightSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        internal bool CanContinue => !HasError;

        internal void Add(BuffAuthoringPreflightSeverity severity, string code, string message, string fieldName)
        {
            Issues.Add(new BuffAuthoringPreflightIssue(severity, code, message, fieldName));
        }

        internal string ToDisplayText()
        {
            if (Issues.Count == 0)
                return "Preflight PASS。";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Issues.Count; i++)
            {
                BuffAuthoringPreflightIssue issue = Issues[i];
                builder.Append('[').Append(issue.Severity).Append("] ");
                if (!string.IsNullOrWhiteSpace(issue.FieldName))
                    builder.Append(issue.FieldName).Append(": ");

                builder.Append(issue.Message).AppendLine();
            }

            return builder.ToString();
        }
    }

    internal sealed class BuffAuthoringBuffPreflightDraft
    {
        internal int ConfigId;
        internal string BuffName;
        internal string SaveFolder;
        internal BuffInstanceType BuffType;
        internal BuffTriggerType TriggerType;
        internal ParallelBuffStorageMode ParallelStorageMode;
        internal bool Unlimited;
        internal int MaxStack;
        internal float Duration;
        internal float TickTime;
        internal ParallelBuffStackUpPolicy StackUpPolicy;
        internal ParallelBuffStackDownPolicy StackDownPolicy;
        internal int EffectId;
        internal string TargetAssetPath;
    }

    internal sealed class BuffAuthoringEffectPreflightDraft
    {
        internal int EffectId;
        internal string EffectClassName;
        internal string TargetFolder;
        internal string Namespace;
        internal string TargetFilePath;
        internal bool OnApply;
        internal bool OnTick;
        internal bool OnRemove;
        internal bool OnRefresh;
        internal bool OnStackChanged;
    }

    /// <summary>
    /// 创建 Buff / Effect 前的 Editor-only 预检。
    /// 只修正表单草稿值和生成诊断，不创建 runtime 状态，不注册 Effect，不修改 whitelist。
    /// </summary>
    internal static class BuffAuthoringPreflightValidator
    {
        private const string DefaultBuffName = "NewBuff";
        private const string DefaultNamespace = "BuffSystem";
        private const string DefaultEffectClassName = "NewBuffEffect";

        private static readonly Regex ClassNameRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex NamespaceRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);
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

        internal static BuffAuthoringPreflightResult RunBuffPreflight(BuffAuthoringBuffPreflightDraft draft, BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringPreflightResult result = new BuffAuthoringPreflightResult();
            BuffAuthoringHubSettingsData safeSettings = settings ?? BuffAuthoringHubSettings.Load();

            if (draft.ConfigId <= 0 && safeSettings.AutoAllocateIds)
            {
                draft.ConfigId = BuffAuthoringIdService.GetNextAvailableBuffConfigId(safeSettings);
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_ID_AUTO_ALLOCATED", $"ConfigId 缺失，已自动分配为 {draft.ConfigId}。", "ConfigId");
            }

            BuffAuthoringIdValidationResult idValidation = BuffAuthoringIdService.ValidateBuffConfigId(draft.ConfigId, safeSettings);
            for (int i = 0; i < idValidation.Errors.Count; i++)
                result.Add(BuffAuthoringPreflightSeverity.Error, "BUFF_ID_INVALID", NormalizeBuffIdMessage(idValidation.Errors[i]), "ConfigId");

            for (int i = 0; i < idValidation.Warnings.Count; i++)
                result.Add(BuffAuthoringPreflightSeverity.Warning, "BUFF_ID_WARNING", idValidation.Warnings[i], "ConfigId");

            if (string.IsNullOrWhiteSpace(draft.BuffName))
            {
                draft.BuffName = DefaultBuffName;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_NAME_DEFAULT", "BuffName 为空，已自动修复为 NewBuff。", "BuffName");
            }
            else
            {
                string safeName = BuffAuthoringValidationUtility.MakeSafeFileName(draft.BuffName, DefaultBuffName);
                if (!string.Equals(safeName, draft.BuffName, StringComparison.Ordinal))
                {
                    draft.BuffName = safeName;
                    result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_NAME_SAFE", $"BuffName 包含不适合文件名的字符，已修正为 {safeName}。", "BuffName");
                }
            }

            if (string.IsNullOrWhiteSpace(draft.SaveFolder))
            {
                draft.SaveFolder = safeSettings.BuffConfigDataDefaultFolder;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_FOLDER_DEFAULT", $"保存目录为空，已使用默认目录：{draft.SaveFolder}。", "SaveFolder");
            }

            draft.SaveFolder = NormalizePath(draft.SaveFolder).TrimEnd('/');
            if (!IsAssetPath(draft.SaveFolder))
                result.Add(BuffAuthoringPreflightSeverity.Error, "BUFF_FOLDER_INVALID", $"保存目录必须位于 Assets 下：{draft.SaveFolder}", "SaveFolder");
            else
                EnsureAssetFolder(draft.SaveFolder, result, "SaveFolder");

            if (draft.MaxStack <= 0)
            {
                draft.MaxStack = 1;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_MAX_STACK_DEFAULT", "MaxStack 小于等于 0，已自动修复为 1。", "MaxStack");
            }

            if (!draft.Unlimited && draft.Duration <= 0f)
            {
                draft.Duration = 1f;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_DURATION_DEFAULT", "非 Unlimited Buff 的 Duration 小于等于 0，已自动修复为 1。", "Duration");
            }

            if (draft.TriggerType == BuffTriggerType.Tick && draft.TickTime <= 0f)
            {
                draft.TickTime = 1f;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "BUFF_TICK_TIME_DEFAULT", "Tick Buff 的 TickTime 小于等于 0，已自动修复为 1。", "TickTime");
            }

            if (draft.EffectId <= 0)
                result.Add(BuffAuthoringPreflightSeverity.Warning, "BUFF_EFFECT_EMPTY", "EffectId 未设置：可以创建配置草稿，但该 Buff 暂不能作为可运行 production Buff。", "EffectId");
            else
            {
                EffectRegistryCheckResult registryCheck = BuffAuthoringValidationUtility.CheckProductionEffectRegistered(draft.EffectId);
                if (draft.EffectId >= BuffAuthoringIdRegistryScanner.ReservedDebugIdStart)
                    result.Add(BuffAuthoringPreflightSeverity.Warning, "BUFF_EFFECT_RESERVED", "EffectId 位于 990000+ Debug / Smoke / Reserved 段；作为引用可创建草稿，但不建议作为正式玩法 Effect。", "EffectId");
                else if (registryCheck.IsUnknown)
                    result.Add(BuffAuthoringPreflightSeverity.Warning, "BUFF_EFFECT_UNKNOWN", $"无法稳定检查 Effect 注册状态：{registryCheck.Status}", "EffectId");
                else if (!registryCheck.IsRegistered)
                    result.Add(BuffAuthoringPreflightSeverity.Warning, "BUFF_EFFECT_UNREGISTERED", "EffectId 未注册：可以创建草稿，但需要注册 Effect 后才能运行。", "EffectId");
            }

            CompressedEligibilityResult eligibility = BuffAuthoringValidationUtility.ComputeCompressedEligibility(
                draft.BuffType,
                draft.TriggerType,
                draft.ParallelStorageMode,
                draft.Unlimited,
                draft.MaxStack);
            if (draft.ParallelStorageMode == ParallelBuffStorageMode.CompressedExpiryFrameList && !eligibility.IsEligible)
                result.Add(BuffAuthoringPreflightSeverity.Warning, "BUFF_COMPRESSED_INELIGIBLE", "当前字段不满足 compressed eligibility，将无法进入 compressed runtime 候选。", "CompressedEligibility");

            draft.TargetAssetPath = $"{draft.SaveFolder}/{draft.ConfigId}_{BuffAuthoringValidationUtility.MakeSafeFileName(draft.BuffName, DefaultBuffName)}.asset";
            if (AssetExists(draft.TargetAssetPath))
                result.Add(BuffAuthoringPreflightSeverity.Error, "BUFF_TARGET_EXISTS", $"目标 asset 文件已经存在：{draft.TargetAssetPath}。", "TargetAsset");

            if (!result.HasError)
                result.Add(BuffAuthoringPreflightSeverity.Info, "BUFF_PREFLIGHT_PASS", "Buff Preflight 通过，将继续创建 BuffConfigData 草稿。", string.Empty);

            return result;
        }

        internal static BuffAuthoringPreflightResult RunEffectPreflight(BuffAuthoringEffectPreflightDraft draft, BuffAuthoringHubSettingsData settings)
        {
            BuffAuthoringPreflightResult result = new BuffAuthoringPreflightResult();
            BuffAuthoringHubSettingsData safeSettings = settings ?? BuffAuthoringHubSettings.Load();

            if (draft.EffectId <= 0 && safeSettings.AutoAllocateIds)
            {
                draft.EffectId = BuffAuthoringIdService.GetNextAvailableEffectId(safeSettings);
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "EFFECT_ID_AUTO_ALLOCATED", $"EffectId 缺失，已自动分配为 {draft.EffectId}。", "EffectId");
            }

            BuffAuthoringIdValidationResult idValidation = BuffAuthoringIdService.ValidateEffectId(draft.EffectId, safeSettings);
            for (int i = 0; i < idValidation.Errors.Count; i++)
                result.Add(BuffAuthoringPreflightSeverity.Error, "EFFECT_ID_INVALID", NormalizeEffectIdMessage(idValidation.Errors[i]), "EffectId");

            for (int i = 0; i < idValidation.Warnings.Count; i++)
                result.Add(BuffAuthoringPreflightSeverity.Warning, "EFFECT_ID_WARNING", idValidation.Warnings[i], "EffectId");

            if (string.IsNullOrWhiteSpace(draft.EffectClassName))
                result.Add(BuffAuthoringPreflightSeverity.Error, "EFFECT_CLASS_EMPTY", "Effect 类名不能为空。", "EffectClassName");
            else if (!IsValidClassName(draft.EffectClassName))
                result.Add(BuffAuthoringPreflightSeverity.Error, "EFFECT_CLASS_INVALID", "Effect 类名不是合法 C# 类名。", "EffectClassName");

            if (string.IsNullOrWhiteSpace(draft.Namespace))
            {
                draft.Namespace = DefaultNamespace;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "EFFECT_NAMESPACE_DEFAULT", "namespace 为空，已自动修复为 BuffSystem。", "Namespace");
            }
            else if (!IsValidNamespace(draft.Namespace))
            {
                result.Add(BuffAuthoringPreflightSeverity.Error, "EFFECT_NAMESPACE_INVALID", "namespace 不是合法 C# 命名空间。", "Namespace");
            }

            if (string.IsNullOrWhiteSpace(draft.TargetFolder))
            {
                draft.TargetFolder = safeSettings.EffectScriptDefaultFolder;
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "EFFECT_FOLDER_DEFAULT", $"目标目录为空，已使用默认目录：{draft.TargetFolder}。", "TargetFolder");
            }

            draft.TargetFolder = NormalizePath(draft.TargetFolder).TrimEnd('/');
            if (!IsAssetPath(draft.TargetFolder))
                result.Add(BuffAuthoringPreflightSeverity.Error, "EFFECT_FOLDER_INVALID", $"目标目录必须位于 Assets 下：{draft.TargetFolder}", "TargetFolder");
            else
                EnsureAssetFolder(draft.TargetFolder, result, "TargetFolder");

            string safeClassName = string.IsNullOrWhiteSpace(draft.EffectClassName) ? DefaultEffectClassName : draft.EffectClassName.Trim();
            draft.TargetFilePath = $"{draft.TargetFolder}/{safeClassName}.cs";
            if (AssetExists(draft.TargetFilePath))
                result.Add(BuffAuthoringPreflightSeverity.Error, "EFFECT_TARGET_EXISTS", $"目标 .cs 文件已存在：{draft.TargetFilePath}。", "TargetFile");

            if (!draft.OnApply && !draft.OnTick && !draft.OnRemove && !draft.OnRefresh && !draft.OnStackChanged)
                result.Add(BuffAuthoringPreflightSeverity.Warning, "EFFECT_NO_CALLBACK", "未选择任何生命周期回调，将生成空 Effect 类。", "Callbacks");

            if (!result.HasError)
                result.Add(BuffAuthoringPreflightSeverity.Info, "EFFECT_PREFLIGHT_PASS", "Effect Preflight 通过，将继续生成 Effect .cs 草稿。", string.Empty);

            return result;
        }

        private static string NormalizeBuffIdMessage(string message)
        {
            if (message.Contains("必须大于 0"))
                return "ConfigId 无效，请点击“重新分配 Buff ID”。";

            if (message.Contains("已被占用"))
                return "ConfigId 已被占用，请点击“重新分配 Buff ID”。";

            return message;
        }

        private static string NormalizeEffectIdMessage(string message)
        {
            if (message.Contains("必须大于 0"))
                return "EffectId 无效，请点击“重新分配 Effect ID”。";

            if (message.Contains("已被占用"))
                return "EffectId 已被占用，请点击“重新分配 Effect ID”。";

            return message;
        }

        private static bool IsValidClassName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            return ClassNameRegex.IsMatch(trimmed) && !CSharpKeywords.Contains(trimmed);
        }

        private static bool IsValidNamespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim();
            if (!NamespaceRegex.IsMatch(trimmed))
                return false;

            string[] parts = trimmed.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (CSharpKeywords.Contains(parts[i]))
                    return false;
            }

            return true;
        }

        private static void EnsureAssetFolder(string folder, BuffAuthoringPreflightResult result, string fieldName)
        {
            string normalized = NormalizePath(folder);
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            try
            {
                Directory.CreateDirectory(normalized);
                AssetDatabase.Refresh();
                result.Add(BuffAuthoringPreflightSeverity.Fixup, "FOLDER_CREATED", $"目录不存在，已自动创建：{normalized}", fieldName);
            }
            catch (Exception exception)
            {
                result.Add(BuffAuthoringPreflightSeverity.Error, "FOLDER_CREATE_FAILED", $"目录创建失败：{exception.Message}", fieldName);
            }
        }

        private static bool AssetExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                return true;

            return File.Exists(Path.GetFullPath(NormalizePath(assetPath)));
        }

        private static bool IsAssetPath(string path)
        {
            return NormalizePath(path).StartsWith("Assets/", StringComparison.Ordinal)
                || string.Equals(NormalizePath(path), "Assets", StringComparison.Ordinal);
        }

        private static string NormalizePath(string path)
        {
            return BuffAuthoringValidationUtility.NormalizeAssetPath(path ?? string.Empty).Trim();
        }
    }
}
