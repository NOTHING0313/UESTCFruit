using ECSFrameWork;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;

namespace BuffSystem.EditorTesting
{
    /// <summary>
    /// BuffSystem Editor-only 测试编排器；不创建 Unity 场景对象，不修改资源、注册表或 whitelist。
    /// </summary>
    internal sealed class BuffSystemFullTestRunner
    {
        private const float FixedTickLength = 0.02f;
        private const int SmokeConfigId = 991001;
        private const int SmokeEffectId = 990101;

        private readonly bool _runDestructiveWriteSmoke;

        internal BuffSystemFullTestRunner(bool runDestructiveWriteSmoke)
        {
            _runDestructiveWriteSmoke = runDestructiveWriteSmoke;
        }

        internal BuffSystemTestReport RunAll()
        {
            BuffSystemTestReport report = CreateReport("All");
            RunUnitTests(report);
            RunIntegrationTests(report);
            RunWhiteBoxTests(report);
            RunBlackBoxTests(report);
            RunSmokeTests(report);
            RunAuthoringSmokeTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        internal BuffSystemTestReport RunUnit()
        {
            BuffSystemTestReport report = CreateReport("Unit");
            RunUnitTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        internal BuffSystemTestReport RunIntegration()
        {
            BuffSystemTestReport report = CreateReport("Integration");
            RunIntegrationTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        internal BuffSystemTestReport RunWhiteBox()
        {
            BuffSystemTestReport report = CreateReport("WhiteBox");
            RunWhiteBoxTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        internal BuffSystemTestReport RunBlackBox()
        {
            BuffSystemTestReport report = CreateReport("BlackBox");
            RunBlackBoxTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        internal BuffSystemTestReport RunSmoke()
        {
            BuffSystemTestReport report = CreateReport("Smoke");
            RunSmokeTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        internal BuffSystemTestReport RunAuthoringSmoke()
        {
            BuffSystemTestReport report = CreateReport("AuthoringSmoke");
            RunAuthoringSmokeTests(report);
            AppendCoverageSummary(report);
            report.WriteLatestFiles();
            return report;
        }

        private BuffSystemTestReport CreateReport(string profile)
        {
            BuffSystemTestReport report = BuffSystemTestReport.Create(profile, _runDestructiveWriteSmoke);
            report.Notes.Add("报告由 Editor-only 测试入口生成；不会修改 BuffSystem runtime、registry、whitelist、场景或 Prefab。");
            report.Notes.Add("现有 MonoBehaviour ContextMenu Runner 仍保持独立，本入口只自动执行无场景对象依赖的 smoke / unit / integration 子集。");
            report.ManualSceneItems.Add("手动 Runner：BuffSystemPhase2AValidationRunner");
            report.ManualSceneItems.Add("手动 Runner：BuffSystemCompressedParallelValidationRunner");
            report.ManualSceneItems.Add("手动 Runner：BuffSystemRestoreHookValidationRunner");
            report.ManualSceneItems.Add("手动 Runner：BuffSystemStorageBehaviorConsistencyRunner");
            report.ManualSceneItems.Add("手动 Runner：BuffSystemStoragePerformanceRunner");
            return report;
        }

        private void RunUnitTests(BuffSystemTestReport report)
        {
            RunCase(report, "Unit", "BuffDefinitionRegistry register/query/remove", "DefinitionRegistry", () =>
            {
                BuffDefinitionRegistry registry = new BuffDefinitionRegistry();
                BuffDefinition definition = CreateDefinition(120001, 120101, BuffInstanceType.normal, ParallelBuffStorageMode.EntityPerStack, 1, 8, 2);
                registry.Register(in definition);

                Assert(registry.Count == 1, "Registry count should be 1 after register.");
                Assert(registry.TryGetDefinition(120001, out BuffDefinition loaded), "Definition should be queryable.");
                Assert(loaded.ConfigId == 120001 && loaded.EffectId == 120101, "Loaded definition fields should match.");
                Assert(registry.Remove(120001), "Remove should return true.");
                Assert(!registry.TryGetDefinition(120001, out _), "Definition should be absent after remove.");
            });

            RunCase(report, "Unit", "BuffEffectRegistry register/query/remove", "EffectRegistry", () =>
            {
                BuffEffectRegistry registry = new BuffEffectRegistry();
                CountingEffect effect = new CountingEffect();
                registry.Register(120201, effect);

                Assert(registry.Count == 1, "Effect registry count should be 1.");
                Assert(registry.TryGet(120201, out IBuffEffectExecutor loaded), "Effect should be queryable.");
                Assert(ReferenceEquals(effect, loaded), "Loaded effect should be the same instance.");
                Assert(registry.Remove(120201), "Remove should return true.");
                Assert(!registry.TryGet(120201, out _), "Effect should be absent after remove.");
            });

            RunCase(report, "Unit", "Compressed eligibility utility reasons", "AuthoringValidationUtility", () =>
            {
                CompressedEligibilityResult eligible = BuffAuthoringValidationUtility.ComputeCompressedEligibility(
                    BuffInstanceType.parallel,
                    BuffTriggerType.Tick,
                    ParallelBuffStorageMode.CompressedExpiryFrameList,
                    false,
                    1);

                CompressedEligibilityResult blocked = BuffAuthoringValidationUtility.ComputeCompressedEligibility(
                    BuffInstanceType.parallel,
                    BuffTriggerType.Tick,
                    ParallelBuffStorageMode.CompressedExpiryFrameList,
                    true,
                    1);

                Assert(eligible.IsEligible, "Expected compressed eligibility pass.");
                Assert(!blocked.IsEligible, "Unlimited should block compressed eligibility.");
                Assert(blocked.Reasons.Count > 0, "Blocked eligibility should include reason.");
            });
        }

        private void RunIntegrationTests(BuffSystemTestReport report)
        {
            RunCase(report, "Integration", "BuffSystemCore Add/Tick/TryGet/Remove", "BuffSystemCore public API", () =>
            {
                TestEnvironment env = CreateEnvironment(121001, 121101, BuffInstanceType.normal, ParallelBuffStorageMode.EntityPerStack, 3, 12, 2);

                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, env.ConfigId, env.Source, 2));
                Tick(env, 1);

                Assert(env.BuffSystem.TryGetBuff(env.Target, env.ConfigId, env.Source, out BuffViewData view), "TryGetBuff should find added buff.");
                Assert(view.Stack == 2, $"Expected Stack=2, actual={view.Stack}.");
                Assert(env.Effect.ApplyCount == 1, $"OnApply should run once, actual={env.Effect.ApplyCount}.");

                IReadOnlyList<BuffViewData> buffs = env.BuffSystem.GetBuffs(env.Target);
                Assert(CountConfigViews(buffs, env.ConfigId) == 1, "GetBuffs should expose one aggregate view for normal buff.");

                env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Target, env.ConfigId, env.Source, 1, false, true));
                Tick(env, 2);
                Assert(!env.BuffSystem.TryGetBuff(env.Target, env.ConfigId, env.Source, out _), "TryGetBuff should be false after clear remove.");
                Assert(env.Effect.RemoveCount == 1, $"OnRemove should run once, actual={env.Effect.RemoveCount}.");
            });
        }

        private void RunWhiteBoxTests(BuffSystemTestReport report)
        {
            RunCase(report, "WhiteBox", "OnWorldRestored reflection keeps public query visible", "OnWorldRestored transient cache rebuild", () =>
            {
                TestEnvironment env = CreateEnvironment(122001, 122101, BuffInstanceType.normal, ParallelBuffStorageMode.EntityPerStack, 1, 30, 10);

                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, env.ConfigId, env.Source, 1));
                Tick(env, 1);
                Assert(env.BuffSystem.TryGetBuff(env.Target, env.ConfigId, env.Source, out BuffViewData before), "Setup TryGetBuff should find runtime.");

                MethodInfo method = typeof(BuffSystemCore).GetMethod(
                    "OnWorldRestored",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Assert(method != null, "OnWorldRestored should exist for restore hook integration.");

                int applyBefore = env.Effect.ApplyCount;
                int tickBefore = env.Effect.TickCount;
                int removeBefore = env.Effect.RemoveCount;
                method.Invoke(env.BuffSystem, new object[] { env.World });

                Assert(env.BuffSystem.TryGetBuff(env.Target, env.ConfigId, env.Source, out BuffViewData after), "TryGetBuff should remain visible after restore hook.");
                Assert(before.RuntimeHandle == after.RuntimeHandle, "RuntimeHandle should remain stable after transient rebuild.");
                Assert(env.Effect.ApplyCount == applyBefore && env.Effect.TickCount == tickBefore && env.Effect.RemoveCount == removeBefore, "Restore hook should not invoke lifecycle effects.");
            });

            RunCase(report, "WhiteBox", "Runtime component query count after Add", "ECS runtime component query", () =>
            {
                TestEnvironment env = CreateEnvironment(122002, 122102, BuffInstanceType.normal, ParallelBuffStorageMode.EntityPerStack, 1, 20, 10);
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, env.ConfigId, env.Source, 1));
                Tick(env, 1);

                int runtimeCount = CountComponents<BuffRuntimeComponent>(env.World);
                int compressedCount = CountComponents<CompressedParallelBuffRuntimeComponent>(env.World);
                Assert(runtimeCount == 1, $"Expected one BuffRuntimeComponent, actual={runtimeCount}.");
                Assert(compressedCount == 0, $"Expected zero compressed runtime for non-production public constructor, actual={compressedCount}.");
            });
        }

