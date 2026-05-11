/*
 * 文件说明：SimulationFrameCommands 定义了创建 / 销毁 Entity、增删 Component、增删 System 等可按帧回放的外部指令。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 帧指令：在指定逻辑帧创建 Entity，并可附加一组初始组件。
/// </summary>
public sealed class CreateEntityFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand
{
    private interface IEntityInitializer
    {
        void Apply(World world, Entity entity);
    }

    private sealed class ComponentInitializer<T> : IEntityInitializer where T : struct, IComponentData
    {
        private readonly T _component;

        public ComponentInitializer(in T component)
        {
            _component = component;
        }

        public void Apply(World world, Entity entity)
        {
            world.SetComponent(entity, in _component);
        }
    }

    private readonly List<IEntityInitializer> _initializers = new List<IEntityInitializer>();

    public int FrameNumber { get; }
    public Entity LastCreatedEntity { get; private set; }

    /// <summary>创建指定逻辑帧执行的 Entity 创建指令。</summary>
    public CreateEntityFrameCommand(int frameNumber)
    {
        FrameNumber = frameNumber;
        LastCreatedEntity = Entity.Invalid;
    }

    /// <summary>使用新的目标帧号重建该指令；CreateEntityFrameCommand 因为包含初始化器集合，重建时会复用初始化器。</summary>
    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        CreateEntityFrameCommand command = new CreateEntityFrameCommand(frameNumber);

        for (int i = 0; i < _initializers.Count; i++)
            command._initializers.Add(_initializers[i]);

        return command;
    }

    /// <summary>为即将创建的 Entity 添加一个初始组件。</summary>
    public CreateEntityFrameCommand WithComponent<T>(in T component) where T : struct, IComponentData
    {
        _initializers.Add(new ComponentInitializer<T>(in component));
        return this;
    }

    /// <summary>执行创建 Entity，并按注册顺序写入初始组件。</summary>
    public void Execute(World world)
    {
        if (world == null)
            return;

        Entity entity = world.CreateEntity();
        LastCreatedEntity = entity;

        if (!entity.IsValid)
            return;

        for (int i = 0; i < _initializers.Count; i++)
        {
            _initializers[i].Apply(world, entity);
        }
    }
}

/// <summary>
/// 帧指令：在指定逻辑帧销毁 Entity。
/// </summary>
public readonly struct DestroyEntityFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand
{
    public int FrameNumber { get; }
    public Entity Entity { get; }

    public DestroyEntityFrameCommand(int frameNumber, Entity entity)
    {
        FrameNumber = frameNumber;
        Entity = entity;
    }

    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        return new DestroyEntityFrameCommand(frameNumber, Entity);
    }

    public void Execute(World world)
    {
        if (world != null)
            world.DestroyEntity(Entity);
    }
}

/// <summary>
/// 帧指令：在指定逻辑帧设置或添加组件。
/// </summary>
public readonly struct SetComponentFrameCommand<T> : ISimulationFrameCommand, IRebuildableSimulationFrameCommand where T : struct, IComponentData
{
    private readonly T _component;

    public int FrameNumber { get; }
    public Entity Entity { get; }

    public SetComponentFrameCommand(int frameNumber, Entity entity, in T component)
    {
        FrameNumber = frameNumber;
        Entity = entity;
        _component = component;
    }

    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        return new SetComponentFrameCommand<T>(frameNumber, Entity, in _component);
    }

    public void Execute(World world)
    {
        if (world != null)
            world.SetComponent(Entity, in _component);
    }
}

/// <summary>
/// 帧指令：在指定逻辑帧移除组件。
/// </summary>
public readonly struct RemoveComponentFrameCommand<T> : ISimulationFrameCommand, IRebuildableSimulationFrameCommand where T : struct, IComponentData
{
    public int FrameNumber { get; }
    public Entity Entity { get; }

    public RemoveComponentFrameCommand(int frameNumber, Entity entity)
    {
        FrameNumber = frameNumber;
        Entity = entity;
    }

    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        return new RemoveComponentFrameCommand<T>(frameNumber, Entity);
    }

    public void Execute(World world)
    {
        if (world != null)
            world.RemoveComponent<T>(Entity);
    }
}

/// <summary>
/// 帧指令：在指定逻辑帧添加 System；正式帧同步模式中更推荐初始化阶段固定 System 列表。
/// </summary>
public readonly struct AddSystemFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand
{
    public int FrameNumber { get; }
    public IFixedStepSystem System { get; }

    public AddSystemFrameCommand(int frameNumber, IFixedStepSystem system)
    {
        FrameNumber = frameNumber;
        System = system;
    }

    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        return new AddSystemFrameCommand(frameNumber, System);
    }

    public void Execute(World world)
    {
        if (world != null)
            world.AddSystem(System);
    }
}

/// <summary>
/// 帧指令：在指定逻辑帧移除 System；正式帧同步模式中更推荐用组件或状态控制 System 行为。
/// </summary>
public readonly struct RemoveSystemFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand
{
    public int FrameNumber { get; }
    public IFixedStepSystem System { get; }

    public RemoveSystemFrameCommand(int frameNumber, IFixedStepSystem system)
    {
        FrameNumber = frameNumber;
        System = system;
    }

    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        return new RemoveSystemFrameCommand(frameNumber, System);
    }

    public void Execute(World world)
    {
        if (world != null)
            world.RemoveSystem(System);
    }
}

/// <summary>
/// 帧指令：在指定逻辑帧清空 System 列表；主要用于测试或非正式模拟流程。
/// </summary>
public readonly struct ClearSystemFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand
{
    public int FrameNumber { get; }

    public ClearSystemFrameCommand(int frameNumber)
    {
        FrameNumber = frameNumber;
    }

    public ISimulationFrameCommand Rebuild(int frameNumber)
    {
        return new ClearSystemFrameCommand(frameNumber);
    }

    public void Execute(World world)
    {
        if (world != null)
            world.ClearSystem();
    }
}

}
