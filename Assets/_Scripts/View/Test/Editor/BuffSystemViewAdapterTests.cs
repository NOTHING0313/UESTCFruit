using BuffSystem;
using ECSFrameWork;
using System;
using System.Collections.Generic;
using View;

namespace View.EditorTesting
{
    internal static class BuffSystemViewAdapterTests
    {
        public static void RunAll()
        {
            NullBuffSystemReturnsEmpty();
            EmptyBuffListReturnsEmpty();
            SingleBuffBuildsViewModel();
            MultipleBuffsBuildMultipleViewModels();
            MissingDefinitionUsesFallbackText();
            ResolvedDefinitionUsesEffectAndName();
            AdapterUsesOnlyPublicQuery();
        }

        private static void NullBuffSystemReturnsEmpty()
        {
            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter();
            IReadOnlyList<BuffViewModel> models = adapter.BuildViewModels(null, new Entity(1, 1));
            AssertEqual(0, models.Count, nameof(NullBuffSystemReturnsEmpty));
        }

        private static void EmptyBuffListReturnsEmpty()
        {
            FakeBuffSystem buffSystem = new FakeBuffSystem();
            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter();

            IReadOnlyList<BuffViewModel> models = adapter.BuildViewModels(buffSystem, new Entity(1, 1));

            AssertEqual(0, models.Count, nameof(EmptyBuffListReturnsEmpty));
            AssertEqual(1, buffSystem.GetBuffsCallCount, nameof(EmptyBuffListReturnsEmpty));
        }

        private static void SingleBuffBuildsViewModel()
        {
            Entity owner = new Entity(10, 1);
            Entity source = new Entity(20, 1);
            FakeBuffSystem buffSystem = new FakeBuffSystem();
            buffSystem.SetBuffs(owner, new BuffViewData(owner, source, 1001, 2, 30, 7));

            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter();
            IReadOnlyList<BuffViewModel> models = adapter.BuildViewModels(buffSystem, owner);

            AssertEqual(1, models.Count, nameof(SingleBuffBuildsViewModel));
            AssertEqual(1001, models[0].ConfigId, nameof(SingleBuffBuildsViewModel));
            AssertEqual(2, models[0].Stack, nameof(SingleBuffBuildsViewModel));
            AssertEqual(30, models[0].RemainingFrames, nameof(SingleBuffBuildsViewModel));
            AssertEqual(20, models[0].SourceEntity, nameof(SingleBuffBuildsViewModel));
        }

        private static void MultipleBuffsBuildMultipleViewModels()
        {
            Entity owner = new Entity(11, 1);
            Entity source = new Entity(21, 1);
            FakeBuffSystem buffSystem = new FakeBuffSystem();
            buffSystem.SetBuffs(
                owner,
                new BuffViewData(owner, source, 1001, 1, 30, 7),
                new BuffViewData(owner, source, 1002, 3, 60, 8));

            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter();
            IReadOnlyList<BuffViewModel> models = adapter.BuildViewModels(buffSystem, owner);

            AssertEqual(2, models.Count, nameof(MultipleBuffsBuildMultipleViewModels));
            AssertEqual(1001, models[0].ConfigId, nameof(MultipleBuffsBuildMultipleViewModels));
            AssertEqual(1002, models[1].ConfigId, nameof(MultipleBuffsBuildMultipleViewModels));
        }

        private static void MissingDefinitionUsesFallbackText()
        {
            Entity owner = new Entity(12, 1);
            Entity source = new Entity(22, 1);
            FakeBuffSystem buffSystem = new FakeBuffSystem();
            buffSystem.SetBuffs(owner, new BuffViewData(owner, source, 1003, 1, 45, 9));

            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter(new FakeResolver());
            IReadOnlyList<BuffViewModel> models = adapter.BuildViewModels(buffSystem, owner);

            AssertEqual("N/A", models[0].EffectIdText, nameof(MissingDefinitionUsesFallbackText));
            AssertEqual("Buff 1003", models[0].DebugName, nameof(MissingDefinitionUsesFallbackText));
        }

