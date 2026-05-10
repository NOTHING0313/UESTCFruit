/*
 * 文件说明：IFixedStepSystem 定义 ECS 固定逻辑帧系统的统一接口、生命周期接口和执行顺序枚举。
 * 设计约束：System 应尽量无状态；需要被回滚或同步的状态应放入 Component。
 */

/// <summary>
/// 固定逻辑帧 System 接口。
/// </summary>
/// <remarks>
/// System 由 SystemManager 按 sequence 排序执行，并通过 OnCreate / OnDestroy 绑定或释放 World。
/// </remarks>
public interface IFixedStepSystem : ISystemInitialize, ISystemDestroy
{
    /// <summary>
    /// 执行一次固定逻辑帧更新。
    /// </summary>
    void Tick(in SimulationContext context);

    /// <summary>
    /// System 执行顺序；数值越小越早执行。
    /// </summary>
    SystemTickSequence sequence { get; }
}

/// <summary>
/// System 初始化生命周期接口。
/// </summary>
public interface ISystemInitialize
{
    /// <summary>
    /// System 被加入 World 时调用。
    /// </summary>
    void OnCreate(World world);
}

/// <summary>
/// System 销毁生命周期接口。
/// </summary>
public interface ISystemDestroy
{
    /// <summary>
    /// System 从 World 移除或 World 释放时调用。
    /// </summary>
    void OnDestroy(World world);
}

/// <summary>
/// 固定帧 System 执行阶段。
/// </summary>
public enum SystemTickSequence
{
    /// <summary>输入组件应用与输入驱动逻辑。</summary>
    input = -400,

    /// <summary>外部帧指令或命令系统。</summary>
    command = -350,

    /// <summary>实体或表现对象生成请求。</summary>
    spawn = -300,

    /// <summary>通用玩法逻辑。</summary>
    logic = -200,

    /// <summary>移动与位置更新。</summary>
    movement = -100,

    /// <summary>伤害结算。</summary>
    damage = -50,

    /// <summary>默认阶段。</summary>
    normal = 0,

    /// <summary>逻辑清理阶段，例如死亡标记清理。</summary>
    cleanup = 50,

    /// <summary>表现同步阶段。</summary>
    view = 100,

    /// <summary>表现对象清理阶段。</summary>
    viewCleanup = 200,

    /// <summary>实体最终清理阶段。</summary>
    entityCleanup = 300,
}
