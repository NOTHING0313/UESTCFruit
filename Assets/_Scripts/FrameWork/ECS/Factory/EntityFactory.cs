/*
 * 文件说明：EntityFactory 管理多个 EntityPrefab，并提供基于 key 的统一实体创建入口。
 * 设计约束：Factory 不参与 World.Tick，不直接访问 Manager / Store，只通过 IEntityPrefab 和 EntityBuilder 创建实体。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// ECS 实体工厂。
/// 用于注册多个 EntityPrefab，并通过字符串 key 创建单位、子弹、建筑等实体。
/// </summary>
public sealed class EntityFactory
{
    private readonly World _world;
    private readonly Dictionary<string, IEntityPrefab> _prefabs = new Dictionary<string, IEntityPrefab>();

    /// <summary>Factory 所属 World。</summary>
    public World World => _world;

    /// <summary>当前已注册的 Prefab 数量。</summary>
    public int PrefabCount => _prefabs.Count;

    /// <summary>创建绑定指定 World 的 EntityFactory。</summary>
    public EntityFactory(World world)
    {
        _world = world;
    }

    /// <summary>
    /// 注册 Prefab。若 key 已存在、key 无效或 prefab 为空，则返回 false。
    /// </summary>
    public bool RegisterPrefab(string key, IEntityPrefab prefab)
    {
        if (string.IsNullOrEmpty(key) || prefab == null || _prefabs.ContainsKey(key))
            return false;

        _prefabs.Add(key, prefab);
        return true;
    }

    /// <summary>
    /// 设置 Prefab。若 key 已存在则覆盖，不存在则新增。
    /// </summary>
    public bool SetPrefab(string key, IEntityPrefab prefab)
    {
        if (string.IsNullOrEmpty(key) || prefab == null)
            return false;

        _prefabs[key] = prefab;
        return true;
    }

    /// <summary>注销指定 key 的 Prefab。</summary>
    public bool UnregisterPrefab(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return _prefabs.Remove(key);
    }

    /// <summary>判断指定 key 是否已注册 Prefab。</summary>
    public bool HasPrefab(string key)
    {
        return !string.IsNullOrEmpty(key) && _prefabs.ContainsKey(key);
    }

    /// <summary>尝试获取指定 key 对应的 Prefab。</summary>
    public bool TryGetPrefab(string key, out IEntityPrefab prefab)
    {
        if (string.IsNullOrEmpty(key))
        {
            prefab = null;
            return false;
        }

        return _prefabs.TryGetValue(key, out prefab);
    }


    /// <summary>基于指定 Prefab 直接创建 Entity。Prefab 无效、World 无效或 World 已释放时返回 Entity.Invalid。</summary>
    public Entity Create(IEntityPrefab prefab)
    {
        return TryCreate(prefab, out Entity entity) ? entity : Entity.Invalid;
    }

    /// <summary>基于指定 Prefab 直接创建 Entity，并在 Prefab 应用后执行运行时覆盖配置。</summary>
    public Entity Create(IEntityPrefab prefab, Action<EntityBuilder> overrideBuilder)
    {
        return TryCreate(prefab, overrideBuilder, out Entity entity) ? entity : Entity.Invalid;
    }

    /// <summary>安全尝试基于指定 Prefab 直接创建 Entity。</summary>
    public bool TryCreate(IEntityPrefab prefab, out Entity entity)
    {
        entity = Entity.Invalid;

        if (_world == null || _world.IsDisposing() || prefab == null)
            return false;

        entity = prefab.Create(_world);
        return entity.IsValid && _world.IsAlive(entity);
    }

    /// <summary>安全尝试基于指定 Prefab 直接创建 Entity，并在 Prefab 应用后执行运行时覆盖配置。</summary>
    public bool TryCreate(IEntityPrefab prefab, Action<EntityBuilder> overrideBuilder, out Entity entity)
    {
        if (!TryCreate(prefab, out entity))
            return false;

        if (overrideBuilder != null)
        {
            EntityBuilder builder = new EntityBuilder(_world, entity);
            overrideBuilder.Invoke(builder);
            entity = builder.Build();
        }

        return entity.IsValid && _world.IsAlive(entity);
    }

    /// <summary>基于指定 key 创建 Entity。key 不存在、World 无效或 World 已释放时返回 Entity.Invalid。</summary>
    public Entity Create(string key)
    {
        return TryCreate(key, out Entity entity) ? entity : Entity.Invalid;
    }

    /// <summary>基于指定 key 创建 Entity，并在 Prefab 应用后执行运行时覆盖配置。</summary>
    public Entity Create(string key, Action<EntityBuilder> overrideBuilder)
    {
        return TryCreate(key, overrideBuilder, out Entity entity) ? entity : Entity.Invalid;
    }

    /// <summary>安全尝试基于指定 key 创建 Entity。</summary>
    public bool TryCreate(string key, out Entity entity)
    {
        entity = Entity.Invalid;

        if (_world == null || _world.IsDisposing() || !TryGetPrefab(key, out IEntityPrefab prefab))
            return false;

        entity = prefab.Create(_world);
        return entity.IsValid && _world.IsAlive(entity);
    }

    /// <summary>安全尝试基于指定 key 创建 Entity，并在 Prefab 应用后执行运行时覆盖配置。</summary>
    public bool TryCreate(string key, Action<EntityBuilder> overrideBuilder, out Entity entity)
    {
        if (!TryCreate(key, out entity))
            return false;

        if (overrideBuilder != null)
        {
            EntityBuilder builder = new EntityBuilder(_world, entity);
            overrideBuilder.Invoke(builder);
            entity = builder.Build();
        }

        return entity.IsValid && _world.IsAlive(entity);
    }

    /// <summary>清空当前 Factory 注册的全部 Prefab。</summary>
    public void Clear()
    {
        _prefabs.Clear();
    }
}

}
