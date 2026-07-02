using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// BuffCandidateGraph 鍒?Buff / 涓?Effect 鑽夌鐨?Editor-only 鐢熸垚鏈嶅姟銆?    /// 浠呭湪鐢ㄦ埛鐐瑰嚮 Hub 鐢熸垚鎸夐挳鍚庡啓鍏ヨ崏绋挎枃浠讹紱涓嶄細鑷姩娉ㄥ唽 Effect锛屼笉淇敼 whitelist锛屼笉杩涘叆 runtime銆?    /// </summary>
    internal static class BuffGraphGenerateService
    {
        private const string DefaultBuffName = "NewBuff";
        private const string DefaultEffectClassName = "NewBuffEffect";
        private const string DefaultNamespace = "BuffSystem";

        internal static bool BuildPlan(BuffCandidateGraph graph, out BuffGraphGeneratePlan plan)
        {
            plan = new BuffGraphGeneratePlan
            {
                Graph = graph,
                WillGenerateEffect = true,
                WillCreateBuff = true
            };

            if (graph == null)
            {
                plan.Errors.Add("No BuffCandidateGraph selected.");
                return false;
            }

            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            plan.GraphAssetPath = AssetDatabase.GetAssetPath(graph);
            plan.GraphGuid = string.IsNullOrWhiteSpace(plan.GraphAssetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(plan.GraphAssetPath);

            ValidateRequiredGraphNodes(graph, plan);
            BuildBuffPart(graph, settings, plan);
            BuildEffectPart(graph, settings, plan);
            BuildEffectCodegenPart(graph, plan);

            if (!plan.HasError)
                plan.Infos.Add("Graph Generate Plan built. Preflight will run before writing files.");

            return !plan.HasError;
        }

        internal static bool TryPreviewCompositeEffectCode(
            BuffCandidateGraph graph,
            out BuffGraphGeneratePlan generatePlan,
            out BuffGraphCompositeEffectPlan compositePlan,
            out string code,
            out string error)
        {
            code = string.Empty;
            error = string.Empty;

            BuildPlan(graph, out generatePlan);
            ApplyCompositePreviewEffectIdFallback(generatePlan);
            BuffGraphCompositeEffectPlanBuilder.TryBuild(graph, generatePlan, out compositePlan, out string buildError);
            if (compositePlan == null)
            {
                error = string.IsNullOrWhiteSpace(buildError) ? "CompositeEffect 预览计划构建失败。" : buildError;
                return false;
            }

            AddGeneratePlanIssues(generatePlan, compositePlan);
            AddUnique(compositePlan.Warnings, "CompositeEffect 预览不会写入 .cs 文件，不会写 ID Registry，也不会自动注册 Effect。");
            AddUnique(compositePlan.Infos, "预览阶段不会占用 EffectId；真实生成阶段才会分配 / 写入。");

            if (compositePlan.HasErrors)
            {
                error = string.Join("\n", compositePlan.Errors);
                return false;
            }

            if (!BuffGraphCompositeEffectEmitter.TryEmit(compositePlan, out code, out string emitError))
            {
                if (!string.IsNullOrWhiteSpace(emitError))
                    AddUnique(compositePlan.Errors, emitError);

                error = string.IsNullOrWhiteSpace(emitError) ? "CompositeEffect 代码预览生成失败。" : emitError;
                return false;
            }

            return true;
        }

        internal static BuffGraphGenerateReport CreatePrimaryEffectDraft(BuffGraphGeneratePlan plan)
        {
            BuffGraphGenerateReport report = CreateReport();
            if (!PrepareEffect(plan, report))
                return report;

            if (!WriteEffectDraft(plan, report))
                return report;

            AssetDatabase.Refresh();
            report.EffectCreated = true;
            report.EffectPath = plan.EffectScriptPath;
            bool effectRegistrySucceeded = TryUpsertEffectRegistry(plan, out string effectRegistryMessage);
            report.RegistryMessage = effectRegistryMessage;
            TryAutoRegisterEffectToBootstrap(plan, report, effectRegistrySucceeded);
            report.Infos.Add("Please review generated code, implement action logic, and validate before production use.");
            return report;
        }

        internal static BuffGraphGenerateReport CreateCompositeEffectDraft(BuffGraphGeneratePlan plan)
        {
            BuffGraphGenerateReport report = CreateReport();
            if (!PrepareCompositeEffect(plan, report, out BuffGraphCompositeEffectPlan compositePlan, out string source))
                return report;

            if (!WriteCompositeEffectDraft(compositePlan, source, report))
                return report;

            AssetDatabase.Refresh();
            report.CompositeEffectCreated = true;
            report.CompositeEffectId = compositePlan.CompositeEffectId;
            report.CompositeEffectClassName = compositePlan.CompositeEffectClassName;
            report.CompositeEffectPath = compositePlan.TargetFilePath;
            bool effectRegistrySucceeded = TryUpsertCompositeEffectRegistry(plan, compositePlan, out string effectRegistryMessage);
            report.RegistryMessage = effectRegistryMessage;
            TryAutoRegisterCompositeEffectToBootstrap(compositePlan, report, effectRegistrySucceeded);
            report.Infos.Add("Next: wait for Unity compile, run Validator / Runner / scene validation, then request whitelist approval if needed.");
            return report;
        }

        internal static BuffGraphGenerateReport CreateBuffAndCompositeEffectDraft(BuffGraphGeneratePlan plan)
        {
            BuffGraphGenerateReport report = CreateReport();
            if (!PrepareCompositeEffect(plan, report, out BuffGraphCompositeEffectPlan compositePlan, out string source))
                return report;

            if (!PrepareBuff(plan, report))
                return report;

            if (!WriteCompositeEffectDraft(compositePlan, source, report))
                return report;

            AssetDatabase.Refresh();
            report.CompositeEffectCreated = true;
            report.CompositeEffectId = compositePlan.CompositeEffectId;
            report.CompositeEffectClassName = compositePlan.CompositeEffectClassName;
            report.CompositeEffectPath = compositePlan.TargetFilePath;

            bool effectRegistrySucceeded = TryUpsertCompositeEffectRegistry(plan, compositePlan, out string effectRegistryMessage);
            report.RegistryMessage = effectRegistryMessage;
            bool bootstrapRegistered = TryAutoRegisterCompositeEffectToBootstrap(compositePlan, report, effectRegistrySucceeded);
            if (!bootstrapRegistered)
            {
                report.Warnings.Add("未创建 BuffConfigData：CompositeEffect 尚未完成 Bootstrap 自动注册，避免 Buff 指向未注册 Effect。");
                return report;
            }

            if (!WriteBuffDraft(plan, report))
            {
                report.Warnings.Add("CompositeEffect .cs、Effect ID Registry 和 Bootstrap auto registration 已保留；Buff 草稿创建失败，请按报告手动清理。");
                return report;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.BuffCreated = true;
            report.BuffAssetPath = plan.BuffConfigAssetPath;
            ApplyBuffReportFields(report, plan);
            bool buffRegistrySucceeded = TryUpsertBuffRegistry(plan, out string buffRegistryMessage);
            report.RegistryMessage = effectRegistryMessage + "\n" + buffRegistryMessage;

            if (!buffRegistrySucceeded)
                report.Warnings.Add("BuffConfigData 已创建，但 Buff ID Registry 写入失败；请按报告检查并手动补录或清理。");

            report.Infos.Add("一键流程已完成：BuffConfigData.EffectId 指向 CompositeEffectId。");
            report.Infos.Add("Next: wait for Unity compile, run Validator / Runner / scene validation, then request whitelist approval if needed.");
            return report;
        }

        internal static BuffGraphGenerateReport CreateBuffDraft(BuffGraphGeneratePlan plan)
        {
            BuffGraphGenerateReport report = CreateReport();
            if (!PrepareBuff(plan, report))
                return report;

            if (!WriteBuffDraft(plan, report))
                return report;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.BuffCreated = true;
            report.BuffAssetPath = plan.BuffConfigAssetPath;
            ApplyBuffReportFields(report, plan);
            TryUpsertBuffRegistry(plan, out string buffRegistryMessage);
            report.RegistryMessage = buffRegistryMessage;
            report.Infos.Add("Please run Validator / Runner and request approval before whitelist changes.");
            return report;
        }

        internal static BuffGraphGenerateReport CreateBuffAndPrimaryEffectDraft(BuffGraphGeneratePlan plan)
        {
            BuffGraphGenerateReport report = CreateReport();
            if (!PrepareEffect(plan, report))
                return report;

            if (!PrepareBuff(plan, report))
                return report;

            if (!WriteEffectDraft(plan, report))
                return report;

            AssetDatabase.Refresh();
            report.EffectCreated = true;
            report.EffectPath = plan.EffectScriptPath;
            bool effectRegistrySucceeded = TryUpsertEffectRegistry(plan, out string effectRegistryMessage);

            if (!WriteBuffDraft(plan, report))
            {
                report.Warnings.Add("Effect draft was generated, but Buff draft creation failed. Please inspect and clean up manually if needed.");
                report.RegistryMessage = effectRegistryMessage;
                return report;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.BuffCreated = true;
            report.BuffAssetPath = plan.BuffConfigAssetPath;
            ApplyBuffReportFields(report, plan);
            bool buffRegistrySucceeded = TryUpsertBuffRegistry(plan, out string buffRegistryMessage);
            report.RegistryMessage = effectRegistryMessage + "\n" + buffRegistryMessage;
            TryAutoRegisterEffectToBootstrap(plan, report, effectRegistrySucceeded && buffRegistrySucceeded);
            report.Infos.Add("Next: review generated code, implement action logic, run Validator / Runner / scene validation, then request whitelist approval if needed.");
            return report;
        }

        private static void ApplyBuffReportFields(BuffGraphGenerateReport report, BuffGraphGeneratePlan plan)
        {
            report.BuffConfigId = plan.BuffConfigId;
            report.BuffName = plan.BuffName;
            report.BuffEffectId = plan.BuffDraft.EffectId;
        }

        private static BuffGraphGenerateReport CreateReport()
        {
            BuffGraphGenerateReport report = new BuffGraphGenerateReport();
            report.Infos.Add("Graph Generate may maintain Bootstrap auto block when enabled, but it never changes whitelist or runtime.");
            report.Infos.Add("Generated Effect can call valid ScriptActionNode actions; gameplay logic still belongs in Execute(in context).");
            return report;
        }

        private static void ValidateRequiredGraphNodes(BuffCandidateGraph graph, BuffGraphGeneratePlan plan)
        {
            BuffCandidateStartNode start = graph.FindSingleNode<BuffCandidateStartNode>();
            BuffShapeNode shape = graph.FindSingleNode<BuffShapeNode>();
            EffectCompositionRootNode effectRoot = graph.FindSingleNode<EffectCompositionRootNode>();
            EffectBindingNode legacyEffect = graph.FindSingleNode<EffectBindingNode>();

            if (start == null)
                plan.Errors.Add("Graph requires BuffCandidateStartNode.");

            if (shape == null)
                plan.Errors.Add("Graph requires BuffShapeNode.");

            if (graph.FindNodes<EffectNode>().Count == 0 && legacyEffect == null && effectRoot == null)
                plan.Errors.Add("Graph requires EffectCompositionRootNode, EffectNode, or EffectBindingNode.");
        }

        private static void BuildBuffPart(BuffCandidateGraph graph, BuffAuthoringHubSettingsData settings, BuffGraphGeneratePlan plan)
        {
            if (!BuffCandidateGraphBridge.TryBuildCreateBuffDraft(graph, out BuffCandidateCreateBuffDraft draft, out string warning))
            {
                plan.Errors.Add(warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(warning))
                plan.Warnings.Add(warning);

            if (draft.ConfigId <= 0 || BuffAuthoringIdService.ShouldReplaceBuffConfigId(draft.ConfigId, settings))
            {
                if (settings.AutoAllocateIds)
                {
                    int oldId = draft.ConfigId;
                    draft.ConfigId = BuffAuthoringIdService.GetNextAvailableBuffConfigId(settings);
                    plan.Infos.Add($"Graph ConfigId={oldId} was invalid or occupied. Auto allocated {draft.ConfigId}.");
                }
                else
                {
                    plan.Errors.Add("Buff ConfigId is invalid or occupied, and AutoAllocateIds is disabled.");
                }
            }

            if (string.IsNullOrWhiteSpace(draft.BuffName))
                draft.BuffName = BuildSafeBuffName(graph);

            draft.BuffName = BuffAuthoringValidationUtility.MakeSafeFileName(draft.BuffName, DefaultBuffName);
            plan.BuffDraft = draft;
            plan.BuffConfigId = draft.ConfigId;
            plan.BuffName = draft.BuffName;
            plan.BuffDescription = draft.Description ?? string.Empty;
            plan.BuffConfigAssetPath = $"{settings.BuffConfigDataDefaultFolder.TrimEnd('/', '\\')}/{draft.ConfigId}_{draft.BuffName}.asset";
        }

        private static void BuildEffectPart(BuffCandidateGraph graph, BuffAuthoringHubSettingsData settings, BuffGraphGeneratePlan plan)
        {
            if (!BuffCandidateGraphBridge.TryBuildEffectTemplateDraft(graph, out BuffCandidateEffectTemplateDraft draft, out string warning))
            {
                plan.Errors.Add(warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(warning))
                plan.Warnings.Add(warning);

            string effectName = GetPrimaryEffectName(graph);
            if (string.IsNullOrWhiteSpace(effectName))
                effectName = draft.EffectClassName;

            if (draft.EffectId <= 0 || BuffAuthoringIdService.ShouldReplaceEffectId(draft.EffectId, settings))
            {
                if (settings.AutoAllocateIds)
                {
                    int oldId = draft.EffectId;
                    draft.EffectId = BuffAuthoringIdService.GetNextAvailableEffectId(settings);
                    plan.Infos.Add($"Graph EffectId={oldId} was invalid or occupied. Auto allocated {draft.EffectId}.");
                }
                else
                {
                    plan.Errors.Add("EffectId is invalid or occupied, and AutoAllocateIds is disabled.");
                }
            }

            if (string.IsNullOrWhiteSpace(draft.EffectClassName))
                draft.EffectClassName = BuildEffectClassName(effectName, graph.name);

            plan.EffectId = draft.EffectId;
            plan.EffectName = string.IsNullOrWhiteSpace(effectName) ? draft.EffectClassName : effectName.Trim();
            plan.EffectClassName = draft.EffectClassName.Trim();
            plan.EffectNamespace = DefaultNamespace;
            plan.EffectTargetFolder = settings.EffectScriptDefaultFolder.TrimEnd('/', '\\');
            plan.EffectScriptPath = $"{plan.EffectTargetFolder}/{plan.EffectClassName}.cs";

            if (plan.BuffDraft.HasAnyValue)
            {
                BuffCandidateCreateBuffDraft buffDraft = plan.BuffDraft;
                buffDraft.EffectId = plan.EffectId;
                plan.BuffDraft = buffDraft;
            }
        }

        private static void BuildEffectCodegenPart(BuffCandidateGraph graph, BuffGraphGeneratePlan plan)
        {
            BuffGraphEffectCodegenRequest request = new BuffGraphEffectCodegenRequest
            {
                EffectId = plan.EffectId,
                EffectClassName = plan.EffectClassName,
                Namespace = plan.EffectNamespace,
                TargetFolder = plan.EffectTargetFolder,
                TargetFilePath = plan.EffectScriptPath,
                OnApply = false,
                OnTick = false,
                OnRemove = false,
                OnRefresh = false,
                OnStackChanged = false
            };

            BuffGraphEffectCodegenBuilder.TryBuild(graph, request, out BuffGraphEffectCodegenPlan codegenPlan);
            plan.EffectCodegenPlan = codegenPlan;
            plan.SelectedEffectNodeSummary = codegenPlan.SelectedEffectNodeSummary;
            plan.HasMultipleEffectNodes = codegenPlan.HasMultipleEffectNodes;
            plan.Errors.AddRange(codegenPlan.Errors);
            plan.Warnings.AddRange(codegenPlan.Warnings);

            if (codegenPlan.EffectId > 0)
                plan.EffectId = codegenPlan.EffectId;

            if (!string.IsNullOrWhiteSpace(codegenPlan.EffectClassName))
                plan.EffectClassName = codegenPlan.EffectClassName;

            if (!string.IsNullOrWhiteSpace(plan.EffectTargetFolder) && !string.IsNullOrWhiteSpace(plan.EffectClassName))
            {
                plan.EffectScriptPath = $"{plan.EffectTargetFolder}/{plan.EffectClassName}.cs";
                codegenPlan.TargetFilePath = plan.EffectScriptPath;
            }

            if (plan.BuffDraft.HasAnyValue)
            {
                BuffCandidateCreateBuffDraft buffDraft = plan.BuffDraft;
                buffDraft.EffectId = plan.EffectId;
                plan.BuffDraft = buffDraft;
            }
        }

        private static bool PrepareEffect(BuffGraphGeneratePlan plan, BuffGraphGenerateReport report)
        {
            if (plan == null)
            {
                report.Errors.Add("Graph Generate Plan 为空。");
                return false;
            }

            AddPlanIssues(plan, report);
            if (report.HasError)
                return false;

            BuffAuthoringEffectPreflightDraft draft = new BuffAuthoringEffectPreflightDraft
            {
                EffectId = plan.EffectId,
                EffectClassName = plan.EffectClassName,
                TargetFolder = plan.EffectTargetFolder,
                Namespace = plan.EffectNamespace,
                OnApply = false,
                OnTick = false,
                OnRemove = false,
                OnRefresh = false,
                OnStackChanged = false
            };

            BuffAuthoringPreflightResult preflight = BuffAuthoringPreflightValidator.RunEffectPreflight(draft, BuffAuthoringHubSettings.Load());
            ApplyEffectPreflight(plan, draft);
            AppendPreflightIssues(preflight, report);
            if (report.HasError)
                return false;

            BuildEffectCodegenPart(plan.Graph, plan);
            AddGraphCodegenIssues(plan, report);
            return !report.HasError;
        }

        private static bool PrepareCompositeEffect(
            BuffGraphGeneratePlan plan,
            BuffGraphGenerateReport report,
            out BuffGraphCompositeEffectPlan compositePlan,
            out string source)
        {
            compositePlan = null;
            source = string.Empty;
            if (plan == null)
            {
                report.Errors.Add("Graph Generate Plan 为空。");
                return false;
            }

            ApplyCompositeGenerationIdentity(plan);
            AddPlanIssues(plan, report);
            if (report.HasError)
                return false;

            BuffGraphCompositeEffectPlanBuilder.TryBuild(plan.Graph, plan, out compositePlan, out string buildError);
            if (compositePlan == null)
            {
                report.Errors.Add(string.IsNullOrWhiteSpace(buildError) ? "CompositeEffect 生成计划构建失败。" : buildError);
                return false;
            }

            AddCompositePlanIssues(compositePlan, report);
            if (report.HasError)
                return false;

            if (!BuffGraphCompositeEffectEmitter.TryEmit(compositePlan, out source, out string emitError))
            {
                if (!string.IsNullOrWhiteSpace(emitError))
                    AddUnique(report.Errors, emitError);

                AddCompositePlanIssues(compositePlan, report);
                return false;
            }

            report.Infos.Add(compositePlan.BuildActionPreview());
            report.Infos.Add("真实生成只注册最终 CompositeEffect，不注册子 EffectNode。");
            return true;
        }

        private static bool PrepareBuff(BuffGraphGeneratePlan plan, BuffGraphGenerateReport report)
        {
            if (plan == null)
            {
                report.Errors.Add("Graph Generate Plan is null.");
                return false;
            }

            AddPlanIssues(plan, report);
            if (report.HasError)
                return false;

            BuffCandidateCreateBuffDraft source = plan.BuffDraft;
            source.EffectId = plan.EffectId;
            BuffAuthoringBuffPreflightDraft draft = new BuffAuthoringBuffPreflightDraft
            {
                ConfigId = source.ConfigId,
                BuffName = source.BuffName,
                SaveFolder = BuffAuthoringHubSettings.Load().BuffConfigDataDefaultFolder,
                BuffType = source.BuffType,
                TriggerType = source.TriggerType,
                ParallelStorageMode = source.ParallelStorageMode,
                Unlimited = source.Unlimited,
                MaxStack = source.MaxStack,
                Duration = source.Duration,
                TickTime = source.TickTime,
                StackUpPolicy = source.StackUpPolicy,
                StackDownPolicy = source.StackDownPolicy,
                EffectId = source.EffectId
            };

            BuffAuthoringPreflightResult preflight = BuffAuthoringPreflightValidator.RunBuffPreflight(draft, BuffAuthoringHubSettings.Load());
            ApplyBuffPreflight(plan, draft);
            AppendPreflightIssues(preflight, report);
            return !report.HasError;
        }

        private static void ApplyEffectPreflight(BuffGraphGeneratePlan plan, BuffAuthoringEffectPreflightDraft draft)
        {
            plan.EffectId = draft.EffectId;
            plan.EffectClassName = draft.EffectClassName;
            plan.EffectTargetFolder = draft.TargetFolder;
            plan.EffectNamespace = draft.Namespace;
            plan.EffectScriptPath = draft.TargetFilePath;

            if (plan.BuffDraft.HasAnyValue)
            {
                BuffCandidateCreateBuffDraft buffDraft = plan.BuffDraft;
                buffDraft.EffectId = plan.EffectId;
                plan.BuffDraft = buffDraft;
            }
        }

        private static void ApplyCompositeGenerationIdentity(BuffGraphGeneratePlan plan)
        {
            if (plan == null || plan.Graph == null)
                return;

            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            EffectCompositionRootNode effectRoot = plan.Graph.FindSingleNode<EffectCompositionRootNode>();
            int rootEffectId = effectRoot != null ? effectRoot.FinalEffectId : 0;

            if (rootEffectId > 0)
            {
                plan.EffectId = rootEffectId;
                RemoveEffectIdAllocationMessages(plan);
            }
            else if (settings.AutoAllocateIds)
            {
                int oldId = plan.EffectId;
                plan.EffectId = BuffAuthoringIdService.GetNextAvailableEffectId(settings);
                RemoveEffectIdAllocationError(plan);
                RemoveEffectIdAllocationMessages(plan);
                AddUnique(plan.Infos, $"CompositeEffectId 缺失，已自动分配正式段 EffectId={plan.EffectId}；原 EffectId={oldId}。");
            }
            else
            {
                plan.EffectId = 0;
                AddUnique(plan.Errors, "CompositeEffectId <= 0 且 AutoAllocateIds 已关闭，已阻止真实生成。");
            }

            string className = effectRoot != null && !string.IsNullOrWhiteSpace(effectRoot.FinalEffectClassName)
                ? effectRoot.FinalEffectClassName.Trim()
                : BuildCompositeEffectClassName(plan.BuffName, plan.Graph.name);

            plan.EffectClassName = className;
            plan.EffectName = effectRoot != null && !string.IsNullOrWhiteSpace(effectRoot.FinalEffectName)
                ? effectRoot.FinalEffectName.Trim()
                : className;
            plan.EffectTargetFolder = settings.EffectScriptDefaultFolder.TrimEnd('/', '\\');
            plan.EffectScriptPath = $"{plan.EffectTargetFolder}/{plan.EffectClassName}.cs";

            if (plan.BuffDraft.HasAnyValue)
            {
                BuffCandidateCreateBuffDraft buffDraft = plan.BuffDraft;
                buffDraft.EffectId = plan.EffectId;
                plan.BuffDraft = buffDraft;
            }

            BuffAuthoringIdValidationResult idValidation = BuffAuthoringIdService.ValidateEffectId(plan.EffectId, settings);
            for (int i = 0; i < idValidation.Errors.Count; i++)
                AddUnique(plan.Errors, idValidation.Errors[i]);

            for (int i = 0; i < idValidation.Warnings.Count; i++)
                AddUnique(plan.Warnings, idValidation.Warnings[i]);
        }

        private static void ApplyBuffPreflight(BuffGraphGeneratePlan plan, BuffAuthoringBuffPreflightDraft draft)
        {
            BuffCandidateCreateBuffDraft buffDraft = plan.BuffDraft;
            buffDraft.ConfigId = draft.ConfigId;
            buffDraft.BuffName = draft.BuffName;
            buffDraft.BuffType = draft.BuffType;
            buffDraft.TriggerType = draft.TriggerType;
            buffDraft.ParallelStorageMode = draft.ParallelStorageMode;
            buffDraft.Unlimited = draft.Unlimited;
            buffDraft.MaxStack = draft.MaxStack;
            buffDraft.Duration = draft.Duration;
            buffDraft.TickTime = draft.TickTime;
            buffDraft.StackUpPolicy = draft.StackUpPolicy;
            buffDraft.StackDownPolicy = draft.StackDownPolicy;
            buffDraft.EffectId = draft.EffectId;
            plan.BuffDraft = buffDraft;
            plan.BuffConfigId = draft.ConfigId;
            plan.BuffName = draft.BuffName;
            plan.BuffConfigAssetPath = draft.TargetAssetPath;
        }

        private static bool WriteEffectDraft(BuffGraphGeneratePlan plan, BuffGraphGenerateReport report)
        {
            try
            {
                Directory.CreateDirectory(plan.EffectTargetFolder);
                string summary = string.IsNullOrWhiteSpace(plan.EffectName) ? "TODO: Fill effect description." : plan.EffectName;
                string source = BuffGraphEffectCodegenEmitter.Emit(plan.EffectCodegenPlan, summary);
                AddGraphCodegenIssues(plan, report);
                if (report.HasError)
                    return false;

                report.Infos.Add(plan.EffectCodegenPlan.BuildActionPreview());
                File.WriteAllText(ToAbsolutePath(plan.EffectScriptPath), source, Encoding.UTF8);
                return true;
            }
            catch (Exception exception)
            {
                report.Errors.Add("Effect 草稿生成失败：" + exception.Message);
                return false;
            }
        }

        private static bool WriteCompositeEffectDraft(BuffGraphCompositeEffectPlan plan, string source, BuffGraphGenerateReport report)
        {
            try
            {
                Directory.CreateDirectory(plan.TargetFolder);
                File.WriteAllText(ToAbsolutePath(plan.TargetFilePath), source, Encoding.UTF8);
                return true;
            }
            catch (Exception exception)
            {
                report.Errors.Add("CompositeEffect 草稿生成失败：" + exception.Message);
                return false;
            }
        }

        private static bool WriteBuffDraft(BuffGraphGeneratePlan plan, BuffGraphGenerateReport report)
        {
            try
            {
                string directory = Path.GetDirectoryName(plan.BuffConfigAssetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                BuffCandidateCreateBuffDraft draft = plan.BuffDraft;
                BuffConfigData asset = ScriptableObject.CreateInstance<BuffConfigData>();
                asset.ID = draft.ConfigId;
                asset.Name = draft.BuffName;
                asset.Description = draft.Description;
                asset.BuffType = draft.BuffType;
                asset.BuffTriggerType = draft.TriggerType;
                asset.ParallelStorageMode = draft.ParallelStorageMode;
                asset.Unlimited = draft.Unlimited;
                asset.MaxStack = draft.Unlimited ? 1 : draft.MaxStack;
                asset.Duration = draft.Duration;
                asset.TickTime = draft.TickTime;
                asset.ParallelStackUpPolicy = draft.StackUpPolicy;
                asset.ParallelStackDownPolicy = draft.StackDownPolicy;
                asset.EffectId = draft.EffectId;
                AssetDatabase.CreateAsset(asset, plan.BuffConfigAssetPath);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                return true;
            }
            catch (Exception exception)
            {
                report.Errors.Add("Buff 草稿创建失败：" + exception.Message);
                return false;
            }
        }

        private static bool TryUpsertEffectRegistry(BuffGraphGeneratePlan plan, out string message)
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            bool success = BuffAuthoringIdRegistryAllocator.UpsertGeneratedEffectEntry(
                settings.IdRegistryJsonPath,
                plan.EffectId,
                string.IsNullOrWhiteSpace(plan.EffectName) ? plan.EffectClassName : plan.EffectName,
                plan.EffectClassName,
                plan.Graph,
                plan.EffectScriptPath,
                out string error);

            message = success ? $"Effect ID Registry 已更新：{settings.IdRegistryJsonPath}" : $"Warning：Effect ID Registry 写入失败：{error}";
            return success;
        }

        private static bool TryUpsertCompositeEffectRegistry(BuffGraphGeneratePlan generatePlan, BuffGraphCompositeEffectPlan compositePlan, out string message)
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            bool success = BuffAuthoringIdRegistryAllocator.UpsertGeneratedEffectEntry(
                settings.IdRegistryJsonPath,
                compositePlan.CompositeEffectId,
                string.IsNullOrWhiteSpace(compositePlan.CompositeEffectName) ? compositePlan.CompositeEffectClassName : compositePlan.CompositeEffectName,
                compositePlan.CompositeEffectClassName,
                generatePlan.Graph,
                compositePlan.TargetFilePath,
                out string error);

            message = success ? $"CompositeEffect ID Registry 已更新：{settings.IdRegistryJsonPath}" : $"Warning：CompositeEffect ID Registry 写入失败：{error}";
            return success;
        }

        private static bool TryUpsertBuffRegistry(BuffGraphGeneratePlan plan, out string message)
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            bool success = BuffAuthoringIdRegistryAllocator.UpsertGeneratedBuffEntry(
                settings.IdRegistryJsonPath,
                plan.BuffConfigId,
                plan.BuffName,
                plan.Graph,
                plan.BuffConfigAssetPath,
                out string error);

            message = success ? $"Buff ID Registry 已更新：{settings.IdRegistryJsonPath}" : $"Warning：Buff ID Registry 写入失败：{error}";
            return success;
        }

        private static void TryAutoRegisterEffectToBootstrap(
            BuffGraphGeneratePlan plan,
            BuffGraphGenerateReport report,
            bool idRegistrySucceeded)
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            string snippet = BuildRegistrySnippet(plan);
            if (!settings.AutoRegisterEffectsToBootstrap)
            {
                report.Infos.Add("Bootstrap 自动注册已关闭，请按需手动注册：" + snippet);
                return;
            }

            if (!idRegistrySucceeded)
            {
                report.Warnings.Add("ID Registry 写入未完全成功，已跳过 Bootstrap 自动注册。可手动注册：" + snippet);
                return;
            }

            bool success = BuffEffectBootstrapAutoRegistryPatcher.TryUpsertAutoRegistration(
                plan.EffectId,
                plan.EffectClassName,
                out BuffEffectBootstrapAutoRegistryReport autoReport);

            string message = autoReport.ToDisplayText();
            if (success)
            {
                report.Infos.Add(message);
                return;
            }

            report.Warnings.Add("Bootstrap 自动注册失败，已保留生成结果，不回滚 ID Registry。");
            report.Warnings.Add(message);
            report.Warnings.Add("可手动注册片段：" + snippet);
        }

        private static bool TryAutoRegisterCompositeEffectToBootstrap(
            BuffGraphCompositeEffectPlan plan,
            BuffGraphGenerateReport report,
            bool idRegistrySucceeded)
        {
            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            string snippet = BuildCompositeRegistrySnippet(plan);
            report.ManualRegistrySnippet = snippet;
            if (!settings.AutoRegisterEffectsToBootstrap)
            {
                report.Infos.Add("Bootstrap 自动注册已关闭，请按需手动注册：" + snippet);
                return false;
            }

            if (!idRegistrySucceeded)
            {
                report.Warnings.Add("CompositeEffect ID Registry 写入未成功，已跳过 Bootstrap 自动注册。可手动注册：" + snippet);
                return false;
            }

            bool success = BuffEffectBootstrapAutoRegistryPatcher.TryUpsertAutoRegistration(
                plan.CompositeEffectId,
                plan.CompositeEffectClassName,
                out BuffEffectBootstrapAutoRegistryReport autoReport);

            string message = autoReport.ToDisplayText();
            if (success)
            {
                report.Infos.Add("CompositeEffect 自动注册状态：成功。");
                report.Infos.Add(message);
                return true;
            }

            report.Warnings.Add("CompositeEffect 自动注册状态：失败。已保留 .cs 和 ID Registry，不执行回滚。");
            report.Warnings.Add(message);
            report.Warnings.Add("可手动注册片段：" + snippet);
            return false;
        }

        private static string BuildRegistrySnippet(BuffGraphGeneratePlan plan)
        {
            return $"registry.Register({plan.EffectId}, new {plan.EffectClassName}());";
        }

        private static string BuildCompositeRegistrySnippet(BuffGraphCompositeEffectPlan plan)
        {
            return $"registry.Register({plan.CompositeEffectId}, new {plan.CompositeEffectClassName}());";
        }

        private static void AddPlanIssues(BuffGraphGeneratePlan plan, BuffGraphGenerateReport report)
        {
            for (int i = 0; i < plan.Errors.Count; i++)
                AddUnique(report.Errors, plan.Errors[i]);

            for (int i = 0; i < plan.Warnings.Count; i++)
                AddUnique(report.Warnings, plan.Warnings[i]);

            for (int i = 0; i < plan.Infos.Count; i++)
                AddUnique(report.Infos, plan.Infos[i]);
        }

        private static void AddGeneratePlanIssues(BuffGraphGeneratePlan generatePlan, BuffGraphCompositeEffectPlan compositePlan)
        {
            if (generatePlan == null || compositePlan == null)
                return;

            for (int i = 0; i < generatePlan.Errors.Count; i++)
                AddUnique(compositePlan.Errors, "GRAPH_GENERATE: " + generatePlan.Errors[i]);

            for (int i = 0; i < generatePlan.Warnings.Count; i++)
                AddUnique(compositePlan.Warnings, "GRAPH_GENERATE: " + generatePlan.Warnings[i]);

            for (int i = 0; i < generatePlan.Infos.Count; i++)
                AddUnique(compositePlan.Infos, "GRAPH_GENERATE: " + generatePlan.Infos[i]);
        }

        private static void AddCompositePlanIssues(BuffGraphCompositeEffectPlan plan, BuffGraphGenerateReport report)
        {
            if (plan == null)
                return;

            for (int i = 0; i < plan.Errors.Count; i++)
                AddUnique(report.Errors, "COMPOSITE: " + plan.Errors[i]);

            for (int i = 0; i < plan.Warnings.Count; i++)
                AddUnique(report.Warnings, "COMPOSITE: " + plan.Warnings[i]);

            for (int i = 0; i < plan.Infos.Count; i++)
                AddUnique(report.Infos, "COMPOSITE: " + plan.Infos[i]);
        }

        private static void ApplyCompositePreviewEffectIdFallback(BuffGraphGeneratePlan plan)
        {
            if (plan == null)
                return;

            BuffAuthoringHubSettingsData settings = BuffAuthoringHubSettings.Load();
            if (plan.EffectId > 0 && !BuffAuthoringIdService.ShouldReplaceEffectId(plan.EffectId, settings))
                return;

            int oldId = plan.EffectId;
            int previewId = BuffAuthoringIdService.GetNextAvailableEffectId(settings);
            plan.EffectId = previewId;
            if (plan.EffectCodegenPlan != null)
                plan.EffectCodegenPlan.EffectId = previewId;

            RemoveEffectIdAllocationError(plan);
            AddUnique(plan.Infos, $"CompositeEffect 预览仅为代码文本使用推荐 EffectId={previewId}；原 EffectId={oldId}，不会写入 ID Registry。");
        }

        private static void RemoveEffectIdAllocationError(BuffGraphGeneratePlan plan)
        {
            for (int i = plan.Errors.Count - 1; i >= 0; i--)
            {
                if (plan.Errors[i] == "EffectId is invalid or occupied, and AutoAllocateIds is disabled.")
                    plan.Errors.RemoveAt(i);
            }
        }

        private static void RemoveEffectIdAllocationMessages(BuffGraphGeneratePlan plan)
        {
            for (int i = plan.Infos.Count - 1; i >= 0; i--)
            {
                if (plan.Infos[i].StartsWith("Graph EffectId=", StringComparison.Ordinal))
                    plan.Infos.RemoveAt(i);
            }
        }

        private static void AddGraphCodegenIssues(BuffGraphGeneratePlan plan, BuffGraphGenerateReport report)
        {
            if (plan.EffectCodegenPlan == null)
                return;

            for (int i = 0; i < plan.EffectCodegenPlan.Errors.Count; i++)
                AddUnique(report.Errors, "GRAPH_CODEGEN: " + plan.EffectCodegenPlan.Errors[i]);

            for (int i = 0; i < plan.EffectCodegenPlan.Warnings.Count; i++)
                AddUnique(report.Warnings, "GRAPH_CODEGEN: " + plan.EffectCodegenPlan.Warnings[i]);
        }

        private static void AppendPreflightIssues(BuffAuthoringPreflightResult result, BuffGraphGenerateReport report)
        {
            for (int i = 0; i < result.Issues.Count; i++)
            {
                BuffAuthoringPreflightIssue issue = result.Issues[i];
                string message = string.IsNullOrWhiteSpace(issue.Code)
                    ? issue.Message
                    : $"{issue.Code}: {issue.Message}";

                if (issue.Severity == BuffAuthoringPreflightSeverity.Error)
                    AddUnique(report.Errors, message);
                else if (issue.Severity == BuffAuthoringPreflightSeverity.Warning)
                    AddUnique(report.Warnings, message);
                else
                    AddUnique(report.Infos, message);
            }
        }

        private static void AddUnique(System.Collections.Generic.List<string> lines, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || lines.Contains(value))
                return;

            lines.Add(value);
        }

        private static string BuildSafeBuffName(BuffCandidateGraph graph)
        {
            return BuffAuthoringValidationUtility.MakeSafeFileName(graph != null ? graph.name : DefaultBuffName, DefaultBuffName);
        }

        private static string BuildEffectClassName(string effectName, string graphName)
        {
            string source = !string.IsNullOrWhiteSpace(effectName) ? effectName : graphName;
            string safe = ToPascalIdentifier(source);
            if (string.IsNullOrWhiteSpace(safe))
                safe = DefaultEffectClassName;

            if (!safe.EndsWith("Effect", StringComparison.Ordinal))
                safe += "Effect";

            return safe;
        }

        private static string BuildCompositeEffectClassName(string buffName, string graphName)
        {
            string source = !string.IsNullOrWhiteSpace(buffName) ? buffName : graphName;
            string safe = ToPascalIdentifier(source);
            if (string.IsNullOrWhiteSpace(safe))
                safe = "Generated";

            if (!safe.EndsWith("CompositeEffect", StringComparison.Ordinal))
                safe += "CompositeEffect";

            return safe;
        }

        private static string GetPrimaryEffectName(BuffCandidateGraph graph)
        {
            if (graph == null)
                return string.Empty;

            EffectCompositionRootNode effectRoot = graph.FindSingleNode<EffectCompositionRootNode>();
            if (effectRoot != null && !string.IsNullOrWhiteSpace(effectRoot.FinalEffectName))
                return effectRoot.FinalEffectName;

            System.Collections.Generic.List<EffectNode> effects = BuffGraphEffectOrderUtility.Build(graph).OrderedEffects;
            if (effects.Count > 0)
                return effects[0].EffectName;

            EffectBindingNode binding = graph.FindSingleNode<EffectBindingNode>();
            return binding != null ? binding.EffectClassName : string.Empty;
        }

        private static int CompareEffectNodes(EffectNode left, EffectNode right)
        {
            return BuffGraphEffectOrderUtility.CompareEffectNodes(left, right);
        }

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string safeFileName = BuffAuthoringValidationUtility.MakeSafeFileName(value, string.Empty);
            StringBuilder builder = new StringBuilder();
            bool nextUpper = true;
            for (int i = 0; i < safeFileName.Length; i++)
            {
                char c = safeFileName[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    nextUpper = true;
                    continue;
                }

                if (builder.Length == 0 && char.IsDigit(c))
                    builder.Append('_');

                builder.Append(nextUpper ? char.ToUpperInvariant(c) : c);
                nextUpper = c == '_';
            }

            return builder.ToString();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(BuffAuthoringValidationUtility.NormalizeAssetPath(assetPath));
        }
    }
}
