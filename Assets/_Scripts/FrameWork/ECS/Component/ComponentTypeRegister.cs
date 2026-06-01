/*
 * 文件说明：ComponentTypeRegistry 负责为组件类型分配稳定的组件 ID，并把组件类型转换为 Mask bit。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 组件类型注册表，负责把组件类型映射为 Mask bit ID。
/// </summary>
internal class ComponentTypeRegistry
{
    private const int MaxComponentTypes = 256;

    private readonly Dictionary<Type, int> _typeToId = new Dictionary<Type, int>();
    private readonly List<Type> _idToType = new List<Type>();

    /// <summary>已经注册过的组件类型数量。</summary>
    public int RegisteredTypeCount => _idToType.Count;

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
    internal int GetOrRegister(Type type)
    {
        ValidateComponentType(type);

        if (_typeToId.TryGetValue(type, out int id))
            return id;

        id = _typeToId.Count;
        if (id >= MaxComponentTypes)
            throw new InvalidOperationException("ComponentMask256 supports at most 256 component types.");

        _typeToId.Add(type, id);
        _idToType.Add(type);
        return id;
    }

    /// <summary>
    /// 捕获当前组件类型注册顺序。
    /// </summary>
    internal IReadOnlyList<Type> CaptureRegisteredTypes()
    {
        return _idToType.ToArray();
    }

    /// <summary>
    /// 按快照顺序恢复组件类型注册表；失败时不会修改当前注册表。
    /// </summary>
    internal void RestoreRegisteredTypes(IReadOnlyList<Type> registeredTypes)
    {
        if (registeredTypes == null)
            throw new ArgumentNullException(nameof(registeredTypes));

        if (registeredTypes.Count > MaxComponentTypes)
            throw new InvalidOperationException("ComponentMask256 supports at most 256 component types.");

        Dictionary<Type, int> restoredTypeToId = new Dictionary<Type, int>(registeredTypes.Count);
        List<Type> restoredIdToType = new List<Type>(registeredTypes.Count);

        for (int i = 0; i < registeredTypes.Count; i++)
        {
            Type type = registeredTypes[i];
            ValidateComponentType(type);

            if (restoredTypeToId.ContainsKey(type))
                throw new InvalidOperationException($"Duplicate component type in registry snapshot: {type.FullName}");

            restoredTypeToId.Add(type, i);
            restoredIdToType.Add(type);
        }

        _typeToId.Clear();
        _idToType.Clear();

        for (int i = 0; i < restoredIdToType.Count; i++)
        {
            Type type = restoredIdToType[i];
            _typeToId.Add(type, i);
            _idToType.Add(type);
        }
    }

    private static void ValidateComponentType(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (!typeof(IComponentData).IsAssignableFrom(type))
            throw new InvalidOperationException($"Type must implement IComponentData: {type.FullName}");

        if (!type.IsValueType)
            throw new InvalidOperationException($"Component type must be a struct: {type.FullName}");
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

    /// <summary>
    /// 尝试通过组件注册 ID 获取组件类型。
    /// </summary>
    public bool TryGetType(int id, out Type type)
    {
        if (id >= 0 && id < _idToType.Count)
        {
            type = _idToType[id];
            return true;
        }

        type = null;
        return false;
    }

    /// <summary>
    /// 把当前已经注册过的组件类型写入外部 List。
    /// </summary>
    public int FillRegisteredTypes(List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        for (int i = 0; i < _idToType.Count; i++)
            results.Add(_idToType[i]);

        return results.Count;
    }

    /// <summary>
    /// 根据 ComponentMask256 把其中包含的组件类型写入外部 List。
    /// </summary>
    public int FillTypesByMask(ComponentMask256 mask, List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        for (int i = 0; i < _idToType.Count; i++)
        {
            if (mask.Has(i))
                results.Add(_idToType[i]);
        }

        return results.Count;
    }
}

}
