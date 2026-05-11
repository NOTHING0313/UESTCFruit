namespace ECSFrameWork
{
/*
 * 文件说明：ECSUnityComponents 提供 Unity 接入层和基础移动测试所需的示例组件。
 * 设计约束：PositionComponent 是逻辑位置真值，Transform 只应由 ViewSyncSystem 根据该组件同步。
 */

/// <summary>
/// ECS 逻辑位置组件。
/// </summary>
public struct PositionComponent : IComponentData
{
    /// <summary>逻辑空间 X 坐标。</summary>
    public float x;

    /// <summary>逻辑空间 Y 坐标。</summary>
    public float y;

    /// <summary>逻辑空间 Z 坐标。</summary>
    public float z;

    /// <summary>创建逻辑位置组件。</summary>
    public PositionComponent(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

/// <summary>
/// ECS 逻辑速度组件。
/// </summary>
public struct VelocityComponent : IComponentData
{
    /// <summary>X 方向速度。</summary>
    public float x;

    /// <summary>Y 方向速度。</summary>
    public float y;

    /// <summary>Z 方向速度。</summary>
    public float z;

    /// <summary>创建逻辑速度组件。</summary>
    public VelocityComponent(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

/// <summary>
/// Unity View 映射组件，保存 ViewManager 分配的 viewID。
/// </summary>
public struct ViewComponent : IComponentData
{
    /// <summary>ViewManager 中的表现对象 ID。</summary>
    public int viewID;

    /// <summary>创建 View 映射组件。</summary>
    public ViewComponent(int viewID)
    {
        this.viewID = viewID;
    }
}

/// <summary>
/// 请求创建 Unity View 的一次性组件。
/// </summary>
public struct PrefabViewRequestComponent : IComponentData
{
    /// <summary>ViewManager 中注册的 Prefab ID。</summary>
    public int prefabID;

    /// <summary>创建 Prefab View 请求。</summary>
    public PrefabViewRequestComponent(int prefabID)
    {
        this.prefabID = prefabID;
    }
}

/// <summary>
/// 请求销毁或注销 Unity View 的一次性组件。
/// </summary>
public struct ViewDestroyRequestComponent : IComponentData
{
}

/// <summary>
/// 请求销毁 Entity 的一次性组件。
/// </summary>
public struct EntityDestroyRequestComponent : IComponentData
{
}

/// <summary>
/// 玩家实体标记组件。
/// </summary>
public struct PlayerTagComponent : IComponentData
{
}

/// <summary>
/// 移动速度组件，通常由输入系统结合 PlayerInputSnapshotComponent 计算 VelocityComponent。
/// </summary>
public struct MoveSpeedComponent : IComponentData
{
    /// <summary>单位逻辑帧速度倍率。</summary>
    public float value;

    /// <summary>创建移动速度组件。</summary>
    public MoveSpeedComponent(float value)
    {
        this.value = value;
    }
}

}