        private void RunBlackBoxTests(BuffSystemTestReport report)
        {
            RunCase(report, "BlackBox", "Public API source-specific queries", "IBuffSystem public surface", () =>
            {
                TestEnvironment env = CreateEnvironment(123001, 123101, BuffInstanceType.parallel, ParallelBuffStorageMode.EntityPerStack, 3, 40, 10);
                Entity secondSource = env.World.CreateEntity();

                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, env.ConfigId, env.Source, 1));
                env.BuffSystem.AddBuff(new AddBuffCommand(env.Target, env.ConfigId, secondSource, 1));
                Tick(env, 1);

                Assert(env.BuffSystem.TryGetBuff(env.Target, env.ConfigId, env.Source, out BuffViewData first), "First source should be queryable.");
                Assert(env.BuffSystem.TryGetBuff(env.Target, env.ConfigId, secondSource, out BuffViewData second), "Second source should be queryable.");
                Assert(first.Source.Equals(env.Source), "First view source should match.");
                Assert(second.Source.Equals(secondSource), "Second view source should match.");
                Assert(CountConfigViews(env.BuffSystem.GetBuffs(env.Target), env.ConfigId) == 2, "GetBuffs should expose one view per source for EntityPerStack parallel test.");
            });
        }

        private void RunSmokeTests(BuffSystemTestReport report)
        {
            RunCase(report, "Smoke", "991001 asset path/effect/eligibility smoke", "Resources pilot asset", () =>
            {
                List<BuffAssetSummary> assets = BuffAuthoringValidationUtility.ScanBuffAssets();
                BuffAssetSummary smoke = null;

                for (int i = 0; i < assets.Count; i++)
                {
                    if (assets[i].ConfigId == SmokeConfigId)
                    {
                        smoke = assets[i];
                        break;
                    }
                }

                Assert(smoke != null, "Expected 991001 smoke asset under Assets/Resources/BuffSystem/Buff.");
                Assert(smoke.AssetPath == "Assets/Resources/BuffSystem/Buff/Debug_CompressedParallel_TickSmoke.asset", $"Unexpected smoke asset path: {smoke.AssetPath}.");
                Assert(smoke.EffectId == SmokeEffectId, $"Expected EffectId={SmokeEffectId}, actual={smoke.EffectId}.");

                EffectRegistryCheckResult effectResult = BuffAuthoringValidationUtility.CheckProductionEffectRegistered(smoke.EffectId);
                Assert(!effectResult.IsUnknown, $"Effect registry status unknown: {effectResult.ErrorMessage}");
                Assert(effectResult.IsRegistered, $"EffectId={smoke.EffectId} should be registered in production registry.");

                CompressedEligibilityResult eligibility = BuffAuthoringValidationUtility.ComputeCompressedEligibility(smoke.SourceAsset);
                Assert(eligibility.IsEligible, "991001 should pass compressed eligibility.");
                Assert(smoke.IsSmokeOrDebug, "991001 should remain classified as smoke/debug.");
            });

            RunCase(report, "Smoke", "Existing manual validation runners detected", "Legacy ContextMenu runners", () =>
            {
                AssertManualRunnerExists("BuffSystem.BuffSystemPhase2AValidationRunner", "RunPhase2AValidation");
                AssertManualRunnerExists("BuffSystem.BuffSystemCompressedParallelValidationRunner", "RunCompressedParallelValidation");
                AssertManualRunnerExists("BuffSystem.BuffSystemRestoreHookValidationRunner", "RunRestoreHookValidation");
                AssertManualRunnerExists("BuffSystem.BuffSystemStorageBehaviorConsistencyRunner", "RunStorageBehaviorConsistencyValidation");
                AssertManualRunnerExists("BuffSystem.BuffSystemStoragePerformanceRunner", "RunStoragePerformanceValidation");
            });

            if (!_runDestructiveWriteSmoke)
            {
                report.AddResult(BuffSystemTestCaseResult.Skipped(
                    "Smoke",
                    "Destructive authoring write smoke",
                    "RunDestructiveWriteSmoke=false，默认不创建 Buff asset、不生成 Effect 模板、不写 registry。",
                    "Authoring write path",
                    "如需验证写入链路，请单独启用临时资源写入并手动清理。"));
            }
        }

        private void RunAuthoringSmokeTests(BuffSystemTestReport report)
        {
            RunCase(report, "AuthoringSmoke", "Authoring validator scan smoke", "BuffAuthoringValidationUtility", () =>
            {
                List<BuffAssetSummary> assets = BuffAuthoringValidationUtility.ScanBuffAssets();
                Dictionary<int, int> index = BuffAuthoringValidationUtility.BuildConfigIdIndex(assets);
                int smokeCount = 0;

                for (int i = 0; i < assets.Count; i++)
                {
                    if (assets[i].IsSmokeOrDebug)
                        smokeCount++;
                }

                Assert(assets.Count >= 1, "Expected at least one BuffConfigData asset.");
                Assert(index.ContainsKey(SmokeConfigId), "ConfigId index should include 991001.");
                Assert(smokeCount >= 1, "Expected at least one smoke/debug asset.");
            });

            RunCase(report, "AuthoringSmoke", "Graph authoring service types detected", "xNode authoring helpers", () =>
            {
                List<string> found = new List<string>
                {
                    FormatFoundType("BuffCandidateGraph", AssertTypeExists(
                        "BuffCandidateGraph",
                        "BuffSystem.BuffCandidateGraph",
                        "BuffSystem.Editor.AuthoringGraphs.BuffCandidateGraph")),
                    FormatFoundType("EffectCompositionRootNode", AssertTypeExists(
                        "EffectCompositionRootNode",
                        "BuffSystem.Editor.AuthoringGraphs.EffectCompositionRootNode")),
                    FormatFoundType("EffectNode", AssertTypeExists(
                        "EffectNode",
                        "BuffSystem.Editor.AuthoringGraphs.EffectNode")),
                    FormatFoundType("ScriptActionNode", AssertTypeExists(
                        "ScriptActionNode",
                        "BuffSystem.Editor.AuthoringGraphs.ScriptActionNode")),
                    FormatFoundType("BuffGraphGenerateService", AssertTypeExists(
                        "BuffGraphGenerateService",
                        "BuffSystem.BuffGraphGenerateService",
                        "BuffSystem.Editor.AuthoringGraphs.BuffGraphGenerateService")),
                    FormatFoundType("BuffGraphCompositeEffectPlanBuilder", AssertTypeExists(
                        "BuffGraphCompositeEffectPlanBuilder",
                        "BuffSystem.BuffGraphCompositeEffectPlanBuilder",
                        "BuffSystem.Editor.AuthoringGraphs.BuffGraphCompositeEffectPlanBuilder")),
                    FormatFoundType("BuffGraphCompositeEffectEmitter", AssertTypeExists(
                        "BuffGraphCompositeEffectEmitter",
                        "BuffSystem.BuffGraphCompositeEffectEmitter",
                        "BuffSystem.Editor.AuthoringGraphs.BuffGraphCompositeEffectEmitter")),
                    FormatFoundType("BuffScriptActionNodeValidator", AssertTypeExists(
                        "BuffScriptActionNodeValidator",
                        "BuffSystem.BuffScriptActionNodeValidator",
                        "BuffSystem.Editor.AuthoringGraphs.BuffScriptActionNodeValidator"))
                };

                return "Found: " + string.Join(", ", found);
            });

            RunCase(report, "AuthoringSmoke", "Effect bootstrap scanner read-only smoke", "Effect bootstrap scanner", () =>
            {
                BuffEffectBootstrapRegistrationScanReport scan = BuffEffectBootstrapRegistrationScanner.Scan();
                Assert(scan.FileExists, "BuffEffectRegistryBootstrap.cs should exist.");
                Assert(!scan.HasError, "Bootstrap registration scan should have no errors.");
                Assert(scan.Entries.Count >= 1, "Expected at least one production effect registration entry.");
            });
        }

        private void AppendCoverageSummary(BuffSystemTestReport report)
        {
            report.AddCoverage("BuffDefinitionRegistry", "Unit", "Covered", "register/query/remove", "内存定义表基础行为。");
            report.AddCoverage("BuffEffectRegistry", "Unit", "Covered", "register/query/remove", "不触发生产 Bootstrap 写入。");
            report.AddCoverage("BuffSystemCore public Add/Tick/Remove/Query", "Integration", "Covered", "in-memory World", "只覆盖 EntityPerStack public constructor 路径。");
            report.AddCoverage("OnWorldRestored transient rebuild", "WhiteBox", "Covered", "reflection call", "验证 hook 不触发生命周期 Effect。");
            report.AddCoverage("991001 production pilot asset", "Smoke", "Covered", "AssetDatabase + production effect registry check", "只读验证 asset/effect/eligibility。");
            report.AddCoverage("Authoring Validator utility", "AuthoringSmoke", "Covered", "ScanBuffAssets", "不修改 asset。");
            report.AddCoverage("Graph/Composite authoring services", "AuthoringSmoke", "SmokeOnly", "type detection", "不生成图、不生成 Effect、不写 registry。");
            report.AddCoverage("Existing ContextMenu validation runners", "Manual", "ManualScene", "runner type detection", "仍需 Unity 场景/组件右键或用户手动运行。");
            report.AddCoverage("Compressed production whitelist path", "Manual", "NotCovered", "not invoked by public constructor", "不修改 whitelist，也不伪造 production factory。");
            report.AddCoverage("Performance benchmark", "Manual", "NotCovered", "manual runner remains independent", "避免 MCP smoke 默认执行耗时测量。");
            report.AddCoverage("Scene/View production pilot", "ManualScene", "NotCovered", "manual ECS Debugger / View scene", "本入口不运行 PlayMode、不保存 scene。");
            report.AddCoverage("Rollback correctness", "Manual", "NotCovered", "external RollBackSystem dependency", "本入口不宣称 rollback-ready。");
        }

        private void RunCase(BuffSystemTestReport report, string category, string name, string coveredArea, Action action)
        {
            RunCase(report, category, name, coveredArea, () =>
            {
                action();
                return "PASS";
            });
        }

        private void RunCase(BuffSystemTestReport report, string category, string name, string coveredArea, Func<string> action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                string message = action();
                stopwatch.Stop();
                report.AddResult(BuffSystemTestCaseResult.Passed(category, name, string.IsNullOrWhiteSpace(message) ? "PASS" : message, coveredArea, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.AddResult(BuffSystemTestCaseResult.Failed(category, name, exception.Message, exception, coveredArea, stopwatch.Elapsed.TotalMilliseconds));
            }
        }

        private static TestEnvironment CreateEnvironment(
            int configId,
            int effectId,
            BuffInstanceType buffType,
            ParallelBuffStorageMode storageMode,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames)
        {
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();

            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            CountingEffect effect = new CountingEffect();

            definitions.Register(CreateDefinition(configId, effectId, buffType, storageMode, maxStack, durationFrames, tickIntervalFrames));
            effects.Register(effectId, effect);

            return new TestEnvironment(world, target, source, configId, definitions, effects, effect, new BuffSystemCore(definitions, effects));
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            int effectId,
            BuffInstanceType buffType,
            ParallelBuffStorageMode storageMode,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames)
        {
            return new BuffDefinition(
                configId,
                "McpTestBuff",
                0,
                maxStack,
                false,
                false,
                durationFrames,
                tickIntervalFrames,
                0,
                BuffTriggerType.Tick,
                buffType,
                NormalBuffStackPolicy.AddStackAndRefreshDuration,
                ParallelBuffStackUpPolicy.Append,
                ParallelBuffStackDownPolicy.RemoveEarliest,
                effectId,
                null,
                storageMode);
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            env.BuffSystem.Tick(env.World, new SimulationContext(frameNumber, FixedTickLength, false));
        }

        private static int CountConfigViews(IReadOnlyList<BuffViewData> views, int configId)
        {
            int count = 0;
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].ConfigId == configId)
                    count++;
            }

            return count;
        }

        private static int CountComponents<T>(World world) where T : struct, IComponentData
        {
            List<Entity> entities = new List<Entity>();
            EntityQueryDescription query = world.Query().With<T>().BuildDescription();
            world.FillQuery(query, entities, true);
            return entities.Count;
        }

        private static void AssertManualRunnerExists(string typeName, string methodName)
        {
            Type type = FindType(typeName);
            Assert(type != null, $"Runner type not found: {typeName}");

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert(method != null, $"Runner method not found: {typeName}.{methodName}");
        }

        private static Type AssertTypeExists(params string[] typeNames)
        {
            Type type = FindType(typeNames);
            if (type != null)
                return type;

            throw new InvalidOperationException(
                "Type not found. Tried names: "
                + string.Join(", ", typeNames)
                + $". Loaded assemblies count: {AppDomain.CurrentDomain.GetAssemblies().Length}.");
        }

        private static Type FindType(params string[] names)
        {
            if (names == null || names.Length == 0)
                return null;

            for (int i = 0; i < names.Length; i++)
            {
                Type type = Type.GetType(names[i] + ", Assembly-CSharp-Editor");
                if (type != null)
                    return type;

                type = Type.GetType(names[i] + ", Assembly-CSharp");
                if (type != null)
                    return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types = GetLoadableTypes(assemblies[i]);
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null)
                        continue;

                    for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
                    {
                        string name = names[nameIndex];
                        if (type.FullName == name || type.Name == name)
                            return type;
                    }
                }
            }

            return null;
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
                return Array.Empty<Type>();

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                Type[] loadedTypes = exception.Types;
                if (loadedTypes == null)
                    return Array.Empty<Type>();

                return loadedTypes;
            }
        }

        private static string FormatFoundType(string label, Type type)
        {
            return $"{label}={type.FullName}";
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct TestEnvironment
        {
            public readonly World World;
            public readonly Entity Target;
            public readonly Entity Source;
            public readonly int ConfigId;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly CountingEffect Effect;
            public readonly BuffSystemCore BuffSystem;

            public TestEnvironment(
                World world,
                Entity target,
                Entity source,
                int configId,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                CountingEffect effect,
                BuffSystemCore buffSystem)
            {
                World = world;
                Target = target;
                Source = source;
                ConfigId = configId;
                Definitions = definitions;
                Effects = effects;
                Effect = effect;
                BuffSystem = buffSystem;
            }
        }

        private sealed class CountingEffect : BuffEffectExecutorBase
        {
            public int ApplyCount { get; private set; }
            public int TickCount { get; private set; }
            public int RemoveCount { get; private set; }

            public override void OnApply(in BuffEffectContext context)
            {
                ApplyCount++;
            }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
            }

            public override void OnRemove(in BuffEffectContext context)
            {
                RemoveCount++;
            }
        }
    }
}
