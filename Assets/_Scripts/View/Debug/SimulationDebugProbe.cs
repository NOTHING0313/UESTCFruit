using BuffSystem;
using Contracts;
using ECSFrameWork;
using System.Collections.Generic;

namespace View
{
    public class SimulationDebugProbe : IDebugProbe
    {
        private readonly World _world;
        private readonly BuffSystemCore _buffSystem;
        private readonly SimulateRunner _runner;
        private int _snapshotCount;
        private int _lastRollbackFrame = -1;

        public int SnapshotCount => _snapshotCount;
        public int LastRollbackFrame => _lastRollbackFrame;
        private bool _isRollbacking;
        private uint _currentChecksum;
        private Entity _debugTarget = Entity.Invalid;
        private Entity _debugSource = Entity.Invalid;

        public SimulationDebugProbe(World world, BuffSystemCore buffSystem, SimulateRunner runner)
        {
            _world = world;
            _buffSystem = buffSystem;
            _runner = runner;
        }

        /// <summary>更新回滚状态，由 SimulationInitializer 每帧调用。</summary>
        public void SetRollbackInfo(bool isRollbacking, uint checksum)
        {
            _isRollbacking = isRollbacking;
            _currentChecksum = checksum;
        }

        public void SetRollbackInfo(bool isRollbacking, uint checksum, int snapshotCount, int lastRollbackFrame)
        {
            _isRollbacking = isRollbacking;
            _currentChecksum = checksum;
            _snapshotCount = snapshotCount;
            _lastRollbackFrame = lastRollbackFrame;
        }

        public int CurrentFrame => _runner?.FrameCount ?? 0;
        public bool IsRollbacking => _isRollbacking;
        public uint CurrentChecksum => _currentChecksum;
        public int EntityCount => _world?.AliveEntityCount ?? 0;
        internal Entity DebugTarget => _debugTarget;
        internal Entity DebugSource => _debugSource;

        public IReadOnlyList<BuffViewData> GetBuffs(Entity entity)
        {
            return _buffSystem.GetBuffs(entity);
        }

        /// <summary>确保 View Debug 面板拥有一组存活的 Target / Source 调试实体。</summary>
        internal void EnsureDebugEntities()
        {
            if (_world == null)
                return;

            if (!_debugTarget.IsValid || !_world.IsAlive(_debugTarget))
                _debugTarget = _world.CreateEntity();

            if (!_debugSource.IsValid || !_world.IsAlive(_debugSource))
                _debugSource = _debugTarget;
        }

        /// <summary>尝试把外部输入的 Entity 设为当前调试 Target。</summary>
        internal bool TrySetDebugTarget(Entity target)
        {
            if (_world == null || !target.IsValid || !_world.IsAlive(target))
                return false;

            _debugTarget = target;
            if (!_debugSource.IsValid || !_world.IsAlive(_debugSource))
                _debugSource = _debugTarget;

            return true;
        }

        /// <summary>尝试把外部输入的 Entity 设为当前调试 Source。</summary>
        internal bool TrySetDebugSource(Entity source)
        {
            if (_world == null || !source.IsValid || !_world.IsAlive(source))
                return false;

            _debugSource = source;
            return true;
        }

        /// <summary>向 BuffSystem 入队 AddBuff，并返回入队后的只读调试快照。</summary>
        internal BuffDebugSnapshot AddBuff(int configId, int stack, Entity target, Entity source)
        {
            if (_buffSystem != null)
                _buffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));

            return CaptureBuffDebug(configId, target, source);
        }

        /// <summary>向 BuffSystem 入队 RemoveBuff，并返回入队后的只读调试快照。</summary>
        internal BuffDebugSnapshot RemoveBuff(int configId, int stack, Entity target, Entity source)
        {
            if (_buffSystem != null)
                _buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, stack));

            return CaptureBuffDebug(configId, target, source);
        }

        /// <summary>通过生产 Runner 推进固定帧；Debug 面板不直接调用 BuffSystemCore.Tick。</summary>
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

        /// <summary>捕获当前 Buff 查询与 runtime 统计；该方法只读 World / BuffSystem，不修改运行时状态。</summary>
        internal BuffDebugSnapshot CaptureBuffDebug(int configId, Entity target, Entity source)
        {
            Entity querySource = source.IsValid ? source : target;
            BuffDebugSnapshot snapshot = new BuffDebugSnapshot
            {
                ConfigId = configId,
                Target = target,
                Source = querySource,
                TargetAlive = _world != null && target.IsValid && _world.IsAlive(target),
                SourceAlive = _world != null && querySource.IsValid && _world.IsAlive(querySource)
            };

            if (_world == null || _buffSystem == null || !snapshot.TargetAlive)
                return snapshot;

            snapshot.Found = _buffSystem.TryGetBuff(target, configId, querySource, out snapshot.View);

            IReadOnlyList<BuffViewData> views = _buffSystem.GetBuffs(target);
            if (views != null)
            {
                snapshot.GetBuffsCount = views.Count;
                for (int i = 0; i < views.Count; i++)
                {
                    BuffViewData view = views[i];
                    snapshot.ViewRows.Add(new BuffDebugViewRow(in view));
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
    }
}
