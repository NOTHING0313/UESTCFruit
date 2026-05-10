/*
 * 文件说明：StructuralChangeBuffer 负责缓存 World.Tick 期间产生的 Entity / Component 结构变化，并在当前逻辑帧末尾统一播放，避免遍历过程中修改底层集合。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

/// <summary>
/// StructuralChangeBuffer 类型。
/// </summary>
public sealed class StructuralChangeBuffer
{
    private interface IStructuralCommand
    {
        EntityInfo Entity { get; }
        /// <summary>
        /// 执行结构命令。
        /// </summary>
        void Execute(World world);
    }

    private readonly List<IStructuralCommand> _componentCommands = new List<IStructuralCommand>();
    private readonly List<IStructuralCommand> _nextComponentCommands = new List<IStructuralCommand>();
    private readonly List<EntityInfo> _destroyEntities = new List<EntityInfo>();
    private readonly List<EntityInfo> _nextDestroyEntities = new List<EntityInfo>();

    private bool _isPlayingBack;

    public int Count => _componentCommands.Count + _nextComponentCommands.Count + _destroyEntities.Count + _nextDestroyEntities.Count;

    /// <summary>
    /// 记录延迟设置组件命令；实体已等待销毁时忽略。
    /// </summary>
    public void SetComponent<T>(EntityInfo entity, in T component) where T : struct, IComponentData
    {
        if (!entity.IsValid || ContainsDestroyEntity(entity))
            return;

        AddComponentCommand(new SetComponentCommand<T>(entity, in component));
    }

    /// <summary>
    /// 记录延迟移除组件命令；实体已等待销毁时忽略。
    /// </summary>
    public void RemoveComponent<T>(EntityInfo entity) where T : struct, IComponentData
    {
        if (!entity.IsValid || ContainsDestroyEntity(entity))
            return;

        AddComponentCommand(new RemoveComponentCommand<T>(entity));
    }

    /// <summary>
    /// 记录延迟销毁实体命令，并移除该实体尚未播放的组件命令。
    /// </summary>
    public void DestroyEntity(EntityInfo entity)
    {
        if (!entity.IsValid || ContainsDestroyEntity(entity))
            return;

        RemoveComponentCommands(entity);
        AddDestroyEntity(entity);
    }

    /// <summary>
    /// 播放当前结构变更命令，真正执行组件增删和实体销毁。
    /// </summary>
    public void Playback(World world)
    {
        if (world == null || _isPlayingBack || (_componentCommands.Count == 0 && _destroyEntities.Count == 0))
            return;

        _isPlayingBack = true;

        try
        {
            for (int i = 0; i < _componentCommands.Count; i++)
            {
                IStructuralCommand command = _componentCommands[i];

                if (!world.IsAlive(command.Entity))
                    continue;

                command.Execute(world);
            }

            for (int i = 0; i < _destroyEntities.Count; i++)
            {
                EntityInfo entity = _destroyEntities[i];

                if (!world.IsAlive(entity))
                    continue;

                world.DestroyEntityImmediately(entity);
            }
        }
        finally
        {
            _componentCommands.Clear();
            _destroyEntities.Clear();

            if (_nextComponentCommands.Count > 0)
            {
                _componentCommands.AddRange(_nextComponentCommands);
                _nextComponentCommands.Clear();
            }

            if (_nextDestroyEntities.Count > 0)
            {
                _destroyEntities.AddRange(_nextDestroyEntities);
                _nextDestroyEntities.Clear();
            }

            _isPlayingBack = false;
        }
    }

    /// <summary>
    /// 清空所有当前和下一轮结构变更命令。
    /// </summary>
    public void Clear()
    {
        _componentCommands.Clear();
        _nextComponentCommands.Clear();
        _destroyEntities.Clear();
        _nextDestroyEntities.Clear();
    }

    /// <summary>
    /// 把组件命令加入当前队列；播放期间产生的命令进入下一轮队列。
    /// </summary>
    private void AddComponentCommand(IStructuralCommand command)
    {
        if (_isPlayingBack)
        {
            _nextComponentCommands.Add(command);
            return;
        }

        _componentCommands.Add(command);
    }

    /// <summary>
    /// 把销毁实体命令加入当前队列；播放期间产生的命令进入下一轮队列。
    /// </summary>
    private void AddDestroyEntity(EntityInfo entity)
    {
        if (_isPlayingBack)
        {
            _nextDestroyEntities.Add(entity);
            return;
        }

        _destroyEntities.Add(entity);
    }

    /// <summary>
    /// 判断实体是否已经处于等待销毁队列中。
    /// </summary>
    private bool ContainsDestroyEntity(EntityInfo entity)
    {
        return ContainsEntity(_destroyEntities, entity) || ContainsEntity(_nextDestroyEntities, entity);
    }

    /// <summary>
    /// 移除指定实体尚未播放的组件相关命令。
    /// </summary>
    private void RemoveComponentCommands(EntityInfo entity)
    {
        _componentCommands.RemoveAll(command => command.Entity == entity);
        _nextComponentCommands.RemoveAll(command => command.Entity == entity);
    }

    /// <summary>
    /// 判断实体列表中是否包含指定实体。
    /// </summary>
    private static bool ContainsEntity(List<EntityInfo> entities, EntityInfo entity)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] == entity)
                return true;
        }

        return false;
    }

    private sealed class SetComponentCommand<T> : IStructuralCommand where T : struct, IComponentData
    {
        private readonly EntityInfo _entity;
        private readonly T _component;

        public EntityInfo Entity => _entity;

        /// <summary>
        /// 创建延迟设置组件命令。
        /// </summary>
        public SetComponentCommand(EntityInfo entity, in T component)
        {
            _entity = entity;
            _component = component;
        }

        /// <summary>
        /// 执行结构命令。
        /// </summary>
        public void Execute(World world)
        {
            world.SetComponentImmediately(_entity, in _component);
        }
    }

    private sealed class RemoveComponentCommand<T> : IStructuralCommand where T : struct, IComponentData
    {
        private readonly EntityInfo _entity;

        public EntityInfo Entity => _entity;

        /// <summary>
        /// 创建延迟移除组件命令。
        /// </summary>
        public RemoveComponentCommand(EntityInfo entity)
        {
            _entity = entity;
        }

        /// <summary>
        /// 执行结构命令。
        /// </summary>
        public void Execute(World world)
        {
            world.RemoveComponentImmediately<T>(_entity);
        }
    }
}
