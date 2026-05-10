/*
 * 文件说明：按逻辑帧记录和消费外部模拟指令。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// SimulationFrameCommandBuffer 的便捷写入 API，统一把 Tick 外部的 Entity、Component、System 修改转成按帧指令。
/// </summary>
public static class SimulationFrameCommandBufferExtensions
{
    /// <summary>计划在指定逻辑帧开始前创建一个 Entity，并返回可继续附加初始组件的创建指令。</summary>
    public static CreateEntityFrameCommand CreateEntityAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber)
    {
        return buffer.CreateEntityAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick);
    }

    /// <summary>计划在指定逻辑帧、指定时机创建一个 Entity，并返回可继续附加初始组件的创建指令。</summary>
    public static CreateEntityFrameCommand CreateEntityAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing)
    {
        if (buffer == null)
            return null;

        CreateEntityFrameCommand command = new CreateEntityFrameCommand(frameNumber);
        buffer.AddCommand(command, timing);
        return command;
    }

    /// <summary>计划在指定逻辑帧开始前销毁 Entity。</summary>
    public static void DestroyEntityAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, EntityInfo entity)
    {
        buffer.DestroyEntityAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick, entity);
    }

    /// <summary>计划在指定逻辑帧、指定时机销毁 Entity。</summary>
    public static void DestroyEntityAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, EntityInfo entity)
    {
        if (buffer != null)
            buffer.AddCommand(new DestroyEntityFrameCommand(frameNumber, entity), timing);
    }

    /// <summary>计划在指定逻辑帧开始前设置或添加组件。</summary>
    public static void SetComponentAtFrame<T>(this SimulationFrameCommandBuffer buffer, int frameNumber, EntityInfo entity, in T component) where T : struct, IComponentData
    {
        buffer.SetComponentAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick, entity, in component);
    }

    /// <summary>计划在指定逻辑帧、指定时机设置或添加组件。</summary>
    public static void SetComponentAtFrame<T>(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, EntityInfo entity, in T component) where T : struct, IComponentData
    {
        if (buffer != null)
            buffer.AddCommand(new SetComponentFrameCommand<T>(frameNumber, entity, in component), timing);
    }

    /// <summary>计划在指定逻辑帧开始前移除组件。</summary>
    public static void RemoveComponentAtFrame<T>(this SimulationFrameCommandBuffer buffer, int frameNumber, EntityInfo entity) where T : struct, IComponentData
    {
        buffer.RemoveComponentAtFrame<T>(frameNumber, SimulationFrameCommandTiming.BeforeTick, entity);
    }

    /// <summary>计划在指定逻辑帧、指定时机移除组件。</summary>
    public static void RemoveComponentAtFrame<T>(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, EntityInfo entity) where T : struct, IComponentData
    {
        if (buffer != null)
            buffer.AddCommand(new RemoveComponentFrameCommand<T>(frameNumber, entity), timing);
    }

    /// <summary>计划在指定逻辑帧开始前添加 System；正式同步模拟中更推荐初始化阶段固定 System 列表。</summary>
    public static void AddSystemAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, IFixedStepSystem system)
    {
        buffer.AddSystemAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick, system);
    }

    /// <summary>计划在指定逻辑帧、指定时机添加 System；正式同步模拟中更推荐初始化阶段固定 System 列表。</summary>
    public static void AddSystemAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, IFixedStepSystem system)
    {
        if (buffer != null)
            buffer.AddCommand(new AddSystemFrameCommand(frameNumber, system), timing);
    }

    /// <summary>计划在指定逻辑帧开始前移除 System；正式同步模拟中更推荐用组件控制 System 行为。</summary>
    public static void RemoveSystemAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, IFixedStepSystem system)
    {
        buffer.RemoveSystemAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick, system);
    }

    /// <summary>计划在指定逻辑帧、指定时机移除 System；正式同步模拟中更推荐用组件控制 System 行为。</summary>
    public static void RemoveSystemAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, IFixedStepSystem system)
    {
        if (buffer != null)
            buffer.AddCommand(new RemoveSystemFrameCommand(frameNumber, system), timing);
    }

    /// <summary>计划在指定逻辑帧开始前清空 System 列表；主要用于测试或非正式模拟流程。</summary>
    public static void ClearSystemAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber)
    {
        buffer.ClearSystemAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick);
    }

    /// <summary>计划在指定逻辑帧、指定时机清空 System 列表；主要用于测试或非正式模拟流程。</summary>
    public static void ClearSystemAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing)
    {
        if (buffer != null)
            buffer.AddCommand(new ClearSystemFrameCommand(frameNumber), timing);
    }
}
