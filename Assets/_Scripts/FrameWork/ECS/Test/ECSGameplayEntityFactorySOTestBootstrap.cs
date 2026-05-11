/*
 * 文件说明：ECSGameplayEntityFactorySOTestBootstrap 用于验证 PrefabSO + DefinitionSO + GameplayEntityFactory 的创建链路。
 */

using System;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// PrefabSO / DefinitionSO / GameplayEntityFactory 测试入口。
/// 挂到空 GameObject 后运行场景即可在 Console 中看到测试结果。
/// </summary>
public sealed class ECSGameplayEntityFactorySOTestBootstrap : MonoBehaviour
{
    /// <summary>自动执行测试。</summary>
    private void Start()
    {
        RunAllTests();
    }

    /// <summary>手动执行全部测试。</summary>
    [ContextMenu("Run ECS Gameplay Entity Factory SO Tests")]
    public void RunAllTests()
    {
        Test_CreateEntity_ByDefinition_OverridesPrefabAndUsesContext();
        Test_OverrideBuilder_HasHighestPriority();
        Test_MismatchWarnAndAdd_AllowsDefinitionToAddComponent();
        Test_MismatchReject_ReturnsInvalidEntity();
        Test_DuplicatePreset_LaterPresetOverwritesEarlierPreset();
        Test_EntityFactory_DirectPrefabCreate_Works();

        Debug.Log("[ECSGameplayEntityFactorySOTestBootstrap] All tests passed.");
    }

    /// <summary>验证 DefinitionSO 覆盖 PrefabSO，并使用 Context 写入运行时位置。</summary>
    private void Test_CreateEntity_ByDefinition_OverridesPrefabAndUsesContext()
    {
        World world = new World();
        EntityFactory entityFactory = new EntityFactory(world);
        GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory) { LogWarnings = false };

        EntityPrefabSO prefab = CreateUnitBasePrefab("UnitBase");
        GameplayEntityDefinitionSO definition = CreateScoutDefinition(prefab);
        EntityCreateContext context = EntityCreateContext.Default;
        context.position = new Vector3(10f, 0f, 5f);

        Entity entity = gameplayFactory.Create(definition, in context);

        Assert(world.IsAlive(entity), "Created entity should be alive.");
        Assert(world.TryGetComponent(entity, out HealthComponent health), "Entity should have HealthComponent.");
        Assert(health.current == 80 && health.max == 80, "DefinitionSO should override HealthComponent.");
        Assert(world.TryGetComponent(entity, out MoveSpeedComponent moveSpeed), "Entity should have MoveSpeedComponent.");
        Assert(Math.Abs(moveSpeed.value - 8f) < 0.0001f, "DefinitionSO should override MoveSpeedComponent.");
        Assert(world.TryGetComponent(entity, out PrefabViewRequestComponent view), "Entity should have PrefabViewRequestComponent.");
        Assert(view.prefabID == 1001, "DefinitionSO should override PrefabViewRequestComponent.");
        Assert(world.TryGetComponent(entity, out PositionComponent position), "Entity should have PositionComponent from context.");
        Assert(Math.Abs(position.x - 10f) < 0.0001f && Math.Abs(position.z - 5f) < 0.0001f, "PositionComponent should use EntityCreateContext position.");

