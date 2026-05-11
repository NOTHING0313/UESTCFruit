/*
 * 文件说明：IEntityPrefab 描述 ECS 层实体模板接口，用于把一组默认组件应用到 Entity。
 */

namespace ECSFrameWork
{

/// <summary>
/// ECS 实体模板接口。
/// Prefab 只描述 ECS 组件组合，不直接持有或创建 Unity GameObject。
/// </summary>
public interface IEntityPrefab
{
    /// <summary>Prefab 名称，主要用于调试、日志和工厂注册。</summary>
    string Name { get; }

    /// <summary>Prefab 当前包含的组件模板数量。</summary>
    int ComponentCount { get; }

    /// <summary>基于模板创建一个新的 Entity，并写入模板中的全部组件。</summary>
    Entity Create(World world);

    /// <summary>将模板中的全部组件应用到已有 Entity。</summary>
    void ApplyTo(World world, Entity entity);
}

}
