/*
 * 文件说明：按逻辑帧记录和消费外部模拟指令。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// SimulationFrameCommandScheduler 的常用 Entity、Component、System 调度 API。
/// </summary>
public static class SimulationFrameCommandSchedulerExtensions
{
    /// <summary>下一逻辑帧开始前设置或添加组件。</summary>
    public static void SetComponentNextFrameStart<T>(this SimulationFrameCommandScheduler scheduler, EntityInfo entity, in T component) where T : struct, IComponentData
    {
        scheduler?.AddNextFrameStart(new SetComponentFrameCommand<T>(1, entity, in component));
    }

    /// <summary>下一逻辑帧末尾设置或添加组件。</summary>
    public static void SetComponentNextFrameEnd<T>(this SimulationFrameCommandScheduler scheduler, EntityInfo entity, in T component) where T : struct, IComponentData
    {
        scheduler?.AddNextFrameEnd(new SetComponentFrameCommand<T>(1, entity, in component));
    }

    /// <summary>当前 Tick 末尾设置或添加组件；如果当前没有 Tick 正在执行，则安排到下一逻辑帧末尾。</summary>
    public static void SetComponentCurrentFrameEndOrNextFrameEnd<T>(this SimulationFrameCommandScheduler scheduler, EntityInfo entity, in T component) where T : struct, IComponentData
    {
        scheduler?.AddCurrentFrameEndOrNextFrameEnd(new SetComponentFrameCommand<T>(1, entity, in component));
    }

    /// <summary>下一逻辑帧开始前移除组件。</summary>
    public static void RemoveComponentNextFrameStart<T>(this SimulationFrameCommandScheduler scheduler, EntityInfo entity) where T : struct, IComponentData
    {
        scheduler?.AddNextFrameStart(new RemoveComponentFrameCommand<T>(1, entity));
    }

    /// <summary>下一逻辑帧末尾移除组件。</summary>
    public static void RemoveComponentNextFrameEnd<T>(this SimulationFrameCommandScheduler scheduler, EntityInfo entity) where T : struct, IComponentData
    {
        scheduler?.AddNextFrameEnd(new RemoveComponentFrameCommand<T>(1, entity));
    }

    /// <summary>当前 Tick 末尾移除组件；如果当前没有 Tick 正在执行，则安排到下一逻辑帧末尾。</summary>
    public static void RemoveComponentCurrentFrameEndOrNextFrameEnd<T>(this SimulationFrameCommandScheduler scheduler, EntityInfo entity) where T : struct, IComponentData
    {
        scheduler?.AddCurrentFrameEndOrNextFrameEnd(new RemoveComponentFrameCommand<T>(1, entity));
    }

    /// <summary>下一逻辑帧开始前销毁 Entity。</summary>
    public static void DestroyEntityNextFrameStart(this SimulationFrameCommandScheduler scheduler, EntityInfo entity)
    {
        scheduler?.AddNextFrameStart(new DestroyEntityFrameCommand(1, entity));
    }

    /// <summary>下一逻辑帧末尾销毁 Entity。</summary>
    public static void DestroyEntityNextFrameEnd(this SimulationFrameCommandScheduler scheduler, EntityInfo entity)
    {
        scheduler?.AddNextFrameEnd(new DestroyEntityFrameCommand(1, entity));
    }

    /// <summary>当前 Tick 末尾销毁 Entity；如果当前没有 Tick 正在执行，则安排到下一逻辑帧末尾。</summary>
    public static void DestroyEntityCurrentFrameEndOrNextFrameEnd(this SimulationFrameCommandScheduler scheduler, EntityInfo entity)
    {
        scheduler?.AddCurrentFrameEndOrNextFrameEnd(new DestroyEntityFrameCommand(1, entity));
    }

    /// <summary>下一逻辑帧开始前添加 System；正式同步模拟中更推荐初始化阶段固定 System 列表。</summary>
    public static void AddSystemNextFrameStart(this SimulationFrameCommandScheduler scheduler, IFixedStepSystem system)
    {
        scheduler?.AddNextFrameStart(new AddSystemFrameCommand(1, system));
    }

    /// <summary>下一逻辑帧末尾添加 System；正式同步模拟中更推荐初始化阶段固定 System 列表。</summary>
    public static void AddSystemNextFrameEnd(this SimulationFrameCommandScheduler scheduler, IFixedStepSystem system)
    {
        scheduler?.AddNextFrameEnd(new AddSystemFrameCommand(1, system));
    }

    /// <summary>下一逻辑帧开始前移除 System；正式同步模拟中更推荐用组件或状态控制 System 行为。</summary>
    public static void RemoveSystemNextFrameStart(this SimulationFrameCommandScheduler scheduler, IFixedStepSystem system)
    {
        scheduler?.AddNextFrameStart(new RemoveSystemFrameCommand(1, system));
    }

    /// <summary>下一逻辑帧末尾移除 System；正式同步模拟中更推荐用组件或状态控制 System 行为。</summary>
    public static void RemoveSystemNextFrameEnd(this SimulationFrameCommandScheduler scheduler, IFixedStepSystem system)
    {
        scheduler?.AddNextFrameEnd(new RemoveSystemFrameCommand(1, system));
    }
}
