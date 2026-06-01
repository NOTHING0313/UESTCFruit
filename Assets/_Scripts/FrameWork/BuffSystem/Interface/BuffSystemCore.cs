using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// 纯 ECS Buff 运行时；不持有 Unity 对象引用，只随 SimulationContext 固定帧推进。
    /// </summary>
    public class BuffSystemCore : IBuffSystem, IDisposable
    {
        private const int RuntimeLookupRetainFrames = 8;

        private readonly struct BuffRuntimeKey : IEquatable<BuffRuntimeKey>
        {
            public readonly Entity target;
            public readonly Entity source;
            public readonly int configId;

            public BuffRuntimeKey(Entity target, Entity source, int configId)
            {
                this.target = target;
                this.source = source.IsValid ? source : Entity.Invalid;
                this.configId = configId;
            }

            public bool Equals(BuffRuntimeKey other)
            {
                return target == other.target && source == other.source && configId == other.configId;
            }

            public override bool Equals(object obj)
            {
                return obj is BuffRuntimeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = target.GetHashCode();
                    hash = (hash * 397) ^ source.GetHashCode();
                    hash = (hash * 397) ^ configId;
                    return hash;
                }
            }
        }

        private readonly struct QueuedCommand
        {
            public readonly bool isAdd;
            public readonly AddBuffCommand addCommand;
            public readonly RemoveBuffCommand removeCommand;

            private QueuedCommand(AddBuffCommand command)
            {
                isAdd = true;
                addCommand = command;
                removeCommand = default;
            }

            private QueuedCommand(RemoveBuffCommand command)
            {
                isAdd = false;
                addCommand = default;
                removeCommand = command;
            }

            public static QueuedCommand Add(AddBuffCommand command) => new QueuedCommand(command);
            public static QueuedCommand Remove(RemoveBuffCommand command) => new QueuedCommand(command);
        }

        private readonly struct BuffEventCandidate
        {
            public readonly Entity runtimeEntity;
            public readonly BuffRuntimeComponent runtime;
            public readonly BuffDefinition definition;

            public BuffEventCandidate(Entity runtimeEntity, in BuffRuntimeComponent runtime, in BuffDefinition definition)
            {
                this.runtimeEntity = runtimeEntity;
                this.runtime = runtime;
                this.definition = definition;
            }
        }

        private readonly struct BuffEffectRequest
        {
            public readonly int frameNumber;
            public readonly int sequence;
            public readonly BuffEffectPhase phase;
            public readonly Entity runtimeEntity;
            public readonly Entity target;
            public readonly Entity source;
            public readonly int configId;
            public readonly int effectId;
            public readonly int priority;
            public readonly int runtimeHandle;
            public readonly int stack;
            public readonly int stackDelta;
            public readonly int remainingFrames;
            public readonly int elapsedFrames;
            public readonly int ticks;
            public readonly BuffRuntimeComponent runtimeSnapshot;

            public BuffEffectRequest(int frameNumber, int sequence, BuffEffectPhase phase, Entity runtimeEntity, in BuffRuntimeComponent runtime, in BuffDefinition definition, int stackDelta)
            {
                this.frameNumber = frameNumber;
                this.sequence = sequence;
                this.phase = phase;
                this.runtimeEntity = runtimeEntity;
                target = runtime.target;
                source = runtime.source;
                configId = runtime.configId;
                effectId = definition.EffectId;
                priority = definition.Priority;
                runtimeHandle = runtime.runtimeHandle;
                stack = runtime.stack;
                this.stackDelta = stackDelta;
                remainingFrames = runtime.remainingFrames;
                elapsedFrames = runtime.elapsedFrames;
                ticks = runtime.ticks;
                runtimeSnapshot = runtime;
            }
        }

        private readonly struct PendingRemoveRuntime
        {
            public readonly Entity runtimeEntity;
            public readonly int runtimeHandle;

            public PendingRemoveRuntime(Entity runtimeEntity, int runtimeHandle)
            {
                this.runtimeEntity = runtimeEntity;
                this.runtimeHandle = runtimeHandle;
            }
        }

        private sealed class RuntimeRemovalComparer : IComparer<Entity>
        {
            private BuffSystemCore _owner;
            private World _world;
            private ParallelBuffStackDownPolicy _policy;

            public void Reset(BuffSystemCore owner, World world, ParallelBuffStackDownPolicy policy)
            {
                _owner = owner;
                _world = world;
                _policy = policy;
            }

            public int Compare(Entity left, Entity right)
            {
                return _owner.CompareRuntimeForRemoval(_world, left, right, _policy);
            }
        }

        private sealed class BuffEventCandidateComparer : IComparer<BuffEventCandidate>
        {
            public int Compare(BuffEventCandidate left, BuffEventCandidate right)
            {
                int priorityCompare = left.definition.Priority.CompareTo(right.definition.Priority);

                if (priorityCompare != 0)
                    return priorityCompare;

                int handleCompare = left.runtime.runtimeHandle.CompareTo(right.runtime.runtimeHandle);

                if (handleCompare != 0)
                    return handleCompare;

                return CompareEntity(left.runtimeEntity, right.runtimeEntity);
            }
        }

        private sealed class BuffEffectRequestComparer : IComparer<BuffEffectRequest>
        {
            public int Compare(BuffEffectRequest left, BuffEffectRequest right)
            {
                int frameCompare = left.frameNumber.CompareTo(right.frameNumber);

                if (frameCompare != 0)
                    return frameCompare;

                int phaseCompare = GetLifecyclePhaseOrder(left.phase).CompareTo(GetLifecyclePhaseOrder(right.phase));

                if (phaseCompare != 0)
                    return phaseCompare;

                int priorityCompare = left.priority.CompareTo(right.priority);

                if (priorityCompare != 0)
                    return priorityCompare;

                int handleCompare = left.runtimeHandle.CompareTo(right.runtimeHandle);

                if (handleCompare != 0)
                    return handleCompare;

                int entityCompare = CompareEntity(left.runtimeEntity, right.runtimeEntity);

                if (entityCompare != 0)
                    return entityCompare;

                return left.sequence.CompareTo(right.sequence);
            }
        }

        private sealed class PendingRemoveRuntimeComparer : IComparer<PendingRemoveRuntime>
        {
            public int Compare(PendingRemoveRuntime left, PendingRemoveRuntime right)
            {
                int handleCompare = left.runtimeHandle.CompareTo(right.runtimeHandle);

                if (handleCompare != 0)
                    return handleCompare;

                return CompareEntity(left.runtimeEntity, right.runtimeEntity);
            }
        }

        private readonly IBuffDefinitionProvider _definitionProvider;
        private readonly BuffEffectRegistry _effectRegistry;
        private readonly RuntimeRemovalComparer _runtimeRemovalComparer = new RuntimeRemovalComparer();
        private readonly BuffEventCandidateComparer _eventCandidateComparer = new BuffEventCandidateComparer();
        private readonly BuffEffectRequestComparer _effectRequestComparer = new BuffEffectRequestComparer();
        private readonly PendingRemoveRuntimeComparer _pendingRemoveRuntimeComparer = new PendingRemoveRuntimeComparer();

        private readonly List<QueuedCommand> _queuedCommands = new List<QueuedCommand>();
        private readonly List<BuffEffectRequest> _pendingLifecycleEffects = new List<BuffEffectRequest>(64);
        private readonly List<BuffEffectRequest> _executingLifecycleEffects = new List<BuffEffectRequest>(64);
        private readonly List<PendingRemoveRuntime> _pendingRemoveRuntimes = new List<PendingRemoveRuntime>(32);
        private readonly List<Entity> _runtimeEntities = new List<Entity>(128);
        private readonly List<Entity> _runtimeEntitiesThisFrame = new List<Entity>(128);
        private readonly List<Entity> _createdRuntimeEntitiesThisFrame = new List<Entity>(32);
        private readonly List<Entity> _addRequestEntities = new List<Entity>(32);
        private readonly List<Entity> _removeRequestEntities = new List<Entity>(32);
        private readonly List<Entity> _requestEntities = new List<Entity>(64);
        private readonly List<Entity> _scratchEntities = new List<Entity>(32);
        private readonly List<BuffRuntimeKey> _removeLookupKeys = new List<BuffRuntimeKey>(32);
        private readonly List<BuffEventCandidate> _eventCandidates = new List<BuffEventCandidate>(32);
        private readonly HashSet<Entity> _eventCandidateEntitySet = new HashSet<Entity>();
        private readonly HashSet<Entity> _pendingRemoveRuntimeSet = new HashSet<Entity>();
        private readonly Dictionary<BuffRuntimeKey, List<Entity>> _runtimeEntitiesByKey = new Dictionary<BuffRuntimeKey, List<Entity>>();
        private readonly Dictionary<BuffRuntimeKey, int> _runtimeLookupUnusedFrames = new Dictionary<BuffRuntimeKey, int>();
        private readonly Dictionary<int, List<Entity>> _eventRuntimeEntitiesByEventId = new Dictionary<int, List<Entity>>();
        private readonly Dictionary<Entity, BuffRuntimeComponent> _pendingRuntimeComponents = new Dictionary<Entity, BuffRuntimeComponent>();
        // 新建 Runtime 的结构变更会延迟播放；这里保留本帧快照，保证同帧 ViewCache 读取不丢数据。
        private readonly Dictionary<Entity, BuffRuntimeComponent> _createdRuntimeComponentsThisFrame = new Dictionary<Entity, BuffRuntimeComponent>();
        private readonly Dictionary<BuffRuntimeKey, BuffViewData> _viewByKey = new Dictionary<BuffRuntimeKey, BuffViewData>();
        private readonly Dictionary<Entity, List<BuffViewData>> _viewsByTarget = new Dictionary<Entity, List<BuffViewData>>();
        private readonly HashSet<Entity> _validTargetViewCache = new HashSet<Entity>();

        private bool _viewCacheDirty = true;
        private bool _eventRuntimeIndexDirty = true;
        private int _nextLifecycleEffectSequence;
        private int _runtimeSnapshotFrameNumber = -1;
        private int _eventRuntimeIndexFrameNumber = -1;

        private World _queryWorld;
        private EntityQueryDescription _runtimeQuery;
        private EntityQueryDescription _addRequestQuery;
        private EntityQueryDescription _removeRequestQuery;

        public BuffSystemCore()
            : this(new BuffDefinitionRegistry(), new BuffEffectRegistry())
        {
        }

        public BuffSystemCore(IBuffDefinitionProvider definitionProvider, BuffEffectRegistry effectRegistry = null)
        {
            _definitionProvider = definitionProvider ?? new BuffDefinitionRegistry();
            _effectRegistry = effectRegistry ?? new BuffEffectRegistry();
        }

        public void Tick(World world, SimulationContext context)
        {
            if (world == null)
                return;

            EnsureQueries(world);
            _pendingRuntimeComponents.Clear();
            _nextLifecycleEffectSequence = 0;
            CaptureRuntimeEntities(world, context.frameNumber);
            RebuildRuntimeLookup(world);
            ConsumeRequestComponents(world, in context);
            ConsumeQueuedCommands(world, in context);
            TickRuntimeBuffs(world, in context);
            FlushLifecycleEffects(world, in context);
            DestroyPendingRemoveRuntimes(world);
            _pendingRuntimeComponents.Clear();
        }

        public void AddBuff(AddBuffCommand command)
        {
            if (command.IsValid)
                _queuedCommands.Add(QueuedCommand.Add(command));
        }

        public void RemoveBuff(RemoveBuffCommand command)
        {
            if (command.IsValid)
                _queuedCommands.Add(QueuedCommand.Remove(command));
        }

        public void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            if (world == null || gameEvent.EventId <= 0)
                return;

            EnsureQueries(world);
            CollectEventCandidates(world, in context, in gameEvent);
            // Event 阶段按 Priority、runtimeHandle、RuntimeEntity.ID、RuntimeEntity.Version 稳定排序，避免响应顺序受查询或字典遍历影响。
            _eventCandidates.Sort(_eventCandidateComparer);

            for (int i = 0; i < _eventCandidates.Count; i++)
            {
                BuffEventCandidate candidate = _eventCandidates[i];
                RunEventEffect(world, in context, in candidate, in gameEvent);
            }

            _eventCandidates.Clear();
            _eventCandidateEntitySet.Clear();
        }

        public bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data)
        {
            EnsureViewCache();
            BuffRuntimeKey key = new BuffRuntimeKey(target, source, configId);
            return _viewByKey.TryGetValue(key, out data);
        }

        public IReadOnlyList<BuffViewData> GetBuffs(Entity target)
        {
            EnsureViewCache();

            if (_validTargetViewCache.Contains(target))
                return _viewsByTarget.TryGetValue(target, out List<BuffViewData> cachedBuffs)
                    ? cachedBuffs
                    : Array.Empty<BuffViewData>();

            List<BuffViewData> buffs = GetOrCreateTargetViewList(target);
            buffs.Clear();

            foreach (KeyValuePair<BuffRuntimeKey, BuffViewData> pair in _viewByKey)
            {
                if (pair.Key.target == target)
                    buffs.Add(pair.Value);
            }

            _validTargetViewCache.Add(target);
            return buffs;
        }

        public void Dispose()
        {
            _queuedCommands.Clear();
            _pendingLifecycleEffects.Clear();
            _executingLifecycleEffects.Clear();
            _pendingRemoveRuntimes.Clear();
            _runtimeEntities.Clear();
            _runtimeEntitiesThisFrame.Clear();
            _createdRuntimeEntitiesThisFrame.Clear();
            _addRequestEntities.Clear();
            _removeRequestEntities.Clear();
            _requestEntities.Clear();
            _scratchEntities.Clear();
            _removeLookupKeys.Clear();
            _eventCandidates.Clear();
            _eventCandidateEntitySet.Clear();
            _pendingRemoveRuntimeSet.Clear();
            _runtimeEntitiesByKey.Clear();
            _runtimeLookupUnusedFrames.Clear();
            _eventRuntimeEntitiesByEventId.Clear();
            _pendingRuntimeComponents.Clear();
            _createdRuntimeComponentsThisFrame.Clear();
            _viewByKey.Clear();
            _viewsByTarget.Clear();
            _validTargetViewCache.Clear();
            _viewCacheDirty = true;
            _eventRuntimeIndexDirty = true;
            _nextLifecycleEffectSequence = 0;
            _runtimeSnapshotFrameNumber = -1;
            _eventRuntimeIndexFrameNumber = -1;
            _queryWorld = null;
        }

        private void EnsureQueries(World world)
        {
            if (_queryWorld == world)
                return;

            _runtimeQuery = world.Query().With<BuffRuntimeComponent>().BuildDescription();
            _addRequestQuery = world.Query().With<AddBuffRequestComponent>().BuildDescription();
            _removeRequestQuery = world.Query().With<RemoveBuffRequestComponent>().BuildDescription();
            _queryWorld = world;
            _runtimeSnapshotFrameNumber = -1;
            MarkEventRuntimeIndexDirty();
        }

        private void CaptureRuntimeEntities(World world, int frameNumber)
        {
            // 每帧只捕获一次 Runtime 查询快照，后续 lookup、Tick 和 ViewCache 都复用它。
            _runtimeEntitiesThisFrame.Clear();
            _createdRuntimeEntitiesThisFrame.Clear();
            _createdRuntimeComponentsThisFrame.Clear();
            world.FillQuery(_runtimeQuery, _runtimeEntitiesThisFrame, true);
            _runtimeSnapshotFrameNumber = frameNumber;
            MarkEventRuntimeIndexDirty();
        }

        private void ConsumeRequestComponents(World world, in SimulationContext context)
        {
            _addRequestEntities.Clear();
            _removeRequestEntities.Clear();
            _requestEntities.Clear();

            world.FillQuery(_addRequestQuery, _addRequestEntities, true);
            world.FillQuery(_removeRequestQuery, _removeRequestEntities, true);

            for (int i = 0; i < _addRequestEntities.Count; i++)
                _requestEntities.Add(_addRequestEntities[i]);

            for (int i = 0; i < _removeRequestEntities.Count; i++)
                _requestEntities.Add(_removeRequestEntities[i]);

            _requestEntities.Sort(CompareEntity);

            Entity lastEntity = Entity.Invalid;
            for (int i = 0; i < _requestEntities.Count; i++)
            {
                Entity requestEntity = _requestEntities[i];

                if (requestEntity == lastEntity)
                    continue;

                lastEntity = requestEntity;

                if (world.TryGetComponent(requestEntity, out RemoveBuffRequestComponent removeRequest))
                    ApplyRemoveCommand(world, in context, removeRequest.command);

                if (world.TryGetComponent(requestEntity, out AddBuffRequestComponent addRequest))
                    ApplyAddCommand(world, in context, addRequest.command);

                world.DestroyEntity(requestEntity);
            }
        }

        private void ConsumeQueuedCommands(World world, in SimulationContext context)
        {
            for (int i = 0; i < _queuedCommands.Count; i++)
            {
                QueuedCommand queuedCommand = _queuedCommands[i];

                if (queuedCommand.isAdd)
                    ApplyAddCommand(world, in context, queuedCommand.addCommand);
                else
                    ApplyRemoveCommand(world, in context, queuedCommand.removeCommand);
            }

            _queuedCommands.Clear();
        }

        private void ApplyAddCommand(World world, in SimulationContext context, AddBuffCommand command)
        {
            if (!command.IsValid || !world.IsAlive(command.Target))
                return;

            if (!_definitionProvider.TryGetDefinition(command.ConfigId, out BuffDefinition definition))
                return;

            if (definition.BuffType == BuffInstanceType.parallel)
            {
                ApplyParallelAdd(world, in context, command, in definition);
                return;
            }

            ApplyNormalAdd(world, in context, command, in definition);
        }

        private void ApplyNormalAdd(World world, in SimulationContext context, AddBuffCommand command, in BuffDefinition definition)
        {
            BuffRuntimeKey key = new BuffRuntimeKey(command.Target, command.Source, command.ConfigId);

            if (TryGetFirstRuntimeEntity(world, key, BuffInstanceType.normal, out Entity runtimeEntity))
            {
                if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                    return;

                int beforeStack = runtime.stack;
                ApplyNormalStackPolicy(ref runtime, in definition, command.Stack);
                WriteRuntimeComponent(world, runtimeEntity, runtime);
                QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.Refresh, 0);

                int delta = runtime.stack - beforeStack;
                if (delta != 0)
                    QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.StackChanged, delta);

                return;
            }

            int stack = ClampStack(command.Stack, definition.Unlimited, definition.MaxStack);
            CreateRuntimeBuffEntity(world, in context, command, in definition, stack, true);
        }

        private void ApplyParallelAdd(World world, in SimulationContext context, AddBuffCommand command, in BuffDefinition definition)
        {
            BuffRuntimeKey key = new BuffRuntimeKey(command.Target, command.Source, command.ConfigId);
            int incoming = command.Stack;

            switch (definition.ParallelStackUpPolicy)
            {
                case ParallelBuffStackUpPolicy.RefreshEarliest:
                    incoming -= RefreshParallelStacks(world, in context, key, definition, incoming, false);
                    AppendParallelStacks(world, in context, command, in definition, incoming);
                    break;

                case ParallelBuffStackUpPolicy.RefreshAll:
                    RefreshParallelStacks(world, in context, key, definition, int.MaxValue, true);
                    AppendParallelStacks(world, in context, command, in definition, incoming);
                    break;

                case ParallelBuffStackUpPolicy.ReplaceEarliestWhenFull:
                    ReplaceOrAppendParallelStacks(world, in context, command, in definition, incoming);
                    break;

                case ParallelBuffStackUpPolicy.Append:
                default:
                    AppendParallelStacks(world, in context, command, in definition, incoming);
                    break;
            }
        }

        private void AppendParallelStacks(World world, in SimulationContext context, AddBuffCommand command, in BuffDefinition definition, int count)
        {
            if (count <= 0)
                return;

            BuffRuntimeKey key = new BuffRuntimeKey(command.Target, command.Source, command.ConfigId);
            int currentStack = CountStacks(world, key, BuffInstanceType.parallel);
            int addCount = count;

            if (!definition.Unlimited)
            {
                int remain = definition.MaxStack - currentStack;

                if (remain <= 0)
                    return;

                addCount = Math.Min(addCount, remain);
            }

            for (int i = 0; i < addCount; i++)
                CreateRuntimeBuffEntity(world, in context, command, in definition, 1, true);
        }

        private int RefreshParallelStacks(World world, in SimulationContext context, BuffRuntimeKey key, BuffDefinition definition, int count, bool refreshAll)
        {
            CollectRuntimeEntities(world, key, _scratchEntities);
            SortRuntimeEntitiesForRemoval(world, definition.ParallelStackDownPolicy);

            int refreshed = 0;
            int refreshLimit = refreshAll ? int.MaxValue : count;

            for (int i = 0; i < _scratchEntities.Count && refreshed < refreshLimit; i++)
            {
                Entity runtimeEntity = _scratchEntities[i];

                if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                runtime.remainingFrames = definition.IsForever ? 0 : definition.DurationFrames;
                runtime.durationFrames = definition.DurationFrames;
                runtime.tickIntervalFrames = definition.TickIntervalFrames;
                runtime.elapsedFrames = 0;
                runtime.ticks = 0;
                WriteRuntimeComponent(world, runtimeEntity, runtime);
                QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.Refresh, 0);
                refreshed++;
            }

            return refreshed;
        }

        private void ReplaceOrAppendParallelStacks(World world, in SimulationContext context, AddBuffCommand command, in BuffDefinition definition, int count)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                BuffRuntimeKey key = new BuffRuntimeKey(command.Target, command.Source, command.ConfigId);
                int currentStack = CountStacks(world, key, BuffInstanceType.parallel);

                if (definition.Unlimited || currentStack < definition.MaxStack)
                {
                    CreateRuntimeBuffEntity(world, in context, command, in definition, 1, true);
                    continue;
                }

                RemoveParallelStacks(world, in context, key, definition, 1);
                CreateRuntimeBuffEntity(world, in context, command, in definition, 1, true);
            }
        }

        private void ApplyRemoveCommand(World world, in SimulationContext context, RemoveBuffCommand command)
        {
            if (!command.IsValid)
                return;

            CollectRuntimeEntities(world, command, _scratchEntities);
            SortRuntimeEntitiesForRemoval(world, GetRemovePolicyForCommand(world, command));

            int remainRemoveCount = command.ClearAllStacks ? int.MaxValue : command.StackCount;

            for (int i = 0; i < _scratchEntities.Count && remainRemoveCount > 0; i++)
            {
                Entity runtimeEntity = _scratchEntities[i];

                if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                if (!_definitionProvider.TryGetDefinition(runtime.configId, out BuffDefinition definition))
                    continue;

                if (runtime.stack <= 0)
                    continue;

                if (runtime.buffType == BuffInstanceType.parallel)
                {
                    QueueRemoveRuntimeEntity(world, in context, runtimeEntity, in runtime, in definition, true);
                    remainRemoveCount--;
                    continue;
                }

                if (command.ClearAllStacks || remainRemoveCount >= runtime.stack)
                {
                    int removed = runtime.stack;
                    QueueRemoveRuntimeEntity(world, in context, runtimeEntity, in runtime, in definition, true);
                    remainRemoveCount -= removed;
                    continue;
                }

                runtime.stack -= remainRemoveCount;
                runtime.remainingFrames = runtime.isForever ? 0 : runtime.durationFrames;
                WriteRuntimeComponent(world, runtimeEntity, runtime);
                QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.StackChanged, -remainRemoveCount);
                remainRemoveCount = 0;
            }
        }

        private void TickRuntimeBuffs(World world, in SimulationContext context)
        {
            for (int i = 0; i < _runtimeEntitiesThisFrame.Count; i++)
            {
                Entity runtimeEntity = _runtimeEntitiesThisFrame[i];

                if (IsPendingRemoveRuntime(runtimeEntity))
                    continue;

                if (!world.TryGetComponent(runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                if (!_definitionProvider.TryGetDefinition(runtime.configId, out BuffDefinition definition))
                    continue;

                if (runtime.stack <= 0 || !world.IsAlive(runtime.target))
                {
                    QueueRemoveRuntimeEntity(world, in context, runtimeEntity, in runtime, in definition, runtime.stack > 0);
                    continue;
                }

                runtime.elapsedFrames++;

                if (runtime.tickIntervalFrames > 0 && runtime.elapsedFrames % runtime.tickIntervalFrames == 0)
                {
                    runtime.ticks++;
                    WriteRuntimeComponent(world, runtimeEntity, runtime);
                    QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.Tick, 0);
                }

                if (runtime.isForever)
                {
                    WriteRuntimeComponent(world, runtimeEntity, runtime);
                    continue;
                }

                runtime.remainingFrames--;

                if (runtime.remainingFrames > 0)
                {
                    WriteRuntimeComponent(world, runtimeEntity, runtime);
                    continue;
                }

                if (runtime.buffType == BuffInstanceType.parallel || runtime.stack <= 1)
                {
                    QueueRemoveRuntimeEntity(world, in context, runtimeEntity, in runtime, in definition, true);
                    continue;
                }

                runtime.stack--;
                runtime.remainingFrames = Math.Max(1, runtime.durationFrames);
                WriteRuntimeComponent(world, runtimeEntity, runtime);
                QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.StackChanged, -1);
            }
        }

        private void CreateRuntimeBuffEntity(World world, in SimulationContext context, AddBuffCommand command, in BuffDefinition definition, int stack, bool runEffects)
        {
            if (stack <= 0)
                return;

            Entity runtimeEntity = world.CreateEntity();

            if (!runtimeEntity.IsValid)
                return;

            BuffRuntimeComponent runtime = new BuffRuntimeComponent(command, in definition, runtimeEntity.ID, stack);
            BuffRuntimeKey key = new BuffRuntimeKey(runtime.target, runtime.source, runtime.configId);

            _pendingRuntimeComponents[runtimeEntity] = runtime;
            _createdRuntimeEntitiesThisFrame.Add(runtimeEntity);
            _createdRuntimeComponentsThisFrame[runtimeEntity] = runtime;
            AddRuntimeEntityToLookup(key, runtimeEntity);
            world.SetComponent(runtimeEntity, runtime);
            MarkViewCacheDirty();
            MarkEventRuntimeIndexDirty();

            if (!runEffects)
                return;

            QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.Apply, 0);
            QueueLifecycleEffect(in context, runtimeEntity, in runtime, in definition, BuffEffectPhase.StackChanged, stack);
        }

        private void RemoveParallelStacks(World world, in SimulationContext context, BuffRuntimeKey key, BuffDefinition definition, int count)
        {
            CollectRuntimeEntities(world, key, _scratchEntities);
            SortRuntimeEntitiesForRemoval(world, definition.ParallelStackDownPolicy);

            int removed = 0;
            for (int i = 0; i < _scratchEntities.Count && removed < count; i++)
            {
                Entity runtimeEntity = _scratchEntities[i];

                if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                QueueRemoveRuntimeEntity(world, in context, runtimeEntity, in runtime, in definition, true);
                removed++;
            }
        }

        private void QueueRemoveRuntimeEntity(World world, in SimulationContext context, Entity runtimeEntity, in BuffRuntimeComponent runtime, in BuffDefinition definition, bool emitStackChanged)
        {
            if (_pendingRemoveRuntimeSet.Contains(runtimeEntity))
                return;

            // Remove effects must observe the pre-removal runtime snapshot.
            BuffRuntimeComponent runtimeBeforeRemove = runtime;
            int removedStack = runtimeBeforeRemove.stack;

            if (emitStackChanged && removedStack != 0)
                QueueLifecycleEffect(in context, runtimeEntity, in runtimeBeforeRemove, in definition, BuffEffectPhase.StackChanged, -removedStack);

            QueueLifecycleEffect(in context, runtimeEntity, in runtimeBeforeRemove, in definition, BuffEffectPhase.Remove, 0);
            _pendingRemoveRuntimeSet.Add(runtimeEntity);
            _pendingRemoveRuntimes.Add(new PendingRemoveRuntime(runtimeEntity, runtimeBeforeRemove.runtimeHandle));

            RemoveRuntimeEntityFromLookup(runtimeBeforeRemove, runtimeEntity);

            BuffRuntimeComponent removedRuntime = runtimeBeforeRemove;
            removedRuntime.stack = 0;
            WriteRuntimeComponent(world, runtimeEntity, removedRuntime);
            _pendingRuntimeComponents.Remove(runtimeEntity);
            _createdRuntimeComponentsThisFrame.Remove(runtimeEntity);
            MarkEventRuntimeIndexDirty();
        }

        private void RemoveRuntimeEntityFromLookup(in BuffRuntimeComponent runtime, Entity runtimeEntity)
        {
            BuffRuntimeKey key = new BuffRuntimeKey(runtime.target, runtime.source, runtime.configId);

            if (_runtimeEntitiesByKey.TryGetValue(key, out List<Entity> entities))
                entities.Remove(runtimeEntity);
        }

        private void ApplyNormalStackPolicy(ref BuffRuntimeComponent runtime, in BuffDefinition definition, int incomingStack)
        {
            switch (definition.NormalStackPolicy)
            {
                case NormalBuffStackPolicy.AddDuration:
                    runtime.durationFrames += Math.Max(0, definition.DurationExtendFramesPerStack * incomingStack);
                    runtime.remainingFrames += Math.Max(0, definition.DurationExtendFramesPerStack * incomingStack);
                    break;

                case NormalBuffStackPolicy.AddStackOnly:
                    runtime.stack = ClampStack(runtime.stack + incomingStack, definition.Unlimited, definition.MaxStack);
                    break;

                case NormalBuffStackPolicy.AddStackAndRefreshDuration:
                    runtime.stack = ClampStack(runtime.stack + incomingStack, definition.Unlimited, definition.MaxStack);
                    ResetRuntimeDuration(ref runtime, in definition);
                    break;

                case NormalBuffStackPolicy.ResetDurationOnly:
                    ResetRuntimeDuration(ref runtime, in definition);
                    break;

                case NormalBuffStackPolicy.CyclicStack:
                    if (definition.Unlimited)
                    {
                        runtime.stack += incomingStack;
                    }
                    else
                    {
                        int total = runtime.stack + incomingStack;
                        runtime.stack = ((total - 1) % definition.MaxStack) + 1;
                    }
                    break;

                case NormalBuffStackPolicy.RefreshDuration:
                default:
                    runtime.stack = ClampStack(runtime.stack + incomingStack, definition.Unlimited, definition.MaxStack);
                    ResetRuntimeDuration(ref runtime, in definition);
                    break;
            }
        }

        private static void ResetRuntimeDuration(ref BuffRuntimeComponent runtime, in BuffDefinition definition)
        {
            runtime.durationFrames = definition.DurationFrames;
            runtime.remainingFrames = definition.IsForever ? 0 : definition.DurationFrames;
            runtime.elapsedFrames = 0;
            runtime.ticks = 0;
        }

        private void RebuildRuntimeLookup(World world)
        {
            // lookup 的 List 会复用；空 key 会延迟回收，避免 Buff 短时间反复出现时持续分配。
            ClearRuntimeLookupLists();

            for (int i = 0; i < _runtimeEntitiesThisFrame.Count; i++)
            {
                Entity runtimeEntity = _runtimeEntitiesThisFrame[i];

                if (IsPendingRemoveRuntime(runtimeEntity))
                    continue;

                if (!world.TryGetComponent(runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                if (runtime.stack <= 0 || !world.IsAlive(runtime.target))
                    continue;

                AddRuntimeEntityToLookup(new BuffRuntimeKey(runtime.target, runtime.source, runtime.configId), runtimeEntity);
            }

            RemoveEmptyRuntimeLookupLists();
        }

        private void EnsureViewCache()
        {
            // ViewCache 延迟到外部读取时构建；RemainingFrames 变化时由 WriteRuntimeComponent 标记 dirty。
            if (!_viewCacheDirty)
                return;

            _viewByKey.Clear();
            _validTargetViewCache.Clear();

            if (_queryWorld == null)
            {
                _viewCacheDirty = false;
                return;
            }

            for (int i = 0; i < _runtimeEntitiesThisFrame.Count; i++)
            {
                Entity runtimeEntity = _runtimeEntitiesThisFrame[i];

                if (TryGetRuntimeComponent(_queryWorld, runtimeEntity, out BuffRuntimeComponent runtime))
                    AddRuntimeToViewCache(_queryWorld, in runtime);
            }

            for (int i = 0; i < _createdRuntimeEntitiesThisFrame.Count; i++)
            {
                Entity runtimeEntity = _createdRuntimeEntitiesThisFrame[i];

                if (TryGetRuntimeComponent(_queryWorld, runtimeEntity, out BuffRuntimeComponent runtime))
                    AddRuntimeToViewCache(_queryWorld, in runtime);
            }

            _viewCacheDirty = false;
        }

        private void AddRuntimeToViewCache(World world, in BuffRuntimeComponent runtime)
        {
            if (runtime.stack <= 0 || !world.IsAlive(runtime.target))
                return;

            BuffRuntimeKey key = new BuffRuntimeKey(runtime.target, runtime.source, runtime.configId);
            BuffViewData view = ToViewData(runtime);

            if (_viewByKey.TryGetValue(key, out BuffViewData existed))
                _viewByKey[key] = MergeViewData(existed, view);
            else
                _viewByKey.Add(key, view);
        }

        private List<BuffViewData> GetOrCreateTargetViewList(Entity target)
        {
            if (!_viewsByTarget.TryGetValue(target, out List<BuffViewData> buffs))
            {
                buffs = new List<BuffViewData>();
                _viewsByTarget.Add(target, buffs);
            }

            return buffs;
        }

        private void ClearRuntimeLookupLists()
        {
            foreach (KeyValuePair<BuffRuntimeKey, List<Entity>> pair in _runtimeEntitiesByKey)
                pair.Value.Clear();
        }

        private void RemoveEmptyRuntimeLookupLists()
        {
            _removeLookupKeys.Clear();

            foreach (KeyValuePair<BuffRuntimeKey, List<Entity>> pair in _runtimeEntitiesByKey)
            {
                if (pair.Value.Count > 0)
                {
                    _runtimeLookupUnusedFrames.Remove(pair.Key);
                    continue;
                }

                _runtimeLookupUnusedFrames.TryGetValue(pair.Key, out int unusedFrames);
                unusedFrames++;

                if (unusedFrames >= RuntimeLookupRetainFrames)
                    _removeLookupKeys.Add(pair.Key);
                else
                    _runtimeLookupUnusedFrames[pair.Key] = unusedFrames;
            }

            for (int i = 0; i < _removeLookupKeys.Count; i++)
            {
                BuffRuntimeKey key = _removeLookupKeys[i];
                _runtimeEntitiesByKey.Remove(key);
                _runtimeLookupUnusedFrames.Remove(key);
            }

            _removeLookupKeys.Clear();
        }

        private void SortRuntimeEntitiesForRemoval(World world, ParallelBuffStackDownPolicy policy)
        {
            // 移除排序不能使用闭包，避免热路径分配；比较器只在排序期间持有上下文。
            _runtimeRemovalComparer.Reset(this, world, policy);
            _scratchEntities.Sort(_runtimeRemovalComparer);
        }

        private void MarkViewCacheDirty()
        {
            // 任意 Runtime 改动都会影响 RemainingFrames、Stack 或存在性，下一次读取必须重建视图。
            _viewCacheDirty = true;
            _validTargetViewCache.Clear();
        }

        private void MarkEventRuntimeIndexDirty()
        {
            // 事件索引只描述当前帧可响应事件的 Runtime，结构变化后必须等待下一次 Raise 懒重建。
            _eventRuntimeIndexDirty = true;
        }

        private bool IsPendingRemoveRuntime(Entity runtimeEntity)
        {
            return _pendingRemoveRuntimeSet.Contains(runtimeEntity);
        }

        private bool TryGetFirstRuntimeEntity(World world, BuffRuntimeKey key, BuffInstanceType buffType, out Entity runtimeEntity)
        {
            runtimeEntity = Entity.Invalid;

            if (!_runtimeEntitiesByKey.TryGetValue(key, out List<Entity> entities))
                return false;

            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];

                if (IsPendingRemoveRuntime(entity))
                    continue;

                if (!TryGetRuntimeComponent(world, entity, out BuffRuntimeComponent runtime))
                    continue;

                if (runtime.stack <= 0 || runtime.buffType != buffType)
                    continue;

                runtimeEntity = entity;
                return true;
            }

            return false;
        }

        private int CountStacks(World world, BuffRuntimeKey key, BuffInstanceType buffType)
        {
            if (!_runtimeEntitiesByKey.TryGetValue(key, out List<Entity> entities))
                return 0;

            int stack = 0;

            for (int i = 0; i < entities.Count; i++)
            {
                Entity runtimeEntity = entities[i];

                if (IsPendingRemoveRuntime(runtimeEntity))
                    continue;

                if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                if (runtime.stack <= 0 || runtime.buffType != buffType)
                    continue;

                stack += runtime.stack;
            }

            return stack;
        }

        private void CollectRuntimeEntities(World world, BuffRuntimeKey key, List<Entity> results)
        {
            results.Clear();

            if (_runtimeEntitiesByKey.TryGetValue(key, out List<Entity> entities))
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    Entity runtimeEntity = entities[i];

                    if (!IsPendingRemoveRuntime(runtimeEntity))
                        results.Add(runtimeEntity);
                }
            }
        }

        private void CollectRuntimeEntities(World world, RemoveBuffCommand command, List<Entity> results)
        {
            // MatchAnySource 使用本帧 Runtime 快照，避免移除请求在热路径再次全量查询。
            results.Clear();

            if (!command.MatchAnySource)
            {
                BuffRuntimeKey key = new BuffRuntimeKey(command.Target, command.Source, command.ConfigId);

                if (_runtimeEntitiesByKey.TryGetValue(key, out List<Entity> entities))
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        Entity runtimeEntity = entities[i];

                        if (!IsPendingRemoveRuntime(runtimeEntity))
                            results.Add(runtimeEntity);
                    }
                }

                return;
            }

            for (int i = 0; i < _runtimeEntitiesThisFrame.Count; i++)
            {
                Entity runtimeEntity = _runtimeEntitiesThisFrame[i];

                if (IsPendingRemoveRuntime(runtimeEntity))
                    continue;

                if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                    continue;

                if (runtime.target == command.Target && runtime.configId == command.ConfigId)
                    results.Add(runtimeEntity);
            }

            foreach (KeyValuePair<Entity, BuffRuntimeComponent> pair in _pendingRuntimeComponents)
            {
                if (IsPendingRemoveRuntime(pair.Key))
                    continue;

                BuffRuntimeComponent runtime = pair.Value;

                if (runtime.target == command.Target && runtime.configId == command.ConfigId)
                    results.Add(pair.Key);
            }
        }

        private ParallelBuffStackDownPolicy GetRemovePolicyForCommand(World world, RemoveBuffCommand command)
        {
            if (!_definitionProvider.TryGetDefinition(command.ConfigId, out BuffDefinition definition))
                return ParallelBuffStackDownPolicy.RemoveEarliest;

            if (command.ClearAllStacks)
                return ParallelBuffStackDownPolicy.ClearAll;

            return definition.ParallelStackDownPolicy;
        }

        private void CollectEventCandidates<TEvent>(World world, in SimulationContext context, in TEvent gameEvent)
            where TEvent : struct, IGameEvent
        {
            // 事件候选先过滤再排序，确保响应顺序不依赖 Query 或 Dictionary 的遍历顺序。
            _eventCandidates.Clear();
            _eventCandidateEntitySet.Clear();
            EnsureEventRuntimeIndex(world, context.frameNumber);

            if (!_eventRuntimeEntitiesByEventId.TryGetValue(gameEvent.EventId, out List<Entity> runtimeEntities))
                return;

            for (int i = 0; i < runtimeEntities.Count; i++)
                TryAddEventCandidate(world, in context, runtimeEntities[i], in gameEvent);
        }

        private void EnsureEventRuntimeIndex(World world, int frameNumber)
        {
            if (!_eventRuntimeIndexDirty && _eventRuntimeIndexFrameNumber == frameNumber)
                return;

            ClearEventRuntimeIndexLists();

            if (_runtimeSnapshotFrameNumber == frameNumber)
            {
                for (int i = 0; i < _runtimeEntitiesThisFrame.Count; i++)
                    AddRuntimeEntityToEventIndex(world, _runtimeEntitiesThisFrame[i]);

                for (int i = 0; i < _createdRuntimeEntitiesThisFrame.Count; i++)
                    AddRuntimeEntityToEventIndex(world, _createdRuntimeEntitiesThisFrame[i]);
            }
            else
            {
                // 当前帧还没有 Runtime 快照时，只兜底查询一次，并基于结果构建本帧事件索引。
                _runtimeEntities.Clear();
                world.FillQuery(_runtimeQuery, _runtimeEntities, true);

                for (int i = 0; i < _runtimeEntities.Count; i++)
                    AddRuntimeEntityToEventIndex(world, _runtimeEntities[i]);
            }

            foreach (KeyValuePair<Entity, BuffRuntimeComponent> pair in _pendingRuntimeComponents)
                AddRuntimeEntityToEventIndex(world, pair.Key);

            _eventRuntimeIndexFrameNumber = frameNumber;
            _eventRuntimeIndexDirty = false;
        }

        private void AddRuntimeEntityToEventIndex(World world, Entity runtimeEntity)
        {
            if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                return;

            if (runtime.stack <= 0 || !world.IsAlive(runtime.target))
                return;

            if (!_definitionProvider.TryGetDefinition(runtime.configId, out BuffDefinition definition))
                return;

            if (definition.TriggerType != BuffTriggerType.EventTrigger || definition.EventIds == null || definition.EventIds.Length == 0)
                return;

            for (int i = 0; i < definition.EventIds.Length; i++)
            {
                int eventId = definition.EventIds[i];

                if (eventId <= 0)
                    continue;

                GetOrCreateEventRuntimeList(eventId).Add(runtimeEntity);
            }
        }

        private List<Entity> GetOrCreateEventRuntimeList(int eventId)
        {
            if (!_eventRuntimeEntitiesByEventId.TryGetValue(eventId, out List<Entity> entities))
            {
                entities = new List<Entity>();
                _eventRuntimeEntitiesByEventId.Add(eventId, entities);
            }

            return entities;
        }

        private void ClearEventRuntimeIndexLists()
        {
            foreach (KeyValuePair<int, List<Entity>> pair in _eventRuntimeEntitiesByEventId)
                pair.Value.Clear();
        }

        private void TryAddEventCandidate<TEvent>(World world, in SimulationContext context, Entity runtimeEntity, in TEvent gameEvent)
            where TEvent : struct, IGameEvent
        {
            if (!_eventCandidateEntitySet.Add(runtimeEntity))
                return;

            if (!TryGetRuntimeComponent(world, runtimeEntity, out BuffRuntimeComponent runtime))
                return;

            if (runtime.stack <= 0 || !world.IsAlive(runtime.target))
                return;

            if (!_definitionProvider.TryGetDefinition(runtime.configId, out BuffDefinition definition))
                return;

            if (!definition.CanRespondToEvent(gameEvent.EventId))
                return;

            if (!_effectRegistry.TryGetEventEffect(definition.EffectId, out IBuffEventEffectExecutor<TEvent> effect))
                return;

            BuffEffectContext effectContext = new BuffEffectContext(world, in context, runtimeEntity, in runtime, in definition);

            if (!effect.ShouldTrigger(in effectContext, in gameEvent))
                return;

            _eventCandidates.Add(new BuffEventCandidate(runtimeEntity, in runtime, in definition));
        }

        private void RunEventEffect<TEvent>(World world, in SimulationContext context, in BuffEventCandidate candidate, in TEvent gameEvent)
            where TEvent : struct, IGameEvent
        {
            if (!_effectRegistry.TryGetEventEffect(candidate.definition.EffectId, out IBuffEventEffectExecutor<TEvent> effect))
                return;

            if (!TryGetRuntimeComponent(world, candidate.runtimeEntity, out BuffRuntimeComponent runtime))
                return;

            if (runtime.stack <= 0 || !world.IsAlive(runtime.target))
                return;

            // 新版 Event 阶段保留旧版 EffectPhase.Event 的语义边界；事件是泛型 struct，为避免装箱，不复用非泛型 RunEffect 管线。
            BuffEffectContext effectContext = new BuffEffectContext(world, in context, candidate.runtimeEntity, in runtime, in candidate.definition);
            effect.OnEvent(in effectContext, in gameEvent);
        }

        private void QueueLifecycleEffect(in SimulationContext context, Entity runtimeEntity, in BuffRuntimeComponent runtime, in BuffDefinition definition, BuffEffectPhase phase, int stackDelta)
        {
            if (definition.EffectId == 0)
                return;

            _pendingLifecycleEffects.Add(new BuffEffectRequest(
                context.frameNumber,
                _nextLifecycleEffectSequence++,
                phase,
                runtimeEntity,
                in runtime,
                in definition,
                stackDelta));
        }

        private void FlushLifecycleEffects(World world, in SimulationContext context)
        {
            if (_pendingLifecycleEffects.Count == 0)
                return;

            _pendingLifecycleEffects.Sort(_effectRequestComparer);

            _executingLifecycleEffects.Clear();
            _executingLifecycleEffects.AddRange(_pendingLifecycleEffects);
            _pendingLifecycleEffects.Clear();

            for (int i = 0; i < _executingLifecycleEffects.Count; i++)
                ExecuteLifecycleEffectRequest(world, in context, _executingLifecycleEffects[i]);

            _executingLifecycleEffects.Clear();
        }

        private void ExecuteLifecycleEffectRequest(World world, in SimulationContext context, in BuffEffectRequest request)
        {
            if (!_definitionProvider.TryGetDefinition(request.configId, out BuffDefinition definition))
                return;

            if (!_effectRegistry.TryGet(request.effectId, out IBuffEffectExecutor effect))
                return;

            BuffEffectContext effectContext = new BuffEffectContext(world, in context, request.runtimeEntity, in request.runtimeSnapshot, in definition);

            switch (request.phase)
            {
                case BuffEffectPhase.Apply:
                    effect.OnApply(in effectContext);
                    break;
                case BuffEffectPhase.Refresh:
                    effect.OnRefresh(in effectContext);
                    break;
                case BuffEffectPhase.StackChanged:
                    effect.OnStackChanged(in effectContext, request.stackDelta);
                    break;
                case BuffEffectPhase.Tick:
                    effect.OnTick(in effectContext);
                    break;
                case BuffEffectPhase.Remove:
                    effect.OnRemove(in effectContext);
                    break;
            }
        }

        private void DestroyPendingRemoveRuntimes(World world)
        {
            if (_pendingRemoveRuntimes.Count == 0)
                return;

            _pendingRemoveRuntimes.Sort(_pendingRemoveRuntimeComparer);

            Entity lastEntity = Entity.Invalid;
            for (int i = 0; i < _pendingRemoveRuntimes.Count; i++)
            {
                Entity runtimeEntity = _pendingRemoveRuntimes[i].runtimeEntity;

                if (runtimeEntity == lastEntity)
                    continue;

                lastEntity = runtimeEntity;

                if (world.IsAlive(runtimeEntity))
                    world.DestroyEntity(runtimeEntity);
            }

            _pendingRemoveRuntimes.Clear();
            _pendingRemoveRuntimeSet.Clear();
        }

        private static int GetLifecyclePhaseOrder(BuffEffectPhase phase)
        {
            switch (phase)
            {
                case BuffEffectPhase.Apply:
                    return 0;
                case BuffEffectPhase.Refresh:
                    return 1;
                case BuffEffectPhase.StackChanged:
                    return 2;
                case BuffEffectPhase.Tick:
                    return 3;
                case BuffEffectPhase.Remove:
                    return 4;
                default:
                    return int.MaxValue;
            }
        }

        private static int ClampStack(int stack, bool unlimited, int maxStack)
        {
            if (unlimited)
                return stack;

            int safeMaxStack = maxStack > 0 ? maxStack : 1;
            return Math.Min(stack, safeMaxStack);
        }

        private void AddRuntimeEntityToLookup(BuffRuntimeKey key, Entity runtimeEntity)
        {
            if (IsPendingRemoveRuntime(runtimeEntity))
                return;

            if (!_runtimeEntitiesByKey.TryGetValue(key, out List<Entity> entities))
            {
                entities = new List<Entity>();
                _runtimeEntitiesByKey.Add(key, entities);
            }

            entities.Add(runtimeEntity);
        }

        private bool TryGetRuntimeComponent(World world, Entity runtimeEntity, out BuffRuntimeComponent runtime)
        {
            if (IsPendingRemoveRuntime(runtimeEntity))
            {
                runtime = default;
                return false;
            }

            if (_pendingRuntimeComponents.TryGetValue(runtimeEntity, out runtime))
                return true;

            if (_createdRuntimeComponentsThisFrame.TryGetValue(runtimeEntity, out runtime))
                return true;

            return world.TryGetComponent(runtimeEntity, out runtime);
        }

        private void WriteRuntimeComponent(World world, Entity runtimeEntity, BuffRuntimeComponent runtime, bool markViewDirty = true)
        {
            // 同步更新本帧新建 Runtime 快照，保证结构变更播放前 ViewCache 也能读到最新值。
            if (_pendingRuntimeComponents.TryGetValue(runtimeEntity, out _))
                _pendingRuntimeComponents[runtimeEntity] = runtime;

            if (_createdRuntimeComponentsThisFrame.TryGetValue(runtimeEntity, out _))
                _createdRuntimeComponentsThisFrame[runtimeEntity] = runtime;

            world.SetComponent(runtimeEntity, runtime);

            if (markViewDirty)
                MarkViewCacheDirty();
        }

        private static BuffViewData ToViewData(BuffRuntimeComponent runtime)
        {
            int remainingFrames = runtime.isForever ? -1 : Math.Max(0, runtime.remainingFrames);
            return new BuffViewData(runtime.target, runtime.source, runtime.configId, runtime.stack, remainingFrames, runtime.runtimeHandle);
        }

        private static BuffViewData MergeViewData(BuffViewData left, BuffViewData right)
        {
            int remainingFrames;

            if (left.RemainingFrames < 0 || right.RemainingFrames < 0)
                remainingFrames = -1;
            else
                remainingFrames = Math.Min(left.RemainingFrames, right.RemainingFrames);

            int runtimeHandle = Math.Min(left.RuntimeHandle, right.RuntimeHandle);
            return new BuffViewData(left.Target, left.Source, left.ConfigId, left.Stack + right.Stack, remainingFrames, runtimeHandle);
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            int idCompare = left.ID.CompareTo(right.ID);

            if (idCompare != 0)
                return idCompare;

            return left.Version.CompareTo(right.Version);
        }

        private int CompareRuntimeForRemoval(World world, Entity left, Entity right, ParallelBuffStackDownPolicy policy)
        {
            bool hasLeft = TryGetRuntimeComponent(world, left, out BuffRuntimeComponent leftRuntime);
            bool hasRight = TryGetRuntimeComponent(world, right, out BuffRuntimeComponent rightRuntime);

            if (!hasLeft && !hasRight)
                return CompareEntity(left, right);

            if (!hasLeft)
                return 1;

            if (!hasRight)
                return -1;

            if (policy == ParallelBuffStackDownPolicy.ClearAll)
                return CompareEntity(left, right);

            int remainingCompare = leftRuntime.remainingFrames.CompareTo(rightRuntime.remainingFrames);

            if (policy == ParallelBuffStackDownPolicy.RemoveLatest)
                remainingCompare = -remainingCompare;

            if (remainingCompare != 0)
                return remainingCompare;

            int handleCompare = leftRuntime.runtimeHandle.CompareTo(rightRuntime.runtimeHandle);

            if (handleCompare != 0)
                return handleCompare;

            return CompareEntity(left, right);
        }

        private enum BuffEffectPhase
        {
            Apply,
            Refresh,
            StackChanged,
            Tick,
            Event,
            Remove
        }
    }
}
