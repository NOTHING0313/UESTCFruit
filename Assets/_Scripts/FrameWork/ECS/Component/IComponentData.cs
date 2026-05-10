/*
 * 文件说明：IComponentData 是所有 ECS 组件的标记接口，用于约束 ComponentStore<T>、World.SetComponent<T> 和 Query.With<T> 的泛型参数。
 * 设计约束：组件应优先使用 struct，避免在核心逻辑中持有 UnityEngine.Object、Transform、GameObject 等非确定性表现层引用。
 */

/// <summary>
/// ECS 组件标记接口。
/// </summary>
/// <remarks>
/// 组件只保存数据，不直接包含生命周期逻辑；逻辑应放在 IFixedStepSystem 中处理。
/// </remarks>
public interface IComponentData
{
}
