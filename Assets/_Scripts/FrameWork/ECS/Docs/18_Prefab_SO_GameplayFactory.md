# PrefabSO + DefinitionSO + GameplayEntityFactory 逻辑链说明

本文说明当前 ECSFrameWork 中 Unity Authoring 层如何通过 `EntityPrefabSO`、`ComponentPresetSO`、`GameplayEntityDefinitionSO` 和 `GameplayEntityFactory` 创建 ECS `Entity`。

## 1. 设计结论

当前实现采用下面这条创建链路：

```text
GameplayEntityDefinitionSO
    ↓ 引用 BasePrefab
EntityPrefabSO
    ↓ 保存默认组件预设
ComponentPresetSO[]
    ↓ 写入默认 ECS Component
GameplayComponentConfigSet
    ↓ 覆盖 / 扩展业务组件
EntityCreateContext
    ↓ 提供运行时参数
overrideBuilder
    ↓ 最终手动覆盖
World.SetComponent()
    ↓ 写入 ComponentStore<T> 并同步 ArcheType
```

组件写入优先级固定为：

```text
1. EntityPrefabSO 默认组件
2. GameplayEntityDefinitionSO 业务配置
3. EntityCreateContext 运行时参数
4. overrideBuilder 最终覆盖
```

越靠后，优先级越高。

## 2. 各类职责

| 类型 | 职责 | 典型使用者 |
|---|---|---|
| `ComponentPresetSO` | 单个组件的 Unity 预设基类 | 配置人员、Authoring 层 |
| `HealthComponentPresetSO` 等 | 把 Inspector 字段转换成具体 ECS Component | PrefabSO |
| `EntityPrefabSO` | 保存一组 `ComponentPresetSO`，提供 Entity 默认组件模板 | 通用业务工厂 |
| `GameplayEntityDefinitionSO` | 保存业务实体配置，引用一个 `BasePrefab`，并保存组件覆盖值 | 游戏业务配置 |
| `GameplayComponentConfigSet` | 将同一组件需要的字段打包，并按 `enabled` 写入组件 | DefinitionSO |
| `EntityCreateContext` | 保存出生位置、初始速度、所属者、目标等运行时参数 | 生成单位、子弹、建筑时传入 |
| `GameplayEntityFactory` | 统一把 DefinitionSO 转换为 ECS Entity | 外部业务创建入口 |
| `EntityFactory` | 底层通用 Prefab 创建器 | GameplayEntityFactory 内部使用 |
| `EntityBuilder` | 链式写入组件，保证所有写入仍走 `World.SetComponent` | Factory 和外部覆盖逻辑 |

## 3. `ComponentPresetSO` 体系

`ComponentPresetSO` 是 Unity Authoring 层的组件预设基类：

```csharp
public abstract class ComponentPresetSO : ScriptableObject
{
    public abstract Type ComponentType { get; }
    public abstract void Apply(World world, Entity entity);
    public virtual void Validate(EntityDefinitionValidationResult result, string ownerName);
}
```

当前已提供的预设类型：

| 预设类型 | 写入组件 |
|---|---|
| `HealthComponentPresetSO` | `HealthComponent` |
| `MoveSpeedComponentPresetSO` | `MoveSpeedComponent` |
| `StatComponentPresetSO` | `StatComponent` |
| `PositionComponentPresetSO` | `PositionComponent` |
| `VelocityComponentPresetSO` | `VelocityComponent` |
| `PrefabViewRequestComponentPresetSO` | `PrefabViewRequestComponent` |
| `PlayerTagComponentPresetSO` | `PlayerTagComponent` |

创建时，`EntityPrefabSO` 会遍历 `ComponentPresetSO[]`：

```csharp
for (int i = 0; i < componentPresets.Length; i++)
{
    ComponentPresetSO preset = componentPresets[i];
    if (preset == null) continue;
    preset.Apply(world, entity);
}
```

每个 Preset 内部最终调用：

```csharp
world.SetComponent(entity, component);
```

因此它不会绕过 `World` 的生命周期和 ArcheType 同步规则。

## 4. `EntityPrefabSO`