        private static void ResolvedDefinitionUsesEffectAndName()
        {
            Entity owner = new Entity(13, 1);
            Entity source = new Entity(23, 1);
            FakeBuffSystem buffSystem = new FakeBuffSystem();
            buffSystem.SetBuffs(owner, new BuffViewData(owner, source, 1004, 1, 45, 9));

            FakeResolver resolver = new FakeResolver();
            resolver.SetDefinition(1004, new BuffViewDefinitionViewData(1004, 990101, "Debug Smoke"));

            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter(resolver);
            IReadOnlyList<BuffViewModel> models = adapter.BuildViewModels(buffSystem, owner);

            AssertEqual("990101", models[0].EffectIdText, nameof(ResolvedDefinitionUsesEffectAndName));
            AssertEqual("Debug Smoke", models[0].DebugName, nameof(ResolvedDefinitionUsesEffectAndName));
        }

        private static void AdapterUsesOnlyPublicQuery()
        {
            Entity owner = new Entity(14, 1);
            FakeBuffSystem buffSystem = new FakeBuffSystem();
            BuffSystemViewAdapter adapter = new BuffSystemViewAdapter();

            adapter.BuildViewModels(buffSystem, owner);

            AssertEqual(1, buffSystem.GetBuffsCallCount, nameof(AdapterUsesOnlyPublicQuery));
            AssertEqual(0, buffSystem.AddBuffCallCount, nameof(AdapterUsesOnlyPublicQuery));
            AssertEqual(0, buffSystem.RemoveBuffCallCount, nameof(AdapterUsesOnlyPublicQuery));
            AssertEqual(0, buffSystem.RaiseCallCount, nameof(AdapterUsesOnlyPublicQuery));
            AssertEqual(0, buffSystem.TickCallCount, nameof(AdapterUsesOnlyPublicQuery));
        }

        private static void AssertEqual<T>(T expected, T actual, string caseName)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{caseName}: expected={expected}, actual={actual}");
        }

        private sealed class FakeResolver : IBuffViewDefinitionResolver
        {
            private readonly Dictionary<int, BuffViewDefinitionViewData> _definitions = new Dictionary<int, BuffViewDefinitionViewData>();

            public void SetDefinition(int configId, BuffViewDefinitionViewData viewData)
            {
                _definitions[configId] = viewData;
            }

            public bool TryResolve(int configId, out BuffViewDefinitionViewData viewData)
            {
                return _definitions.TryGetValue(configId, out viewData);
            }
        }

        private sealed class FakeBuffSystem : IBuffSystem
        {
            private readonly Dictionary<Entity, IReadOnlyList<BuffViewData>> _buffs = new Dictionary<Entity, IReadOnlyList<BuffViewData>>();

            public int AddBuffCallCount { get; private set; }
            public int RemoveBuffCallCount { get; private set; }
            public int RaiseCallCount { get; private set; }
            public int TickCallCount { get; private set; }
            public int GetBuffsCallCount { get; private set; }

            public void SetBuffs(Entity target, params BuffViewData[] buffs)
            {
                _buffs[target] = buffs ?? Array.Empty<BuffViewData>();
            }

            public void Tick(World world, SimulationContext context)
            {
                TickCallCount++;
            }

            public void AddBuff(AddBuffCommand command)
            {
                AddBuffCallCount++;
            }

            public void RemoveBuff(RemoveBuffCommand command)
            {
                RemoveBuffCallCount++;
            }

            public void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent)
                where TEvent : struct, IGameEvent
            {
                RaiseCallCount++;
            }

            public bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data)
            {
                if (_buffs.TryGetValue(target, out IReadOnlyList<BuffViewData> buffs))
                {
                    for (int i = 0; i < buffs.Count; i++)
                    {
                        BuffViewData candidate = buffs[i];
                        if (candidate.ConfigId == configId && candidate.Source == source)
                        {
                            data = candidate;
                            return true;
                        }
                    }
                }

                data = default;
                return false;
            }

            public IReadOnlyList<BuffViewData> GetBuffs(Entity target)
            {
                GetBuffsCallCount++;
                return _buffs.TryGetValue(target, out IReadOnlyList<BuffViewData> buffs)
                    ? buffs
                    : Array.Empty<BuffViewData>();
            }
        }
    }
}
