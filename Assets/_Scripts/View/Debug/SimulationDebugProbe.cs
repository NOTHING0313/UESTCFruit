using BuffSystem;
using Contracts;
using ECSFrameWork;
using System.Collections.Generic;

namespace View
{
    /// <summary>
    /// 从 World 和 Runner 提取只读调试数据。
    /// </summary>
    public class SimulationDebugProbe : IDebugProbe
    {
        private readonly World _world;
        private readonly BuffSystemCore _buffSystem;
        private readonly SimulateRunner _runner;
        private Entity _debugTarget;
        private Entity _debugSource;

        public SimulationDebugProbe(World world, BuffSystemCore buffSystem, SimulateRunner runner)
        {
            _world = world;
            _buffSystem = buffSystem;
            _runner = runner;
        }

        public int CurrentFrame => _runner?.FrameCount ?? 0;
        public bool IsRollbacking => false;
        public uint CurrentChecksum => 0;
        public int EntityCount => _world?.AliveEntityCount ?? 0;
        internal Entity DebugTarget => _debugTarget;
        internal Entity DebugSource => _debugSource;

        public IReadOnlyList<BuffViewData> GetBuffs(Entity entity)
        {
            return _buffSystem == null ? System.Array.Empty<BuffViewData>() : _buffSystem.GetBuffs(entity);
        }

        internal void EnsureDebugEntities()
        {
            if (_world == null)
                return;

            if (!_debugTarget.IsValid || !_world.IsAlive(_debugTarget))
                _debugTarget = _world.CreateEntity();

            if (!_debugSource.IsValid || !_world.IsAlive(_debugSource))
                _debugSource = _debugTarget;
        }

        internal bool TrySetDebugTarget(Entity entity)
        {
            if (_world == null || !entity.IsValid || !_world.IsAlive(entity))
                return false;

            _debugTarget = entity;

            if (!_debugSource.IsValid || !_world.IsAlive(_debugSource))
                _debugSource = entity;

            return true;
        }

        internal bool TrySetDebugSource(Entity entity)
        {
            if (_world == null || !entity.IsValid || !_world.IsAlive(entity))
                return false;

            _debugSource = entity;
            return true;
        }

        internal BuffDebugSnapshot CaptureBuffDebug(int configId, Entity target, Entity source)
        {
            BuffDebugSnapshot snapshot = new BuffDebugSnapshot
            {
                ConfigId = configId,
                Target = target,
                Source = source.IsValid ? source : target
            };

            if (_world == null || _buffSystem == null || !target.IsValid || !_world.IsAlive(target))
                return snapshot;

            snapshot.TargetAlive = true;
            snapshot.SourceAlive = snapshot.Source.IsValid && _world.IsAlive(snapshot.Source);
            snapshot.Found = _buffSystem.TryGetBuff(target, configId, snapshot.Source, out snapshot.View);

            IReadOnlyList<BuffViewData> views = _buffSystem.GetBuffs(target);
            snapshot.ViewRows = new List<BuffDebugViewRow>();

            if (views != null)
            {
                snapshot.GetBuffsCount = views.Count;

                for (int i = 0; i < views.Count; i++)
                {
                    BuffViewData view = views[i];
                    snapshot.ViewRows.Add(new BuffDebugViewRow(view));

                    if (view.ConfigId == configId)
                        snapshot.MatchingViewCount++;
                }
            }

            _world.ForEach<BuffRuntimeComponent>((Entity entity, ref BuffRuntimeComponent runtime) =>
            {
                snapshot.EntityPerStackRuntimeCount++;

                if (runtime.configId == configId && runtime.target == target)
                    snapshot.ConfigEntityPerStackRuntimeCount++;
            });

            _world.ForEach<CompressedParallelBuffRuntimeComponent>((Entity entity, ref CompressedParallelBuffRuntimeComponent runtime) =>
            {
                snapshot.CompressedRuntimeCount++;

                if (runtime.configId == configId && runtime.target == target)
                    snapshot.ConfigCompressedRuntimeCount++;
            });

            return snapshot;
        }

        internal BuffDebugSnapshot AddBuff(int configId, int stack, Entity target, Entity source)
        {
            EnsureDebugEntities();
            Entity effectiveTarget = target.IsValid ? target : _debugTarget;
            Entity effectiveSource = source.IsValid ? source : effectiveTarget;

            if (_buffSystem != null)
                _buffSystem.AddBuff(new AddBuffCommand(effectiveTarget, configId, effectiveSource, stack));

            return CaptureBuffDebug(configId, effectiveTarget, effectiveSource);
        }

        internal BuffDebugSnapshot RemoveBuff(int configId, int stack, Entity target, Entity source)
        {
            EnsureDebugEntities();
            Entity effectiveTarget = target.IsValid ? target : _debugTarget;
            Entity effectiveSource = source.IsValid ? source : effectiveTarget;

            if (_buffSystem != null)
                _buffSystem.RemoveBuff(new RemoveBuffCommand(effectiveTarget, configId, effectiveSource, stack));

            return CaptureBuffDebug(configId, effectiveTarget, effectiveSource);
        }

        internal bool TickFrames(int frameCount)
        {
            if (_runner == null)
                return false;

            int count = frameCount > 0 ? frameCount : 1;
            bool ticked = false;

            for (int i = 0; i < count; i++)
                ticked |= _runner.StepNextFrame(false);

            return ticked;
        }
    }

    internal struct BuffDebugSnapshot
    {
        public int ConfigId;
        public Entity Target;
        public Entity Source;
        public bool TargetAlive;
        public bool SourceAlive;
        public bool Found;
        public BuffViewData View;
        public int GetBuffsCount;
        public int MatchingViewCount;
        public int EntityPerStackRuntimeCount;
        public int CompressedRuntimeCount;
        public int ConfigEntityPerStackRuntimeCount;
        public int ConfigCompressedRuntimeCount;
        public List<BuffDebugViewRow> ViewRows;
    }

    [System.Serializable]
    internal struct BuffDebugViewRow
    {
        public int ConfigId;
        public int Stack;
        public int RemainingFrames;
        public int RuntimeHandle;
        public string Target;
        public string Source;

        public BuffDebugViewRow(BuffViewData view)
        {
            ConfigId = view.ConfigId;
            Stack = view.Stack;
            RemainingFrames = view.RemainingFrames;
            RuntimeHandle = view.RuntimeHandle;
            Target = FormatEntity(view.Target);
            Source = FormatEntity(view.Source);
        }

        private static string FormatEntity(Entity entity)
        {
            return entity.IsValid ? $"{entity.ID}/{entity.Version}" : "Invalid";
        }
    }
}
