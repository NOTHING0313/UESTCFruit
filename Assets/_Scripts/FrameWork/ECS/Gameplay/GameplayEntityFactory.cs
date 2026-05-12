/*
 * 文件说明：GameplayEntityFactory 是通用业务实体工厂，负责把 GameplayEntityDefinitionSO 转换为 ECS Entity。
 */

using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 通用业务实体工厂。
/// 它不会针对 Unit / Building / Bullet 分别写创建逻辑，而是根据 DefinitionSO 中启用的组件配置统一生成 Entity。
/// </summary>
public sealed class GameplayEntityFactory
{
    private readonly EntityFactory _entityFactory;

    /// <summary>DefinitionSO 启用的组件不在 BasePrefab 中时的处理策略。</summary>
    public EntityDefinitionMismatchPolicy MismatchPolicy { get; set; } = EntityDefinitionMismatchPolicy.WarnAndAdd;

    /// <summary>是否把 Warning 也输出到 Unity Console。</summary>
    public bool LogWarnings { get; set; } = true;

    /// <summary>Factory 所属的 World。</summary>
    public World World => _entityFactory?.World;

    /// <summary>创建通用业务实体工厂。</summary>
    public GameplayEntityFactory(EntityFactory entityFactory)
    {
        _entityFactory = entityFactory;
    }

    /// <summary>根据 DefinitionSO 和默认创建上下文创建 Entity。</summary>
    public Entity Create(GameplayEntityDefinitionSO definition)
    {
        EntityCreateContext context = EntityCreateContext.Default;
        return Create(definition, in context, null);
    }

    /// <summary>根据 DefinitionSO 和创建上下文创建 Entity。</summary>
    public Entity Create(GameplayEntityDefinitionSO definition, in EntityCreateContext context)
    {
        return Create(definition, in context, null);
    }

        /// <summary>根据 DefinitionSO 和创建上下文创建 Entity，并允许最终覆盖组件。</summary>
        /// <summary>
        /// 根据 GameplayEntityDefinitionSO 创建实体，并允许外部通过 overrideBuilder 做最终覆盖。
        /// </summary>
        public Entity Create(GameplayEntityDefinitionSO definition, in EntityCreateContext context, Action<EntityBuilder> overrideBuilder)
        {
            if (_entityFactory == null || definition == null)
                return Entity.Invalid;

            EntityDefinitionValidationResult result = definition.ValidateDefinition(MismatchPolicy);
            LogValidationResult(definition, result);

            if (result.HasError)
                return Entity.Invalid;

            EntityPrefabSO prefab = definition.BasePrefab;

            if (prefab == null)
                return Entity.Invalid;

            EntityCreateContext localContext = context;

            return _entityFactory.Create(prefab, builder =>
            {
                EntityCreateContext applyContext = localContext;
                definition.Components.Apply(builder, in applyContext);
                overrideBuilder?.Invoke(builder);
            });
        }
        /// <summary>尝试创建 Entity。</summary>
        public bool TryCreate(GameplayEntityDefinitionSO definition, in EntityCreateContext context, out Entity entity)
    {
        entity = Create(definition, in context, null);
        return entity.IsValid && World != null && World.IsAlive(entity);
    }

    /// <summary>尝试创建 Entity，并允许最终覆盖组件。</summary>
    public bool TryCreate(GameplayEntityDefinitionSO definition, in EntityCreateContext context, Action<EntityBuilder> overrideBuilder, out Entity entity)
    {
        entity = Create(definition, in context, overrideBuilder);
        return entity.IsValid && World != null && World.IsAlive(entity);
    }

    /// <summary>按策略输出校验消息。</summary>
    private void LogValidationResult(GameplayEntityDefinitionSO definition, EntityDefinitionValidationResult result)
    {
        if (result == null || !result.HasMessage)
            return;

        UnityEngine.Object context = definition;

        for (int i = 0; i < result.Messages.Count; i++)
        {
            EntityDefinitionValidationMessage message = result.Messages[i];

            if (message.severity == EntityDefinitionValidationSeverity.Error)
            {
                Debug.LogError(message.message, context);
                continue;
            }

            if (LogWarnings)
                Debug.LogWarning(message.message, context);
        }
    }
}

}