        world.Dispose();
    }

    /// <summary>验证 overrideBuilder 拥有最终覆盖优先级。</summary>
    private void Test_OverrideBuilder_HasHighestPriority()
    {
        World world = new World();
        EntityFactory entityFactory = new EntityFactory(world);
        GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory) { LogWarnings = false };

        EntityPrefabSO prefab = CreateUnitBasePrefab("UnitBase");
        GameplayEntityDefinitionSO definition = CreateScoutDefinition(prefab);
        EntityCreateContext context = EntityCreateContext.Default;

        Entity entity = gameplayFactory.Create(definition, in context, builder =>
        {
            builder.With(new HealthComponent(1, 80));
        });

        Assert(world.TryGetComponent(entity, out HealthComponent health), "Entity should have HealthComponent.");
        Assert(health.current == 1 && health.max == 80, "overrideBuilder should override DefinitionSO values.");

        world.Dispose();
    }

    /// <summary>验证 WarnAndAdd 策略允许 DefinitionSO 添加 BasePrefab 中没有的组件。</summary>
    private void Test_MismatchWarnAndAdd_AllowsDefinitionToAddComponent()
    {
        World world = new World();
        EntityFactory entityFactory = new EntityFactory(world);
        GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory)
        {
            LogWarnings = false,
            MismatchPolicy = EntityDefinitionMismatchPolicy.WarnAndAdd,
        };

        EntityPrefabSO prefab = CreateHealthOnlyPrefab("HealthOnly");
        GameplayEntityDefinitionSO definition = CreateStatOnlyDefinition(prefab);
        EntityCreateContext context = EntityCreateContext.Default;

        Entity entity = gameplayFactory.Create(definition, in context);

        Assert(world.IsAlive(entity), "WarnAndAdd should allow entity creation.");
        Assert(world.HasComponent<StatComponent>(entity), "WarnAndAdd should add component enabled by DefinitionSO.");

        world.Dispose();
    }

    /// <summary>验证 Reject 策略会拒绝 DefinitionSO 添加 BasePrefab 中没有的组件。</summary>
    private void Test_MismatchReject_ReturnsInvalidEntity()
    {
        World world = new World();
        EntityFactory entityFactory = new EntityFactory(world);
        GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory)
        {
            LogWarnings = false,
            MismatchPolicy = EntityDefinitionMismatchPolicy.Reject,
        };

        EntityPrefabSO prefab = CreateHealthOnlyPrefab("HealthOnly");
        GameplayEntityDefinitionSO definition = CreateStatOnlyDefinition(prefab);
        EntityCreateContext context = EntityCreateContext.Default;

        Entity entity = gameplayFactory.Create(definition, in context);

        Assert(!entity.IsValid, "Reject should return Entity.Invalid when DefinitionSO enables a missing component.");
        Assert(world.AliveEntityCount == 0, "Reject should not create entity.");

        world.Dispose();
    }

    /// <summary>验证 PrefabSO 中重复组件时后面的预设覆盖前面的预设。</summary>
    private void Test_DuplicatePreset_LaterPresetOverwritesEarlierPreset()
    {
        World world = new World();
        EntityFactory entityFactory = new EntityFactory(world);
        GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory) { LogWarnings = false };

        HealthComponentPresetSO first = ScriptableObject.CreateInstance<HealthComponentPresetSO>();
        first.Configure(100, 100);

        HealthComponentPresetSO second = ScriptableObject.CreateInstance<HealthComponentPresetSO>();
        second.Configure(300, 300);

        EntityPrefabSO prefab = ScriptableObject.CreateInstance<EntityPrefabSO>();
        prefab.Configure("DuplicateHealth", first, second);

        GameplayComponentConfigSet components = new GameplayComponentConfigSet();
        GameplayEntityDefinitionSO definition = ScriptableObject.CreateInstance<GameplayEntityDefinitionSO>();
        definition.Configure("duplicate_health", "Duplicate Health", prefab, in components);
        EntityCreateContext context = EntityCreateContext.Default;

        Entity entity = gameplayFactory.Create(definition, in context);

        Assert(world.TryGetComponent(entity, out HealthComponent health), "Entity should have HealthComponent from duplicate presets.");
        Assert(health.current == 300 && health.max == 300, "Later duplicate preset should overwrite earlier component data.");

        world.Dispose();
    }

    /// <summary>验证 EntityFactory 可以直接接收 IEntityPrefab 创建 Entity。</summary>
    private void Test_EntityFactory_DirectPrefabCreate_Works()
    {
        World world = new World();
        EntityFactory entityFactory = new EntityFactory(world);
        EntityPrefabSO prefab = CreateUnitBasePrefab("UnitBase");

        Entity entity = entityFactory.Create(prefab, builder =>
        {
            builder.With(new PositionComponent(1f, 2f, 3f));
        });

        Assert(world.IsAlive(entity), "Direct prefab create should create alive entity.");
        Assert(world.HasComponent<HealthComponent>(entity), "Direct prefab create should apply prefab presets.");
        Assert(world.TryGetComponent(entity, out PositionComponent position), "Direct prefab create should apply overrideBuilder.");
        Assert(Math.Abs(position.y - 2f) < 0.0001f, "PositionComponent should be written by overrideBuilder.");

        world.Dispose();
    }

    /// <summary>创建一个基础单位 PrefabSO。</summary>
    private EntityPrefabSO CreateUnitBasePrefab(string key)
    {
        HealthComponentPresetSO health = ScriptableObject.CreateInstance<HealthComponentPresetSO>();
        health.Configure(100, 100);

        MoveSpeedComponentPresetSO moveSpeed = ScriptableObject.CreateInstance<MoveSpeedComponentPresetSO>();
        moveSpeed.Configure(5f);

        PrefabViewRequestComponentPresetSO view = ScriptableObject.CreateInstance<PrefabViewRequestComponentPresetSO>();
        view.Configure(1000);

        EntityPrefabSO prefab = ScriptableObject.CreateInstance<EntityPrefabSO>();
        prefab.Configure(key, health, moveSpeed, view);
        return prefab;
    }

    /// <summary>创建一个只有 Health 的 PrefabSO。</summary>
    private EntityPrefabSO CreateHealthOnlyPrefab(string key)
    {
        HealthComponentPresetSO health = ScriptableObject.CreateInstance<HealthComponentPresetSO>();
        health.Configure(100, 100);

        EntityPrefabSO prefab = ScriptableObject.CreateInstance<EntityPrefabSO>();
        prefab.Configure(key, health);
        return prefab;
    }

    /// <summary>创建 Scout DefinitionSO。</summary>
    private GameplayEntityDefinitionSO CreateScoutDefinition(EntityPrefabSO prefab)
    {
        GameplayComponentConfigSet components = new GameplayComponentConfigSet();
        components.health.enabled = true;
        components.health.current = 80;
        components.health.max = 80;
        components.moveSpeed.enabled = true;
        components.moveSpeed.value = 8f;
        components.viewRequest.enabled = true;
        components.viewRequest.prefabID = 1001;
        components.position.enabled = true;
        components.position.useCreateContextPosition = true;

        GameplayEntityDefinitionSO definition = ScriptableObject.CreateInstance<GameplayEntityDefinitionSO>();
        definition.Configure("unit_scout", "Scout", prefab, in components);
        return definition;
    }

    /// <summary>创建只启用 Stat 的 DefinitionSO。</summary>
    private GameplayEntityDefinitionSO CreateStatOnlyDefinition(EntityPrefabSO prefab)
    {
        GameplayComponentConfigSet components = new GameplayComponentConfigSet();
        components.stat.enabled = true;
        components.stat.attack = 10;
        components.stat.defense = 2;
        components.stat.moveSpeed = 1;

        GameplayEntityDefinitionSO definition = ScriptableObject.CreateInstance<GameplayEntityDefinitionSO>();
        definition.Configure("stat_only", "Stat Only", prefab, in components);
        return definition;
    }

    /// <summary>简单断言工具。</summary>
    private void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[ECSGameplayEntityFactorySOTestBootstrap] Test failed: {message}");
    }
}

}
