using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace BuffSystem.EditorTesting
{
    internal sealed class BuffSystemTagTestRunner
    {
        internal const string TagDiscoveryCategory = "Tag Discovery";
        internal const string TagConfigCategory = "Tag Config";
        internal const string TagQueryCategory = "Tag Query";
        internal const string MultiTagCategory = "Multi Tag";
        internal const string IsolationCategory = "Target / Source Isolation";
        internal const string StackCategory = "Stack / Refresh / Replace";
        internal const string CleanupCategory = "Remove / Expire Cleanup";
        internal const string BoundaryCategory = "Boundary";

        private const string RuntimeQueryMissingReason = "当前 IBuffSystem / BuffSystemCore 未暴露 live runtime Tag query API；不通过测试新增 runtime API。";

        public BuffSystemTagTestReport RunAll()
        {
            BuffSystemTagCapabilitySnapshot capabilities = DiscoverCapabilities();
            BuffSystemTagTestReport report = BuffSystemTagTestReport.Create();
            report.ApplyCapabilities(capabilities);

            RunDiscoveryTests(report, capabilities);
            RunConfigTests(report, capabilities);
            RunQueryTests(report, capabilities);
            RunIsolationTests(report, capabilities);
            RunStackTests(report, capabilities);
            RunCleanupTests(report, capabilities);
            RunBoundaryTests(report, capabilities);

            report.WriteMarkdown();
            return report;
        }

        private static BuffSystemTagCapabilitySnapshot DiscoverCapabilities()
        {
            BuffSystemTagCapabilitySnapshot capabilities = new BuffSystemTagCapabilitySnapshot();
            CollectTagMembers(typeof(BuffDefinition), capabilities.DefinitionTagMembers);
            CollectTagMembers(typeof(BuffConfigData), capabilities.ConfigTagMembers);
            CollectTagMembers(typeof(BuffConfigDataLoader), capabilities.LoaderTagMembers);
            CollectTagMembers(typeof(TagRegistry), capabilities.LoaderTagMembers);
            CollectTagMembers(typeof(BuffSystemCore), capabilities.RuntimeTagMembers);
            CollectRuntimeQueryMembers(typeof(IBuffSystem), capabilities.PublicRuntimeQueryMembers);
            CollectRuntimeQueryMembers(typeof(BuffSystemCore), capabilities.PublicRuntimeQueryMembers);

            capabilities.HasDefinitionTagField = capabilities.DefinitionTagMembers.Count > 0;
            capabilities.HasConfigTagField = capabilities.ConfigTagMembers.Count > 0;
            capabilities.HasLoaderTagQueryApi = capabilities.LoaderTagMembers.Count > 0;
            capabilities.HasRuntimeTagQueryApi = capabilities.PublicRuntimeQueryMembers.Count > 0;
            capabilities.HasRuntimeTagCleanupSignal = capabilities.HasRuntimeTagQueryApi && capabilities.RuntimeTagMembers.Count > 0;

            capabilities.Notes.Add("BuffConfigData.Tags is authoring/config metadata and is copied by BuffConfigData.CopyTo.");
            capabilities.Notes.Add("BuffConfigData.ToDefinition does not pass Tags into BuffDefinition.");
            capabilities.Notes.Add("BuffDefinition currently has no Tag field or constructor parameter.");
            capabilities.Notes.Add("BuffConfigDataLoader exposes config-level tag lookup such as BuffHasTag / FindBuffsWithTag / FindBuffWithAllTags.");
            capabilities.Notes.Add("IBuffSystem exposes TryGetBuff(target, configId, source) and GetBuffs(target), but no public live runtime Tag query.");
            return capabilities;
        }

        private void RunDiscoveryTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            AddDiscovery(report, "TagDiscovery_RuntimeTagApi_DetectedOrNotSupported", capabilities.HasRuntimeTagQueryApi, Join(capabilities.PublicRuntimeQueryMembers), RuntimeQueryMissingReason, "runtime public Tag query API should be discoverable if supported.", capabilities);
            bool hasDefinitionOrConfigTag = capabilities.HasDefinitionTagField || capabilities.HasConfigTagField;
            AddDiscovery(report, "TagDiscovery_DefinitionTagField_DetectedOrNotSupported", hasDefinitionOrConfigTag, $"BuffDefinition=[{Join(capabilities.DefinitionTagMembers)}], BuffConfigData=[{Join(capabilities.ConfigTagMembers)}]", "BuffDefinition 没有 Tag 字段；仅 BuffConfigData 存在 authoring Tags。", "definition/config Tag field should be discoverable if supported.", capabilities);
            AddDiscovery(report, "TagDiscovery_PublicQueryByTag_DetectedOrNotSupported", capabilities.HasRuntimeTagQueryApi, Join(capabilities.PublicRuntimeQueryMembers), RuntimeQueryMissingReason, "public runtime query by Tag should be discoverable if supported.", capabilities);
            AddDiscovery(report, "TagDiscovery_TagCleanupBehavior_DetectedOrNotSupported", capabilities.HasRuntimeTagCleanupSignal, Join(capabilities.RuntimeTagMembers), "当前没有 live runtime Tag index，因此没有可验证的 runtime Tag cleanup 行为。", "runtime Tag cleanup behavior should be discoverable if supported.", capabilities);
        }

        private void RunConfigTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            RunConfigCase(report, "Tag_Config_SingleTag_CanBeStoredInDefinition", "Single authoring Tag can be stored in BuffConfigData.", "Fire", data => data.Tags.Count == 1 && data.Tags[0] == "Fire", capabilities);
            RunConfigCase(report, "Tag_Config_MultipleTags_CanBeStoredInDefinition", "Multiple authoring Tags can be stored in BuffConfigData.", "Fire,Damage,Dot", data => data.Tags.Count == 3 && data.Tags[2] == "Dot", capabilities);
            RunConfigCase(report, "Tag_Config_EmptyTags_IsValidOrDocumented", "Empty authoring Tags list is stable.", string.Empty, data => data.Tags.Count == 0, capabilities);
            RunConfigCase(report, "Tag_Config_DuplicateTags_NormalizedOrDocumented", "Duplicate authoring Tags remain documented and stable.", "Fire,Fire", data => data.Tags.Count == 2 && data.Tags[0] == data.Tags[1], capabilities);
        }

        private void RunQueryTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            AddRuntimeDependentCase(report, TagQueryCategory, "Tag_Query_SingleTag_ReturnsMatchingBuff", "按 Tag 查询能命中正确 Buff。", capabilities);
            AddRuntimeDependentCase(report, TagQueryCategory, "Tag_Query_WrongTag_ReturnsEmpty", "错误 Tag 不命中。", capabilities);
            AddRuntimeDependentCase(report, TagQueryCategory, "Tag_Query_MultipleBuffs_SameTag_ReturnsAllMatching", "多个 Buff 共享 Tag 时全部命中。", capabilities);
            AddRuntimeDependentCase(report, MultiTagCategory, "Tag_Query_MultipleTags_AnyOrAllSemanticsDocumented", "多 Tag Any / All 语义必须记录。", capabilities);
            AddRuntimeDependentCase(report, TagQueryCategory, "Tag_Query_GetBuffsByTag_DoesNotReturnExpiredBuff", "Expire 后 Tag 查询不返回过期 Buff。", capabilities);
            AddRuntimeDependentCase(report, TagQueryCategory, "Tag_Query_GetBuffsByTag_DoesNotReturnRemovedBuff", "Remove 后 Tag 查询不返回移除 Buff。", capabilities);
        }

        private void RunIsolationTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            AddRuntimeDependentCase(report, IsolationCategory, "Tag_Isolation_SameTagDifferentTargets_QueryTargetIsolated", "同 Tag 不串 target。", capabilities);
            AddRuntimeDependentCase(report, IsolationCategory, "Tag_Isolation_SameTagDifferentSources_SourceSpecificQueryIsolated", "同 Tag 不串 source。", capabilities);
            AddRuntimeDependentCase(report, IsolationCategory, "Tag_Isolation_RemoveOneSource_OtherSourceTagStillVisible", "移除 source A 不影响 source B。", capabilities);
            AddRuntimeDependentCase(report, IsolationCategory, "Tag_Isolation_ClearTarget_OtherTargetTagStillVisible", "Clear target A 不影响 target B。", capabilities);
        }

        private void RunStackTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            AddRuntimeDependentCase(report, StackCategory, "Tag_Stack_Append_DoesNotDuplicateBeyondStackSemantics", "Append 后 Tag 查询数量符合 stack 语义。", capabilities);
            AddRuntimeDependentCase(report, StackCategory, "Tag_Stack_RefreshAll_NotFull_TagStillVisible", "RefreshAll 未满时 Tag 仍可见。", capabilities);
            AddRuntimeDependentCase(report, StackCategory, "Tag_Stack_RefreshAll_WhenFull_TagStillVisible", "RefreshAll 满层时 Tag 仍可见。", capabilities);
            AddRuntimeDependentCase(report, StackCategory, "Tag_Stack_Replace_TagStillVisibleAfterReplacement", "Replace 后 Tag 仍可见。", capabilities);
            AddRuntimeDependentCase(report, StackCategory, "Tag_Stack_MaxStack_TagQueryCountStableOrDocumented", "MaxStack 下 Tag 查询数量稳定或被记录。", capabilities);
        }

        private void RunCleanupTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            AddRuntimeDependentCase(report, CleanupCategory, "Tag_Cleanup_ManualRemove_RemovesFromTagQuery", "手动 Remove 后 Tag 查询不可见。", capabilities);
            AddRuntimeDependentCase(report, CleanupCategory, "Tag_Cleanup_Expire_RemovesFromTagQuery", "Expire 后 Tag 查询不可见。", capabilities);
            AddRuntimeDependentCase(report, CleanupCategory, "Tag_Cleanup_ClearAll_RemovesFromTagQuery", "ClearAll 后 Tag 查询不可见。", capabilities);
            AddRuntimeDependentCase(report, CleanupCategory, "Tag_Cleanup_RemoveMissing_DoesNotAffectOtherTaggedBuffs", "Remove missing 不影响其他 tagged buff。", capabilities);
            AddRuntimeDependentCase(report, CleanupCategory, "Tag_Cleanup_ReAddAfterRemove_TagVisibleAgain", "重新 Add 后 Tag 可再次可见。", capabilities);
        }

        private void RunBoundaryTests(BuffSystemTagTestReport report, BuffSystemTagCapabilitySnapshot capabilities)
        {
            AddRuntimeDependentCase(report, BoundaryCategory, "Tag_Boundary_UnknownTag_ReturnsEmptyOrDocumented", "未知 Tag 返回空或有明确文档。", capabilities);
            AddRuntimeDependentCase(report, BoundaryCategory, "Tag_Boundary_NullOrEmptyTag_HandledOrDocumented", "空 Tag 被处理或有明确文档。", capabilities);
            AddRuntimeDependentCase(report, BoundaryCategory, "Tag_Boundary_DuplicateTags_NotDuplicatedOrDocumented", "重复 Tag 不重复或有明确文档。", capabilities);
            AddRuntimeDependentCase(report, BoundaryCategory, "Tag_Boundary_CaseSensitivity_DocumentedIfStringTag", "string Tag 大小写敏感性有明确记录。", capabilities);
            AddRuntimeDependentCase(report, BoundaryCategory, "Tag_Boundary_LargeTagSet_HandledInMemory", "大量 Tag 集合在内存中稳定处理。", capabilities);
        }

        private static void RunConfigCase(BuffSystemTagTestReport report, string caseName, string expected, string tagsCsv, Func<BuffConfigData, bool> assertion, BuffSystemTagCapabilitySnapshot capabilities)
        {
            if (!capabilities.HasConfigTagField)
            {
                AddNotSupported(report, TagConfigCategory, caseName, expected, "BuffConfigData.Tags field not found.", capabilities);
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int invariantChecks = 0;
            BuffConfigData source = null;
            BuffConfigData copy = null;
            try
            {
                source = ScriptableObject.CreateInstance<BuffConfigData>();
                source.ID = 120900;
                source.Name = "TagConfigSmoke";
                source.Tags = SplitTags(tagsCsv);
                copy = ScriptableObject.CreateInstance<BuffConfigData>();
                source.CopyTo(copy);
                invariantChecks++;
                if (!assertion(copy))
                    throw new InvalidOperationException("BuffConfigData.CopyTo 后 Tags 不符合预期。");

                stopwatch.Stop();
                report.Add(BuffSystemTagTestCaseResult.Passed(TagConfigCategory, caseName, expected, $"sourceTags={source.Tags.Count}, copyTags={copy.Tags.Count}, tags=[{string.Join(",", copy.Tags)}]", invariantChecks, stopwatch.Elapsed.TotalMilliseconds, BuildAvailability(capabilities)));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.Add(BuffSystemTagTestCaseResult.Failed(TagConfigCategory, caseName, expected, copy != null && copy.Tags != null ? $"copyTags={copy.Tags.Count}" : "copyTags=<null>", invariantChecks, stopwatch.Elapsed.TotalMilliseconds, exception, BuildAvailability(capabilities)));
            }
            finally
            {
                if (source != null)
                    UnityEngine.Object.DestroyImmediate(source);
                if (copy != null)
                    UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static void AddDiscovery(BuffSystemTagTestReport report, string caseName, bool found, string actual, string unsupportedReason, string expected, BuffSystemTagCapabilitySnapshot capabilities)
        {
            if (found)
                report.Add(BuffSystemTagTestCaseResult.Passed(TagDiscoveryCategory, caseName, expected, actual, 1, 0, BuildAvailability(capabilities)));
            else
                AddNotSupported(report, TagDiscoveryCategory, caseName, expected, unsupportedReason, capabilities, actual);
        }

        private static void AddNotSupported(BuffSystemTagTestReport report, string category, string caseName, string expected, string reason, BuffSystemTagCapabilitySnapshot capabilities, string actual = "")
        {
            report.Add(BuffSystemTagTestCaseResult.NotSupported(category, caseName, expected, string.IsNullOrEmpty(actual) ? reason : actual, reason, BuildAvailability(capabilities)));
        }

        private static void AddRuntimeDependentCase(BuffSystemTagTestReport report, string category, string caseName, string expected, BuffSystemTagCapabilitySnapshot capabilities)
        {
            if (!capabilities.HasRuntimeTagQueryApi)
            {
                AddNotSupported(report, category, caseName, expected, RuntimeQueryMissingReason, capabilities);
                return;
            }

            InvalidOperationException exception = new InvalidOperationException("检测到 runtime Tag query API，但 Tag runner 尚未适配真实查询调用链。请在后续阶段补真实 runtime Tag case，不能自动视为通过。");
            report.Add(BuffSystemTagTestCaseResult.Failed(category, caseName, expected, "Runtime Tag API detected but no concrete test adapter exists.", 0, 0, exception, BuildAvailability(capabilities)));
        }

        private static void CollectTagMembers(Type type, List<string> results)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            MemberInfo[] members = type.GetMembers(flags);
            for (int i = 0; i < members.Length; i++)
            {
                string name = members[i].Name;
                if (ContainsTagSignal(name))
                    results.Add(type.FullName + "." + name);
            }
        }

        private static void CollectRuntimeQueryMembers(Type type, List<string> results)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                string name = methods[i].Name;
                if (ContainsTagSignal(name) && (name.IndexOf("Get", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Find", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Query", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Has", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Try", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    results.Add(type.FullName + "." + name);
                }
            }
        }

        private static bool ContainsTagSignal(string name)
        {
            return name.IndexOf("Tag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("BuffTag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("TagId", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("TagMask", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("BuffCategory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Mutex", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Exclusive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Dispel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Cleanse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<string> SplitTags(string tagsCsv)
        {
            List<string> tags = new List<string>();
            if (string.IsNullOrEmpty(tagsCsv))
                return tags;

            string[] values = tagsCsv.Split(',');
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    tags.Add(values[i]);
            }

            return tags;
        }

        private static string BuildAvailability(BuffSystemTagCapabilitySnapshot capabilities)
        {
            return capabilities.HasRuntimeTagQueryApi ? "RuntimeTagQuery=Found" : "RuntimeTagQuery=NotFound; ConfigTag=AuthoringOnly";
        }

        private static string Join(List<string> values)
        {
            if (values == null || values.Count == 0)
                return "<none>";

            return string.Join("; ", values);
        }
    }

    internal sealed class BuffSystemTagCapabilitySnapshot
    {
        public readonly List<string> DefinitionTagMembers = new List<string>();
        public readonly List<string> ConfigTagMembers = new List<string>();
        public readonly List<string> LoaderTagMembers = new List<string>();
        public readonly List<string> RuntimeTagMembers = new List<string>();
        public readonly List<string> PublicRuntimeQueryMembers = new List<string>();
        public readonly List<string> Notes = new List<string>();

        public bool HasDefinitionTagField;
        public bool HasConfigTagField;
        public bool HasLoaderTagQueryApi;
        public bool HasRuntimeTagQueryApi;
        public bool HasRuntimeTagCleanupSignal;
    }
}
