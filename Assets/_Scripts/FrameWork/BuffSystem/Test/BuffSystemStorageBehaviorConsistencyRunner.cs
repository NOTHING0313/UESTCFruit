using System.Collections.Generic;
using System.Text;
using ECSFrameWork;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// EntityPerStack 与 CompressedParallel 在 public API 层的行为一致性验证入口。
    /// </summary>
    public sealed class BuffSystemStorageBehaviorConsistencyRunner : MonoBehaviour
    {
        private const float FixedTickLength = 0.02f;

        private const int BehaviorBuffId = 9301;
        private const int RefreshEarliestBuffId = 9305;
        private const int RefreshAllBuffId = 9306;
        private const int RemoveEarliestBuffId = 9307;
        private const int RemoveLatestBuffId = 9308;
        private const int ExpireBuffId = 9310;
        private const int DifferentSourceBuffId = 9311;
        private const int MatchAnySourceBuffId = 9312;
        private const int ReplaceFullBuffId = 9314;
        private const int CallbackBuffId = 9315;

        private const int BehaviorEffectId = 9401;
        private const int RefreshEarliestEffectId = 9405;
        private const int RefreshAllEffectId = 9406;
        private const int RemoveEarliestEffectId = 9407;
        private const int RemoveLatestEffectId = 9408;
        private const int ExpireEffectId = 9410;
        private const int DifferentSourceEffectId = 9707;
        private const int MatchAnySourceEffectId = 9708;
        private const int ReplaceFullEffectId = 9414;
        private const int CallbackEffectId = 9709;

        [ContextMenu("运行 EntityPerStack vs Compressed 行为一致性验证")]
        public void RunStorageBehaviorConsistencyValidation()
        {
            ValidationState state = new ValidationState();
            state.Log("========== EntityPerStack vs Compressed 行为一致性验证 ==========");

            RunAddTryGetConsistencyTest(state);
            RunAddGetBuffsConsistencyTest(state);
            RunRemoveOneLayerConsistencyTest(state);
            RunRemoveAllConsistencyTest(state);
            RunTickCountConsistencyTest(state);
            RunExpireConsistencyTest(state);
            RunRefreshEarliestConsistencyTest(state);
            RunRefreshAllConsistencyTest(state);
            RunReplaceEarliestWhenFullConsistencyTest(state);
            RunRemoveEarliestConsistencyTest(state);
            RunRemoveLatestConsistencyTest(state);
            RunDifferentSourceConsistencyTest(state);
            RunMatchAnySourceConsistencyTest(state);
            RunCallbackCountConsistencyTest(state);

            state.Log(state.HasFailure
                ? "========== EntityPerStack vs Compressed Strategy Behavior Result: FAIL =========="
                : "========== EntityPerStack vs Compressed Strategy Behavior Result: PASS ==========");

            Debug.Log(state.BuildOutput());
        }

        private static void RunAddTryGetConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(BehaviorBuffId, stack: 3, durationFrames: 30, tickIntervalFrames: 0);
            AddAndTick(pair, BehaviorBuffId, 3, 1, 2);

            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, BehaviorBuffId, pair.EntityPerStack.Source, out BuffViewData entityView);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, BehaviorBuffId, pair.Compressed.Source, out BuffViewData compressedView);

            state.ExpectEqual("Add 后 TryGetBuff 成功状态一致", entityFound ? 1 : 0, compressedFound ? 1 : 0, 2, BehaviorBuffId, pair, "TryGetBuff");
            AssertComparableView(state, "Add 后 TryGetBuff ViewData 一致", entityView, compressedView, entityFound && compressedFound, 2, BehaviorBuffId, pair, compareRemainingFrames: true);
        }

        private static void RunAddGetBuffsConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(BehaviorBuffId, stack: 3, durationFrames: 30, tickIntervalFrames: 0);
            AddAndTick(pair, BehaviorBuffId, 3, 1, 2);

            IReadOnlyList<BuffViewData> entityBuffs = pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target);
            IReadOnlyList<BuffViewData> compressedBuffs = pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target);
            int entityCount = CountBuffsWithConfig(entityBuffs, BehaviorBuffId);
            int compressedCount = CountBuffsWithConfig(compressedBuffs, BehaviorBuffId);

            state.ExpectEqual("Add 后 GetBuffs 当前 configId 数量一致", entityCount, compressedCount, 2, BehaviorBuffId, pair, "GetBuffs count");

            bool entityHasView = TryFindView(entityBuffs, BehaviorBuffId, out BuffViewData entityView);
            bool compressedHasView = TryFindView(compressedBuffs, BehaviorBuffId, out BuffViewData compressedView);
            state.ExpectEqual("Add 后 GetBuffs 当前 configId 可见性一致", entityHasView ? 1 : 0, compressedHasView ? 1 : 0, 2, BehaviorBuffId, pair, "GetBuffs visible");
            AssertComparableView(state, "Add 后 GetBuffs 聚合 ViewData 一致", entityView, compressedView, entityHasView && compressedHasView, 2, BehaviorBuffId, pair, compareRemainingFrames: true);
        }

        private static void RunRemoveOneLayerConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(BehaviorBuffId, stack: 3, durationFrames: 30, tickIntervalFrames: 0);
            AddAndTick(pair, BehaviorBuffId, 3, 1, 2);

            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, BehaviorBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, BehaviorBuffId, pair.Compressed.Source, 1));
            Tick(pair, 3);

            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, BehaviorBuffId, pair.EntityPerStack.Source, out BuffViewData entityView);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, BehaviorBuffId, pair.Compressed.Source, out BuffViewData compressedView);

            state.ExpectEqual("Remove 一层后 TryGetBuff 成功状态一致", entityFound ? 1 : 0, compressedFound ? 1 : 0, 3, BehaviorBuffId, pair, "Remove one found");
            state.ExpectEqual("Remove 一层后剩余 Stack 一致", entityFound ? entityView.Stack : -1, compressedFound ? compressedView.Stack : -1, 3, BehaviorBuffId, pair, "Remove one stack");
        }

        private static void RunRemoveAllConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(BehaviorBuffId, stack: 3, durationFrames: 30, tickIntervalFrames: 0);
            AddAndTick(pair, BehaviorBuffId, 3, 1, 2);

            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, BehaviorBuffId, pair.EntityPerStack.Source, 1, false, true));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, BehaviorBuffId, pair.Compressed.Source, 1, false, true));
            Tick(pair, 3);

            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, BehaviorBuffId, pair.EntityPerStack.Source, out BuffViewData _);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, BehaviorBuffId, pair.Compressed.Source, out BuffViewData _);
            int entityCount = CountBuffsWithConfig(pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target), BehaviorBuffId);
            int compressedCount = CountBuffsWithConfig(pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target), BehaviorBuffId);

            state.ExpectEqual("RemoveAll 后 TryGetBuff 失败状态一致", entityFound ? 1 : 0, compressedFound ? 1 : 0, 3, BehaviorBuffId, pair, "RemoveAll found");
            state.ExpectEqual("RemoveAll 后 GetBuffs 不包含当前 configId 一致", entityCount, compressedCount, 3, BehaviorBuffId, pair, "RemoveAll GetBuffs count");
            state.ExpectEqual("RemoveAll 后 EntityPerStack 当前 configId 数量为 0", 0, entityCount, 3, BehaviorBuffId, pair, "RemoveAll EntityPerStack count");
            state.ExpectEqual("RemoveAll 后 Compressed 当前 configId 数量为 0", 0, compressedCount, 3, BehaviorBuffId, pair, "RemoveAll Compressed count");
        }

        private static void RunTickCountConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(BehaviorBuffId, stack: 3, durationFrames: 30, tickIntervalFrames: 1);
            AddAndTick(pair, BehaviorBuffId, 3, 1, 2);
            Tick(pair, 3);
            Tick(pair, 4);

            state.ExpectEqual("Tick 触发次数一致", pair.EntityPerStack.Effect.TickCount, pair.Compressed.Effect.TickCount, 4, BehaviorBuffId, pair, "Tick count");
        }

        private static void RunExpireConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(ExpireBuffId, stack: 2, durationFrames: 2, tickIntervalFrames: 1);
            AddAndTick(pair, ExpireBuffId, 2, 1, 2);
            Tick(pair, 3);
            Tick(pair, 4);

            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, ExpireBuffId, pair.EntityPerStack.Source, out BuffViewData _);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, ExpireBuffId, pair.Compressed.Source, out BuffViewData _);
            int entityCount = CountBuffsWithConfig(pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target), ExpireBuffId);
            int compressedCount = CountBuffsWithConfig(pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target), ExpireBuffId);

            state.ExpectEqual("Expire 后 TryGetBuff 失败状态一致", entityFound ? 1 : 0, compressedFound ? 1 : 0, 4, ExpireBuffId, pair, "Expire found");
            state.ExpectEqual("Expire 后 GetBuffs 当前 configId 数量一致", entityCount, compressedCount, 4, ExpireBuffId, pair, "Expire GetBuffs count");
            state.ExpectEqual("Expire 后 EntityPerStack 当前 configId 数量为 0", 0, entityCount, 4, ExpireBuffId, pair, "Expire EntityPerStack count");
            state.ExpectEqual("Expire 后 Compressed 当前 configId 数量为 0", 0, compressedCount, 4, ExpireBuffId, pair, "Expire Compressed count");
        }

        private static void RunRefreshEarliestConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(RefreshEarliestBuffId, RefreshEarliestEffectId, 3, 10, 1, ParallelBuffStackUpPolicy.RefreshEarliest, ParallelBuffStackDownPolicy.RemoveEarliest);
            AddAndTick(pair, RefreshEarliestBuffId, 3, 1, 2);
            Tick(pair, 3);
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, RefreshEarliestBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, RefreshEarliestBuffId, pair.Compressed.Source, 1));
            Tick(pair, 4);

            AssertTryGetViewConsistency(state, "RefreshEarliest 后聚合 ViewData 一致", pair, RefreshEarliestBuffId, 4, compareRemainingFrames: true);
            AssertGetBuffsConsistency(state, "RefreshEarliest 后 GetBuffs 一致", pair, RefreshEarliestBuffId, 4, compareRemainingFrames: true);
        }

        private static void RunRefreshAllConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(RefreshAllBuffId, RefreshAllEffectId, 3, 10, 1, ParallelBuffStackUpPolicy.RefreshAll, ParallelBuffStackDownPolicy.RemoveEarliest);
            AddAndTick(pair, RefreshAllBuffId, 3, 1, 2);
            Tick(pair, 3);
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, RefreshAllBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, RefreshAllBuffId, pair.Compressed.Source, 1));
            Tick(pair, 4);

            AssertTryGetViewConsistency(state, "RefreshAll 后聚合 ViewData 一致", pair, RefreshAllBuffId, 4, compareRemainingFrames: true);
            AssertGetBuffsConsistency(state, "RefreshAll 后 GetBuffs 一致", pair, RefreshAllBuffId, 4, compareRemainingFrames: true);
        }

        private static void RunReplaceEarliestWhenFullConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(ReplaceFullBuffId, ReplaceFullEffectId, 3, 10, 1, ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull, ParallelBuffStackDownPolicy.RemoveEarliest);
            AddAndTick(pair, ReplaceFullBuffId, 3, 1, 2);
            Tick(pair, 3);
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, ReplaceFullBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, ReplaceFullBuffId, pair.Compressed.Source, 1));
            Tick(pair, 4);

            AssertTryGetViewConsistency(state, "ReplaceEarliestWhenFull 满层替换后 ViewData 一致", pair, ReplaceFullBuffId, 4, compareRemainingFrames: true);
            AssertGetBuffsConsistency(state, "ReplaceEarliestWhenFull 满层替换后 GetBuffs 一致", pair, ReplaceFullBuffId, 4, compareRemainingFrames: true);
            AssertStackWithinMax(state, "ReplaceEarliestWhenFull 最终 Stack 不超过 MaxStack", pair, ReplaceFullBuffId, 3, 4);
        }

        private static void RunRemoveEarliestConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(RemoveEarliestBuffId, RemoveEarliestEffectId, 3, 10, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
            AddOneLayerPerFrame(pair, RemoveEarliestBuffId, 1, 3);
            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, RemoveEarliestBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, RemoveEarliestBuffId, pair.Compressed.Source, 1));
            Tick(pair, 4);

            AssertTryGetViewConsistency(state, "RemoveEarliest 一层后 ViewData 一致", pair, RemoveEarliestBuffId, 4, compareRemainingFrames: true);
            AssertGetBuffsConsistency(state, "RemoveEarliest 一层后 GetBuffs 一致", pair, RemoveEarliestBuffId, 4, compareRemainingFrames: true);
        }

        private static void RunRemoveLatestConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(RemoveLatestBuffId, RemoveLatestEffectId, 3, 10, 1, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveLatest);
            AddOneLayerPerFrame(pair, RemoveLatestBuffId, 1, 3);
            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, RemoveLatestBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, RemoveLatestBuffId, pair.Compressed.Source, 1));
            Tick(pair, 4);

            AssertTryGetViewConsistency(state, "RemoveLatest 一层后 ViewData 一致", pair, RemoveLatestBuffId, 4, compareRemainingFrames: true);
            AssertGetBuffsConsistency(state, "RemoveLatest 一层后 GetBuffs 一致", pair, RemoveLatestBuffId, 4, compareRemainingFrames: true);
        }

        private static void RunDifferentSourceConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(DifferentSourceBuffId, DifferentSourceEffectId, 2, 30, 0, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
            Entity entitySourceB = pair.EntityPerStack.World.CreateEntity();
            Entity compressedSourceB = pair.Compressed.World.CreateEntity();

            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, DifferentSourceBuffId, pair.EntityPerStack.Source, 2));
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, DifferentSourceBuffId, entitySourceB, 1));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, DifferentSourceBuffId, pair.Compressed.Source, 2));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, DifferentSourceBuffId, compressedSourceB, 1));
            Tick(pair, 1);
            Tick(pair, 2);

            AssertSourceView(state, "Different Source A", pair, DifferentSourceBuffId, pair.EntityPerStack.Source, pair.Compressed.Source, 2);
            AssertSourceView(state, "Different Source B", pair, DifferentSourceBuffId, entitySourceB, compressedSourceB, 2);
            state.ExpectEqual(
                "Different Source GetBuffs 聚合条目数量一致",
                CountBuffsWithConfig(pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target), DifferentSourceBuffId),
                CountBuffsWithConfig(pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target), DifferentSourceBuffId),
                2,
                DifferentSourceBuffId,
                pair,
                "Different Source GetBuffs count");
        }

        private static void RunMatchAnySourceConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(MatchAnySourceBuffId, MatchAnySourceEffectId, 3, 30, 0, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
            Entity entitySourceB = pair.EntityPerStack.World.CreateEntity();
            Entity compressedSourceB = pair.Compressed.World.CreateEntity();

            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, MatchAnySourceBuffId, pair.EntityPerStack.Source, 2));
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, MatchAnySourceBuffId, entitySourceB, 1));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, MatchAnySourceBuffId, pair.Compressed.Source, 2));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, MatchAnySourceBuffId, compressedSourceB, 1));
            Tick(pair, 1);
            Tick(pair, 2);

            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, MatchAnySourceBuffId, Entity.Invalid, 1, true));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, MatchAnySourceBuffId, Entity.Invalid, 1, true));
            Tick(pair, 3);

            AssertSourceView(state, "MatchAnySource 移除后 SourceA", pair, MatchAnySourceBuffId, pair.EntityPerStack.Source, pair.Compressed.Source, 3);
            AssertSourceView(state, "MatchAnySource 移除后 SourceB", pair, MatchAnySourceBuffId, entitySourceB, compressedSourceB, 3);

            IReadOnlyList<BuffViewData> entityBuffs = pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target);
            IReadOnlyList<BuffViewData> compressedBuffs = pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target);
            state.ExpectEqual("MatchAnySource 移除后 GetBuffs 条目数量一致", CountBuffsWithConfig(entityBuffs, MatchAnySourceBuffId), CountBuffsWithConfig(compressedBuffs, MatchAnySourceBuffId), 3, MatchAnySourceBuffId, pair, "MatchAnySource GetBuffs count");
            state.ExpectEqual("MatchAnySource 移除后剩余总 Stack 一致", SumStacksWithConfig(entityBuffs, MatchAnySourceBuffId), SumStacksWithConfig(compressedBuffs, MatchAnySourceBuffId), 3, MatchAnySourceBuffId, pair, "MatchAnySource total Stack");
        }

        private static void RunCallbackCountConsistencyTest(ValidationState state)
        {
            StoragePair pair = CreatePair(CallbackBuffId, CallbackEffectId, 3, 10, 1, ParallelBuffStackUpPolicy.RefreshEarliest, ParallelBuffStackDownPolicy.RemoveEarliest);
            AddAndTick(pair, CallbackBuffId, 3, 1, 2);
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, CallbackBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, CallbackBuffId, pair.Compressed.Source, 1));
            Tick(pair, 3);
            pair.EntityPerStack.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.EntityPerStack.Target, CallbackBuffId, pair.EntityPerStack.Source, 1));
            pair.Compressed.BuffSystem.RemoveBuff(new RemoveBuffCommand(pair.Compressed.Target, CallbackBuffId, pair.Compressed.Source, 1));
            Tick(pair, 4);

            state.ExpectEqual("Callback OnApply 次数一致", pair.EntityPerStack.Effect.ApplyCount, pair.Compressed.Effect.ApplyCount, 4, CallbackBuffId, pair, "OnApply count");
            state.ExpectEqual("Callback OnRefresh 次数一致", pair.EntityPerStack.Effect.RefreshCount, pair.Compressed.Effect.RefreshCount, 4, CallbackBuffId, pair, "OnRefresh count");
            state.ExpectEqual("Callback OnStackChanged 次数一致", pair.EntityPerStack.Effect.StackChangedCount, pair.Compressed.Effect.StackChangedCount, 4, CallbackBuffId, pair, "OnStackChanged count");
            state.ExpectEqual("Callback OnRemove 次数一致", pair.EntityPerStack.Effect.RemoveCount, pair.Compressed.Effect.RemoveCount, 4, CallbackBuffId, pair, "OnRemove count");
        }

        private static StoragePair CreatePair(int configId, int stack, int durationFrames, int tickIntervalFrames)
        {
            int effectId = configId == ExpireBuffId ? ExpireEffectId : BehaviorEffectId;
            return CreatePair(configId, effectId, stack, durationFrames, tickIntervalFrames, ParallelBuffStackUpPolicy.Append, ParallelBuffStackDownPolicy.RemoveEarliest);
        }

        private static StoragePair CreatePair(
            int configId,
            int effectId,
            int stack,
            int durationFrames,
            int tickIntervalFrames,
            ParallelBuffStackUpPolicy stackUpPolicy,
            ParallelBuffStackDownPolicy stackDownPolicy)
        {
            TestEnvironment entityPerStack = CreateEnvironment(false);
            TestEnvironment compressed = CreateEnvironment(true);

            RegisterDefinition(entityPerStack.Definitions, CreateDefinition(configId, "Consistency_EntityPerStack", effectId, stack, durationFrames, tickIntervalFrames, stackUpPolicy, stackDownPolicy, ParallelBuffStorageMode.EntityPerStack));
            RegisterDefinition(compressed.Definitions, CreateDefinition(configId, "Consistency_Compressed", effectId, stack, durationFrames, tickIntervalFrames, stackUpPolicy, stackDownPolicy, ParallelBuffStorageMode.CompressedExpiryFrameList));

            entityPerStack.Effects.Register(effectId, entityPerStack.Effect);
            compressed.Effects.Register(effectId, compressed.Effect);
            return new StoragePair(entityPerStack, compressed);
        }

        private static TestEnvironment CreateEnvironment(bool compressedGate)
        {
            World world = new World();
            Entity target = world.CreateEntity();
            Entity source = world.CreateEntity();
            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            CountingEffect effect = new CountingEffect();
            BuffSystemCore buffSystem = compressedGate
                ? BuffSystemCore.CreateForCompressedParallelValidation(definitions, effects)
                : new BuffSystemCore(definitions, effects);
            return new TestEnvironment(world, target, source, definitions, effects, effect, buffSystem);
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            string name,
            int effectId,
            int maxStack,
            int durationFrames,
            int tickIntervalFrames,
            ParallelBuffStackUpPolicy stackUpPolicy,
            ParallelBuffStackDownPolicy stackDownPolicy,
            ParallelBuffStorageMode storageMode)
        {
            return new BuffDefinition(
                configId,
                name,
                0,
                maxStack,
                false,
                false,
                durationFrames,
                tickIntervalFrames,
                0,
                BuffTriggerType.Tick,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                stackUpPolicy,
                stackDownPolicy,
                effectId,
                null,
                storageMode);
        }

        private static void RegisterDefinition(BuffDefinitionRegistry registry, in BuffDefinition definition)
        {
            registry.Register(in definition);
        }

        private static void AddAndTick(StoragePair pair, int configId, int stack, int addFrame, int queryFrame)
        {
            pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, stack));
            pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, configId, pair.Compressed.Source, stack));
            Tick(pair, addFrame);

            if (queryFrame != addFrame)
                Tick(pair, queryFrame);
        }

        private static void AddOneLayerPerFrame(StoragePair pair, int configId, int firstFrame, int layerCount)
        {
            for (int i = 0; i < layerCount; i++)
            {
                pair.EntityPerStack.BuffSystem.AddBuff(new AddBuffCommand(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, 1));
                pair.Compressed.BuffSystem.AddBuff(new AddBuffCommand(pair.Compressed.Target, configId, pair.Compressed.Source, 1));
                Tick(pair, firstFrame + i);
            }
        }

        private static void Tick(StoragePair pair, int frameNumber)
        {
            Tick(pair.EntityPerStack, frameNumber);
            Tick(pair.Compressed, frameNumber);
        }

        private static void Tick(TestEnvironment env, int frameNumber)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            env.BuffSystem.Tick(env.World, context);
        }

        private static void AssertComparableView(
            ValidationState state,
            string testName,
            BuffViewData entityView,
            BuffViewData compressedView,
            bool hasBoth,
            int frameNumber,
            int configId,
            StoragePair pair,
            bool compareRemainingFrames)
        {
            state.ExpectEqual(testName + " ConfigId", hasBoth ? entityView.ConfigId : -1, hasBoth ? compressedView.ConfigId : -2, frameNumber, configId, pair, "ConfigId");
            state.ExpectEqual(testName + " Stack", hasBoth ? entityView.Stack : -1, hasBoth ? compressedView.Stack : -2, frameNumber, configId, pair, "Stack");

            if (compareRemainingFrames)
                state.ExpectEqual(testName + " RemainingFrames", hasBoth ? entityView.RemainingFrames : -1, hasBoth ? compressedView.RemainingFrames : -2, frameNumber, configId, pair, "RemainingFrames");

            state.ExpectEqual(testName + " Target", hasBoth && entityView.Target == pair.EntityPerStack.Target ? 1 : 0, hasBoth && compressedView.Target == pair.Compressed.Target ? 1 : 0, frameNumber, configId, pair, "Target");
            state.ExpectEqual(testName + " Source", hasBoth && entityView.Source == pair.EntityPerStack.Source ? 1 : 0, hasBoth && compressedView.Source == pair.Compressed.Source ? 1 : 0, frameNumber, configId, pair, "Source");
        }

        private static void AssertTryGetViewConsistency(ValidationState state, string testName, StoragePair pair, int configId, int frameNumber, bool compareRemainingFrames)
        {
            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, out BuffViewData entityView);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, configId, pair.Compressed.Source, out BuffViewData compressedView);

            state.ExpectEqual(testName + " TryGetBuff 成功状态", entityFound ? 1 : 0, compressedFound ? 1 : 0, frameNumber, configId, pair, "TryGetBuff");
            AssertComparableView(state, testName, entityView, compressedView, entityFound && compressedFound, frameNumber, configId, pair, compareRemainingFrames);
        }

        private static void AssertGetBuffsConsistency(ValidationState state, string testName, StoragePair pair, int configId, int frameNumber, bool compareRemainingFrames)
        {
            IReadOnlyList<BuffViewData> entityBuffs = pair.EntityPerStack.BuffSystem.GetBuffs(pair.EntityPerStack.Target);
            IReadOnlyList<BuffViewData> compressedBuffs = pair.Compressed.BuffSystem.GetBuffs(pair.Compressed.Target);
            bool entityHasView = TryFindView(entityBuffs, configId, out BuffViewData entityView);
            bool compressedHasView = TryFindView(compressedBuffs, configId, out BuffViewData compressedView);

            state.ExpectEqual(testName + " 当前 configId 条目数量", CountBuffsWithConfig(entityBuffs, configId), CountBuffsWithConfig(compressedBuffs, configId), frameNumber, configId, pair, "GetBuffs count");
            state.ExpectEqual(testName + " 当前 configId 可见性", entityHasView ? 1 : 0, compressedHasView ? 1 : 0, frameNumber, configId, pair, "GetBuffs visible");
            AssertComparableView(state, testName, entityView, compressedView, entityHasView && compressedHasView, frameNumber, configId, pair, compareRemainingFrames);
        }

        private static void AssertSourceView(ValidationState state, string testName, StoragePair pair, int configId, Entity entitySource, Entity compressedSource, int frameNumber)
        {
            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, configId, entitySource, out BuffViewData entityView);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, configId, compressedSource, out BuffViewData compressedView);

            state.ExpectEqual(testName + " TryGetBuff 成功状态一致", entityFound ? 1 : 0, compressedFound ? 1 : 0, frameNumber, configId, pair, testName + " found");
            state.ExpectEqual(testName + " Stack 一致", entityFound ? entityView.Stack : -1, compressedFound ? compressedView.Stack : -1, frameNumber, configId, pair, testName + " Stack");
            state.ExpectEqual(testName + " RemainingFrames 一致", entityFound ? entityView.RemainingFrames : -1, compressedFound ? compressedView.RemainingFrames : -1, frameNumber, configId, pair, testName + " RemainingFrames");
        }

        private static void AssertStackWithinMax(ValidationState state, string testName, StoragePair pair, int configId, int maxStack, int frameNumber)
        {
            bool entityFound = pair.EntityPerStack.BuffSystem.TryGetBuff(pair.EntityPerStack.Target, configId, pair.EntityPerStack.Source, out BuffViewData entityView);
            bool compressedFound = pair.Compressed.BuffSystem.TryGetBuff(pair.Compressed.Target, configId, pair.Compressed.Source, out BuffViewData compressedView);

            state.ExpectEqual(testName + " EntityPerStack", 1, entityFound && entityView.Stack <= maxStack ? 1 : 0, frameNumber, configId, pair, "EntityPerStack Stack<=Max");
            state.ExpectEqual(testName + " Compressed", 1, compressedFound && compressedView.Stack <= maxStack ? 1 : 0, frameNumber, configId, pair, "Compressed Stack<=Max");
        }

        private static int CountBuffsWithConfig(IReadOnlyList<BuffViewData> buffs, int configId)
        {
            int count = 0;

            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].ConfigId == configId)
                    count++;
            }

            return count;
        }

        private static int SumStacksWithConfig(IReadOnlyList<BuffViewData> buffs, int configId)
        {
            int stack = 0;

            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].ConfigId == configId)
                    stack += buffs[i].Stack;
            }

            return stack;
        }

        private static bool TryFindView(IReadOnlyList<BuffViewData> buffs, int configId, out BuffViewData view)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].ConfigId != configId)
                    continue;

                view = buffs[i];
                return true;
            }

            view = default(BuffViewData);
            return false;
        }

        private readonly struct StoragePair
        {
            public readonly TestEnvironment EntityPerStack;
            public readonly TestEnvironment Compressed;

            public StoragePair(TestEnvironment entityPerStack, TestEnvironment compressed)
            {
                EntityPerStack = entityPerStack;
                Compressed = compressed;
            }
        }

        private readonly struct TestEnvironment
        {
            public readonly World World;
            public readonly Entity Target;
            public readonly Entity Source;
            public readonly BuffDefinitionRegistry Definitions;
            public readonly BuffEffectRegistry Effects;
            public readonly CountingEffect Effect;
            public readonly BuffSystemCore BuffSystem;

            public TestEnvironment(
                World world,
                Entity target,
                Entity source,
                BuffDefinitionRegistry definitions,
                BuffEffectRegistry effects,
                CountingEffect effect,
                BuffSystemCore buffSystem)
            {
                World = world;
                Target = target;
                Source = source;
                Definitions = definitions;
                Effects = effects;
                Effect = effect;
                BuffSystem = buffSystem;
            }
        }

        private sealed class CountingEffect : BuffEffectExecutorBase
        {
            public int ApplyCount { get; private set; }
            public int RefreshCount { get; private set; }
            public int StackChangedCount { get; private set; }
            public int TickCount { get; private set; }
            public int RemoveCount { get; private set; }

            public override void OnApply(in BuffEffectContext context)
            {
                ApplyCount++;
            }

            public override void OnRefresh(in BuffEffectContext context)
            {
                RefreshCount++;
            }

            public override void OnStackChanged(in BuffEffectContext context, int delta)
            {
                StackChangedCount++;
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

        private sealed class ValidationState
        {
            private readonly StringBuilder _builder = new StringBuilder();

            public bool HasFailure { get; private set; }

            public void Log(string message)
            {
                _builder.AppendLine(message);
            }

            public void ExpectEqual(string testName, int expected, int actual, int frameNumber, int configId, StoragePair pair, string fieldName)
            {
                if (expected == actual)
                {
                    _builder.Append("[F").Append(frameNumber).Append("] ").Append(testName)
                        .Append(": PASS configId=").Append(configId)
                        .Append(", target=").Append(pair.EntityPerStack.Target.ID).Append('/').Append(pair.EntityPerStack.Target.Version)
                        .Append(", source=").Append(pair.EntityPerStack.Source.ID).Append('/').Append(pair.EntityPerStack.Source.Version)
                        .Append(", field=").Append(fieldName)
                        .Append(", expected=").Append(expected)
                        .Append(", actual=").Append(actual)
                        .AppendLine();
                    return;
                }

                HasFailure = true;
                _builder.Append("[F").Append(frameNumber).Append("] ").Append(testName)
                    .Append(": FAIL configId=").Append(configId)
                    .Append(", target=").Append(pair.EntityPerStack.Target.ID).Append('/').Append(pair.EntityPerStack.Target.Version)
                    .Append(", source=").Append(pair.EntityPerStack.Source.ID).Append('/').Append(pair.EntityPerStack.Source.Version)
                    .Append(", field=").Append(fieldName)
                    .Append(", expected=").Append(expected)
                    .Append(", actual=").Append(actual)
                    .AppendLine();
            }

            public string BuildOutput()
            {
                return _builder.ToString();
            }
        }
    }
}
