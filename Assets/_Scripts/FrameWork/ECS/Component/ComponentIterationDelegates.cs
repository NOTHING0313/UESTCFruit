namespace ECSFrameWork
{
/*
 * 文件说明：ComponentIterationDelegates 定义高频组件遍历使用的委托类型。
 * 设计约束：该委托只负责传递 Entity 与组件 ref，不应在回调中直接破坏当前遍历的 Store 结构。
 */


/// <summary>
/// 单组件高性能遍历委托。
/// 回调中可以修改传入的组件 ref，但不建议立即增删当前正在遍历的组件类型。
/// </summary>
public delegate void EntityComponentAction<T>(Entity entity, ref T component) where T : struct, IComponentData;

/// <summary>
/// 双组件高性能遍历委托。
/// 回调中可以修改传入的组件 ref，但不建议立即增删当前正在遍历的组件类型。
/// </summary>
public delegate void EntityComponentAction<T1, T2>(Entity entity, ref T1 component1, ref T2 component2) where T1 : struct, IComponentData where T2 : struct, IComponentData;

/// <summary>
/// 三组件高性能遍历委托。
/// 回调中可以修改传入的组件 ref，但不建议立即增删当前正在遍历的组件类型。
/// </summary>
public delegate void EntityComponentAction<T1, T2, T3>(Entity entity, ref T1 component1, ref T2 component2, ref T3 component3) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData;

}