`EntityPrefabSO` 是 Unity 版实体模板。

核心接口：

```csharp
public sealed class EntityPrefabSO : ScriptableObject, IEntityPrefab, IEntityPrefabComponentInfo
{
    public string Key { get; }
    public string Name { get; }
    public int ComponentCount { get; }
    public IReadOnlyList<ComponentPresetSO> ComponentPresets { get; }

    public void Configure(string key, params ComponentPresetSO[] presets);
    public Entity Create(World world);
    public void ApplyTo(World world, Entity entity);
    public bool HasComponent(Type componentType);
    public int FillComponentTypes(List<Type> results);
    public void Validate(EntityDefinitionValidationResult result);
}
```

它的职责是：

```text
1. 创建一个新 Entity
2. 遍历 ComponentPresetSO[]
3. 写入默认组件
4. 为 DefinitionSO 提供组件类型查询能力
```

注意：`EntityPrefabSO` 不负责创建 Unity `GameObject`。如果需要表现对象，应通过 `PrefabViewRequestComponent` 交给 `ViewSpawnSystem` 和 `ViewManager`。

## 5. `GameplayEntityDefinitionSO`

`GameplayEntityDefinitionSO` 是通用业务实体配置：

```csharp
public class GameplayEntityDefinitionSO : ScriptableObject
{
    public string DefinitionID { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }
    public EntityPrefabSO BasePrefab { get; }
    public GameplayComponentConfigSet Components { get; }

    public void Configure(string definitionID, string displayName, EntityPrefabSO basePrefab, in GameplayComponentConfigSet components);
    public EntityDefinitionValidationResult ValidateDefinition(EntityDefinitionMismatchPolicy mismatchPolicy = EntityDefinitionMismatchPolicy.WarnAndAdd);
}
```

它不直接保存 `Component`，而是保存“生成组件所需的数据”。例如：

```csharp
components.health.enabled = true;
components.health.current = 80;
components.health.max = 80;

components.moveSpeed.enabled = true;
components.moveSpeed.value = 8f;

components.position.enabled = true;
components.position.useCreateContextPosition = true;
```

## 6. `GameplayComponentConfigSet`

`GameplayComponentConfigSet` 把常用组件配置集中在一个结构中：

```csharp
public struct GameplayComponentConfigSet
{
    public PositionComponentConfig position;
    public VelocityComponentConfig velocity;
    public HealthComponentConfig health;
    public MoveSpeedComponentConfig moveSpeed;
    public StatComponentConfig stat;
    public PrefabViewRequestComponentConfig viewRequest;
    public PlayerTagComponentConfig playerTag;

    public void Apply(EntityBuilder builder, in EntityCreateContext context);
    public void Validate(EntityDefinitionValidationResult result, string ownerName);
    public int FillEnabledComponentTypes(List<Type> results);
}
```

每个配置结构都遵守同一个规则：

```text
enabled = false：不覆盖 PrefabSO 中已有组件
enabled = true：写入或覆盖对应组件
```

所以 `enabled = false` 不等于“删除组件”。如果某类实体不应该有某个组件，应该换一个不包含该组件的 `BasePrefab`。

## 7. `EntityCreateContext`

`EntityCreateContext` 保存运行时参数：

```csharp
public struct EntityCreateContext
{
    public Vector3 position;
    public Vector3 velocity;
    public int ownerID;
    public int campID;
    public Entity sourceEntity;
    public Entity targetEntity;
}
```

它解决的问题是：

```text
SO 负责静态配置；
Context 负责每次创建时才确定的动态参数。
```

例如同一个 `Def_Unit_Scout` 可以被创建在不同位置：

```csharp
EntityCreateContext context = EntityCreateContext.Default;
context.position = new Vector3(10, 0, 5);

Entity scout = gameplayFactory.Create(scoutDefinition, in context);
```

## 8. `GameplayEntityFactory`

`GameplayEntityFactory` 是推荐的业务实体创建入口。

