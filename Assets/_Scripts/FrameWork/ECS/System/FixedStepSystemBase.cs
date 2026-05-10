/*
 * 文件说明：固定逻辑帧 System 接口、基类、系统变更缓冲和示例系统。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 固定帧 System 基类，封装 World 绑定和生命周期转调。
/// </summary>
public abstract class FixedStepSystemBase : IFixedStepSystem
{
    protected World World { get; private set; }

    public abstract SystemTickSequence sequence { get; }

    /// <summary>
    /// 绑定 World 并转调子类可重写的 OnSystemCreate 生命周期。
    /// </summary>
    public void OnCreate(World world)
    {
        World = world;
        OnSystemCreate();
    }

    /// <summary>
    /// 执行固定逻辑帧更新逻辑，由具体 System 实现。
    /// </summary>
    public abstract void Tick(in SimulationContext context);

    /// <summary>
    /// 转调子类销毁生命周期，并解除 World 引用。
    /// </summary>
    public void OnDestroy(World world)
    {
        OnSystemDestroy();
        World = null;
    }

    /// <summary>
    /// System 创建时的可选扩展点。
    /// </summary>
    protected virtual void OnSystemCreate()
    {
    }

    /// <summary>
    /// System 销毁时的可选扩展点。
    /// </summary>
    protected virtual void OnSystemDestroy()
    {
    }
}
