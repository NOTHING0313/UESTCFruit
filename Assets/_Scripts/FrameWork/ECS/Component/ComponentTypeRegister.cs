/*
 * 文件说明：ComponentTypeRegistry 负责为组件类型分配稳定的组件 ID，并把组件类型转换为 Mask bit。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections.Generic;

/// <summary>
/// 组件类型注册表，负责把组件类型映射为 Mask bit ID。
/// </summary>
public class ComponentTypeRegistry
{
    private const int MaxComponentTypes = 256;

    private readonly Dictionary<Type, int> _typeToId = new Dictionary<Type, int>();

    /// <summary>
    /// 获取组件类型 T 的注册 ID；如果尚未注册则自动注册。
    /// </summary>
    public int GetOrRegister<T>() where T : struct, IComponentData
    {
        return GetOrRegister(typeof(T));
    }

    /// <summary>
    /// 获取指定 Type 的注册 ID；如果尚未注册则分配新的组件类型 ID。
    /// </summary>
    private int GetOrRegister(Type type)
    {
        if (_typeToId.TryGetValue(type, out int id))
            return id;

        id = _typeToId.Count;
        if (id >= MaxComponentTypes)
            throw new InvalidOperationException("ComponentMask256 supports at most 256 component types.");

        _typeToId.Add(type, id);
        return id;
    }
    /// <summary>
    /// 为单个组件类型创建 ComponentMask256。
    /// </summary>
    public ComponentMask256 CreateMask<T1>() where T1 : struct, IComponentData
    {
        ComponentMask256 mask = default;
        mask.Set(GetOrRegister<T1>());
        return mask;
    }

    /// <summary>
    /// 为两个组件类型创建组合 ComponentMask256。
    /// </summary>
    public ComponentMask256 CreateMask<T1, T2>() where T1 : struct, IComponentData where T2 : struct, IComponentData
    {
        ComponentMask256 mask = default;
        mask.Set(GetOrRegister<T1>());
        mask.Set(GetOrRegister<T2>());
        return mask;
    }


    /// <summary>
    /// 为三个组件类型创建组合 ComponentMask256。
    /// </summary>
    public ComponentMask256 CreateMask<T1, T2, T3>() where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
    {
        ComponentMask256 mask = default;
        mask.Set(GetOrRegister<T1>());
        mask.Set(GetOrRegister<T2>());
        mask.Set(GetOrRegister<T3>());
        return mask;
    }
}