```csharp
public sealed class GameplayEntityFactory
{
    public EntityDefinitionMismatchPolicy MismatchPolicy { get; set; }
    public bool LogWarnings { get; set; }
    public World World { get; }

    public GameplayEntityFactory(EntityFactory entityFactory);

    public Entity Create(GameplayEntityDefinitionSO definition);
    public Entity Create(GameplayEntityDefinitionSO definition, in EntityCreateContext context);
    public Entity Create(GameplayEntityDefinitionSO definition, in EntityCreateContext context, Action<EntityBuilder> overrideBuilder);
    public bool TryCreate(GameplayEntityDefinitionSO definition, in EntityCreateContext context, out Entity entity);
    public bool TryCreate(GameplayEntityDefinitionSO definition, in EntityCreateContext context, Action<EntityBuilder> overrideBuilder, out Entity entity);
}
```

推荐用法：

```csharp
World world = new World();
EntityFactory entityFactory = new EntityFactory(world);
GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory);

EntityCreateContext context = EntityCreateContext.Default;
context.position = spawnPosition;

Entity unit = gameplayFactory.Create(unitDefinition, in context, builder =>
{
    builder.With(new HealthComponent(1, 80));
});
```

## 9. 不匹配校验策略

`DefinitionSO` 启用的组件不一定必须已经存在于 `BasePrefab` 中。当前提供三种策略：

```csharp
public enum EntityDefinitionMismatchPolicy
{
    AllowAdd,
    WarnAndAdd,
    Reject,
}
```

| 策略 | 行为 |
|---|---|
| `AllowAdd` | 允许 `DefinitionSO` 添加 `BasePrefab` 中没有的组件，不输出警告 |
| `WarnAndAdd` | 允许添加，但输出 Warning，默认推荐 |
| `Reject` | 不允许添加，创建失败并返回 `Entity.Invalid` |

推荐默认使用 `WarnAndAdd`。这样既能支持“基础模板 + 业务扩展”，又能在配置不符合预期时提醒开发者。

## 10. 创建顺序示例

假设：

```text
EntityPrefabSO: UnitBase
    HealthPreset: 100 / 100
    MoveSpeedPreset: 5
    ViewPreset: prefabID = 1000

GameplayEntityDefinitionSO: Scout
    BasePrefab = UnitBase
    health.enabled = true, current = 80, max = 80
    moveSpeed.enabled = true, value = 8
    viewRequest.enabled = true, prefabID = 1001
    position.enabled = true, useCreateContextPosition = true
```

调用：

```csharp
EntityCreateContext context = EntityCreateContext.Default;
context.position = new Vector3(10, 0, 5);

Entity scout = gameplayFactory.Create(scoutDefinition, in context, builder =>
{
    builder.With(new HealthComponent(1, 80));
});
```

最终结果：

```text
PrefabSO 默认写入：
    Health = 100 / 100
    MoveSpeed = 5
    ViewRequest = 1000

DefinitionSO 覆盖：
    Health = 80 / 80
    MoveSpeed = 8
    ViewRequest = 1001
    Position = context.position

OverrideBuilder 最终覆盖：
    Health = 1 / 80
```

最终 Entity 拥有：

```text
HealthComponent(current = 1, max = 80)
MoveSpeedComponent(value = 8)
PrefabViewRequestComponent(prefabID = 1001)
PositionComponent(10, 0, 5)
```

## 11. 测试脚本

新增测试脚本：

```text
ECS/Test/ECSGameplayEntityFactorySOTestBootstrap.cs
```

测试内容：

```text
1. DefinitionSO 覆盖 PrefabSO 默认组件
2. EntityCreateContext 写入运行时位置
3. overrideBuilder 拥有最高优先级
4. WarnAndAdd 允许 DefinitionSO 添加 PrefabSO 中没有的组件
5. Reject 阻止组件不匹配创建
6. PrefabSO 重复组件时后者覆盖前者
7. EntityFactory 可以直接接收 IEntityPrefab 创建 Entity
```

使用方式：

```text
1. 在 Unity 场景中新建空 GameObject
2. 挂载 ECSGameplayEntityFactorySOTestBootstrap
3. 运行场景
4. Console 中出现 All tests passed 即通过
```
