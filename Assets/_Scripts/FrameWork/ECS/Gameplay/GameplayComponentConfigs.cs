/*
 * 文件说明：GameplayComponentConfigs 把 DefinitionSO 中的配置字段按组件分组，并负责转换为 ECS Component。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>PositionComponent 的业务配置。</summary>
[Serializable]
public struct PositionComponentConfig
{
    public bool enabled;
    public bool useCreateContextPosition;
    public float x;
    public float y;
    public float z;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(PositionComponent);

    /// <summary>把位置配置写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        if (useCreateContextPosition)
            builder.With(new PositionComponent(context.position.x, context.position.y, context.position.z));
        else
            builder.With(new PositionComponent(x, y, z));
    }
}

/// <summary>VelocityComponent 的业务配置。</summary>
[Serializable]
public struct VelocityComponentConfig
{
    public bool enabled;
    public bool useCreateContextVelocity;
    public float x;
    public float y;
    public float z;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(VelocityComponent);

    /// <summary>把速度配置写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        if (useCreateContextVelocity)
            builder.With(new VelocityComponent(context.velocity.x, context.velocity.y, context.velocity.z));
        else
            builder.With(new VelocityComponent(x, y, z));
    }
}

/// <summary>HealthComponent 的业务配置。</summary>
[Serializable]
public struct HealthComponentConfig
{
    public bool enabled;
    public int current;
    public int max;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(HealthComponent);

    /// <summary>把生命值配置写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        int finalCurrent = current <= 0 ? max : current;
        builder.With(new HealthComponent(finalCurrent, max));
    }

    /// <summary>校验生命值配置。</summary>
    public void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        if (!enabled || result == null)
            return;

        if (max <= 0)
            result.AddError($"[{ownerName}] HealthComponentConfig is enabled, but max <= 0.");

        if (current < 0)
            result.AddWarning($"[{ownerName}] HealthComponentConfig current < 0. It will be treated as max when applied.");
    }
}

/// <summary>MoveSpeedComponent 的业务配置。</summary>
[Serializable]
public struct MoveSpeedComponentConfig
{
    public bool enabled;
    public float value;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(MoveSpeedComponent);

    /// <summary>把移动速度配置写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        builder.With(new MoveSpeedComponent(value));
    }

    /// <summary>校验移动速度配置。</summary>
    public void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        if (!enabled || result == null)
            return;

        if (value < 0f)
            result.AddError($"[{ownerName}] MoveSpeedComponentConfig is enabled, but value < 0.");
    }
}

/// <summary>StatComponent 的业务配置。</summary>
[Serializable]
public struct StatComponentConfig
{
    public bool enabled;
    public int attack;
    public int defense;
    public int moveSpeed;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(StatComponent);

    /// <summary>把属性配置写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        builder.With(new StatComponent(attack, defense, moveSpeed));
    }
}

/// <summary>PrefabViewRequestComponent 的业务配置。</summary>
[Serializable]
public struct PrefabViewRequestComponentConfig
{
    public bool enabled;
    public int prefabID;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(PrefabViewRequestComponent);

    /// <summary>把 View 创建请求写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        builder.With(new PrefabViewRequestComponent(prefabID));
    }

    /// <summary>校验 View 创建请求配置。</summary>
    public void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        if (!enabled || result == null)
            return;

        if (prefabID < 0)
            result.AddError($"[{ownerName}] PrefabViewRequestComponentConfig is enabled, but prefabID < 0.");
    }
}

/// <summary>PlayerTagComponent 的业务配置。</summary>
[Serializable]
public struct PlayerTagComponentConfig
{
    public bool enabled;

    public bool Enabled => enabled;
    public Type ComponentType => typeof(PlayerTagComponent);

    /// <summary>把玩家标记写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        if (!enabled || builder == null)
            return;

        builder.With(new PlayerTagComponent());
    }
}

/// <summary>通用业务 Entity 的组件配置集合。</summary>
[Serializable]
public struct GameplayComponentConfigSet
{
    public PositionComponentConfig position;
    public VelocityComponentConfig velocity;
    public HealthComponentConfig health;
    public MoveSpeedComponentConfig moveSpeed;
    public StatComponentConfig stat;
    public PrefabViewRequestComponentConfig viewRequest;
    public PlayerTagComponentConfig playerTag;

    /// <summary>把所有启用的组件配置写入 EntityBuilder。</summary>
    public void Apply(EntityBuilder builder, in EntityCreateContext context)
    {
        position.Apply(builder, in context);
        velocity.Apply(builder, in context);
        health.Apply(builder, in context);
        moveSpeed.Apply(builder, in context);
        stat.Apply(builder, in context);
        viewRequest.Apply(builder, in context);
        playerTag.Apply(builder, in context);
    }

    /// <summary>校验所有启用的组件配置。</summary>
    public void Validate(EntityDefinitionValidationResult result, string ownerName)
    {
        health.Validate(result, ownerName);
        moveSpeed.Validate(result, ownerName);
        viewRequest.Validate(result, ownerName);
    }

    /// <summary>把所有启用配置对应的 ComponentType 填充到外部 List 中。</summary>
    public int FillEnabledComponentTypes(List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (position.Enabled) results.Add(position.ComponentType);
        if (velocity.Enabled) results.Add(velocity.ComponentType);
        if (health.Enabled) results.Add(health.ComponentType);
        if (moveSpeed.Enabled) results.Add(moveSpeed.ComponentType);
        if (stat.Enabled) results.Add(stat.ComponentType);
        if (viewRequest.Enabled) results.Add(viewRequest.ComponentType);
        if (playerTag.Enabled) results.Add(playerTag.ComponentType);

        return results.Count;
    }
}

}
