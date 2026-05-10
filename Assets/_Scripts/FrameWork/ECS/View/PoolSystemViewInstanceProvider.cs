/*
 * 文件说明：PoolSystemViewInstanceProvider 适配已有的 PoolSystem.GameObjectPoolCenter。
 * 设计约束：这里通过反射接入对象池，避免 ECS Core 对 PoolSystem 产生强编译依赖。
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 基于 PoolSystem.GameObjectPoolCenter 的 View 实例提供器。
/// 如果项目中不存在 PoolSystem，会自动回退到 Instantiate / Destroy。
/// </summary>
public sealed class PoolSystemViewInstanceProvider : IViewInstanceProvider
{
    private readonly int _initialCapacity;
    private readonly HashSet<GameObject> _fallbackInstances = new HashSet<GameObject>();

    private Type _centerType;
    private PropertyInfo _instanceProperty;
    private MethodInfo _getInstanceMethod;
    private MethodInfo _releaseMethod;
    private bool _searchedPoolSystem;

    /// <summary>创建对象池 Provider；initialCapacity 小于等于 0 时使用对象池中心默认容量。</summary>
    public PoolSystemViewInstanceProvider(int initialCapacity = -1)
    {
        _initialCapacity = initialCapacity;
    }

    /// <summary>优先从 GameObjectPoolCenter 获取实例；对象池不可用时回退到 Instantiate。</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        object center = GetPoolCenterInstance();

        if (center == null || _getInstanceMethod == null)
            return SpawnFallback(prefab, position, rotation);

        try
        {
            object[] args = { prefab, position, rotation, null, null, _initialCapacity };
            GameObject instance = _getInstanceMethod.Invoke(center, args) as GameObject;

            if (instance == null)
                return SpawnFallback(prefab, position, rotation);

            return instance;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PoolSystemViewInstanceProvider] Pool Spawn failed, fallback to Instantiate. {exception.Message}");
            return SpawnFallback(prefab, position, rotation);
        }
    }

    /// <summary>优先释放到 GameObjectPoolCenter；回退实例使用 Destroy。</summary>
    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        if (_fallbackInstances.Remove(instance))
        {
            UnityEngine.Object.Destroy(instance);
            return;
        }

        object center = GetPoolCenterInstance();

        if (center == null || _releaseMethod == null)
        {
            UnityEngine.Object.Destroy(instance);
            return;
        }

        try
        {
            object[] args = { instance, null };
            _releaseMethod.Invoke(center, args);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PoolSystemViewInstanceProvider] Pool Release failed, fallback to Destroy. {exception.Message}");
            UnityEngine.Object.Destroy(instance);
        }
    }

    /// <summary>清理 Provider 自己创建的 fallback 实例，不清空全局对象池。</summary>
    public void Clear()
    {
        foreach (GameObject instance in _fallbackInstances)
        {
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        _fallbackInstances.Clear();
    }

    /// <summary>实例化回退路径，用于对象池不可用或调用失败时。</summary>
    private GameObject SpawnFallback(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        instance.SetActive(true);
        _fallbackInstances.Add(instance);
        return instance;
    }

    /// <summary>通过反射获取 PoolSystem.GameObjectPoolCenter.Instance。</summary>
    private object GetPoolCenterInstance()
    {
        EnsureReflectionCache();

        if (_centerType == null || _instanceProperty == null)
            return null;

        return _instanceProperty.GetValue(null, null);
    }

    /// <summary>缓存对象池中心的反射信息，避免每次 Spawn / Release 都重复查找。</summary>
    private void EnsureReflectionCache()
    {
        if (_searchedPoolSystem)
            return;

        _searchedPoolSystem = true;
        _centerType = FindType("PoolSystem.GameObjectPoolCenter");

        if (_centerType == null)
            return;

        _instanceProperty = _centerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        _getInstanceMethod = _centerType.GetMethod("GetInstance", new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(Transform), typeof(Action<GameObject>), typeof(int) });
        _releaseMethod = _centerType.GetMethod("Release", new[] { typeof(GameObject), typeof(Action<GameObject>) });
    }

    /// <summary>在当前 AppDomain 中查找指定类型。</summary>
    private static Type FindType(string fullName)
    {
        Type type = Type.GetType(fullName);

        if (type != null)
            return type;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            type = assemblies[i].GetType(fullName);

            if (type != null)
                return type;
        }

        return null;
    }
}
