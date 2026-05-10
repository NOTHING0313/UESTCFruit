/*
 * 文件说明：WorldUnityExtensions 提供 World 与 Unity View 创建、销毁请求相关的便捷扩展方法。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using UnityEngine;

/// <summary>
/// World 与 Unity View 请求相关的扩展方法。
/// </summary>
public static class WorldUnityExtensions
{
    /// <summary>创建带 View 生成请求的 Entity，并设置初始位置。</summary>
    public static EntityInfo CreateEntityWithView(this World world, int prefabID, Vector3 position)
    {
        if (world == null)
            return default;

        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new PositionComponent(position.x, position.y, position.z));
        world.SetComponent(entity, new PrefabViewRequestComponent(prefabID));

        return entity;
    }

    /// <summary>创建带 View 生成请求和速度组件的 Entity。</summary>
    public static EntityInfo CreateMovingEntityWithView(this World world, int prefabID, Vector3 position, Vector3 velocity)
    {
        if (world == null)
            return default;

        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new PositionComponent(position.x, position.y, position.z));
        world.SetComponent(entity, new VelocityComponent(velocity.x, velocity.y, velocity.z));
        world.SetComponent(entity, new PrefabViewRequestComponent(prefabID));

        return entity;
    }

    /// <summary>为已有 Entity 请求生成 View。</summary>
    public static bool RequestView(this World world, EntityInfo entity, int prefabID)
    {
        if (world == null || !world.IsAlive(entity))
            return false;

        if (world.HasComponent<ViewComponent>(entity))
            return false;

        world.SetComponent(entity, new PrefabViewRequestComponent(prefabID));
        return true;
    }

    /// <summary>请求销毁 Entity；如果 Entity 关联了 View，则走 View 安全销毁流程。</summary>
    public static bool DestroyEntityWithView(this World world, EntityInfo entity)
    {
        if (world == null || !world.IsAlive(entity))
            return false;

        bool hasView = world.HasComponent<ViewComponent>(entity);
        bool hasViewRequest = world.HasComponent<PrefabViewRequestComponent>(entity);

        if (hasView || hasViewRequest)
        {
            world.SetComponent(entity, new EntityDestroyRequestComponent());
            return true;
        }

        world.DestroyEntity(entity);
        return true;
    }

    /// <summary>只请求销毁 View，不销毁 Entity。</summary>
    public static bool DestroyViewOnly(this World world, EntityInfo entity)
    {
        if (world == null || !world.IsAlive(entity))
            return false;

        if (!world.HasComponent<ViewComponent>(entity))
        {
            if (world.HasComponent<PrefabViewRequestComponent>(entity))
                world.RemoveComponent<PrefabViewRequestComponent>(entity);

            return false;
        }

        world.SetComponent(entity, new ViewDestroyRequestComponent());
        return true;
    }
}
