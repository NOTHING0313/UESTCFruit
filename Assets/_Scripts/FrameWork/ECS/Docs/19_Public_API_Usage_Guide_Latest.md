# ECSFrameWork 对外接口与调用方式完整说明

命名空间：

```csharp
using ECSFrameWork;
```

本文按实际业务使用顺序梳理当前 ECSFrameWork 的公开入口。外部业务应尽量只依赖这些 API，不要直接访问内部 Manager、Store 或 Buffer。

## 1. World：统一入口

`World` 是 ECS Core 的入口，负责 Entity、Component、Query、System、Singleton、事件和统计。

常用创建方式：

```csharp
World world = new World();
```

常用属性：

```csharp
world.CurrentState;
world.AliveEntityCount;
world.CreatedEntityCount;
world.ComponentStoreCount;
world.ArcheTypeCount;
world.SystemCount;
world.WorldEventCount;
world.SingletonCount;
```

推进逻辑帧：

```csharp
SimulationContext context = new SimulationContext(frameNumber, tickLength, false);
world.Tick(in context);
```

释放：

```csharp
world.Dispose();
```

## 2. Entity：实体句柄

`Entity` 是 `ID + Version` 的轻量句柄。

```csharp
Entity entity = world.CreateEntity();

if (entity.IsValid && world.IsAlive(entity))
{
    world.DestroyEntity(entity);
}
```

注意：不要只保存 `Entity.ID`。实体 ID 会复用，长期引用必须保存完整 `Entity` 并通过 `world.IsAlive(entity)` 校验。

## 3. Component：数据组件

业务组件必须是结构体，并实现 `IComponentData`：

```csharp
public struct EnergyComponent : IComponentData
{
    public int value;
}
```

添加或覆盖组件：

```csharp
world.SetComponent(entity, new HealthComponent(100, 100));
```

读取组件：

```csharp
if (world.TryGetComponent(entity, out HealthComponent health))
{
    Debug.Log(health.current);
}
```

需要原地修改时使用 `ref`：

```csharp
ref HealthComponent health = ref world.GetComponent<HealthComponent>(entity);
health.current -= 10;
```

判断和移除：

```csharp
bool hasHealth = world.HasComponent<HealthComponent>(entity);
bool removed = world.RemoveComponent<HealthComponent>(entity);
```

## 4. EntityBuilder：链式创建

推荐在初始化实体时使用：

```csharp
Entity entity = world.CreateEntityBuilder()
    .With(new PositionComponent(0, 0, 0))
    .With(new VelocityComponent(1, 0, 0))
    .With(new HealthComponent(100, 100))
    .Build();
```

也可以使用委托入口：

```csharp
Entity entity = world.BuildEntity(builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
    builder.With(new HealthComponent(80, 100));
});
```

## 5. EntityPrefab / EntityFactory：纯 C# 模板

如果不需要 Unity SO，可以直接使用纯 C# Prefab：

```csharp
EntityPrefab unitPrefab = new EntityPrefab("Unit")
    .With(new HealthComponent(100, 100))
    .With(new MoveSpeedComponent(5))
    .With(new PrefabViewRequestComponent(1000));

EntityFactory factory = new EntityFactory(world);
factory.RegisterPrefab("unit", unitPrefab);

Entity unit = factory.Create("unit", builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
});
```

也可以直接传入实现了 `IEntityPrefab` 的对象：

```csharp
Entity unit = factory.Create(unitPrefab);
```

## 6. EntityPrefabSO / ComponentPresetSO：Unity 配置模板

`EntityPrefabSO` 用于在 Inspector 中组合多个 `ComponentPresetSO`。

当前内置组件预设：

```text
HealthComponentPresetSO
MoveSpeedComponentPresetSO
StatComponentPresetSO
PositionComponentPresetSO
VelocityComponentPresetSO
PrefabViewRequestComponentPresetSO
PlayerTagComponentPresetSO
```

配置方式：

```text
1. 创建 ComponentPresetSO 资源，例如 HealthComponentPresetSO
2. 创建 EntityPrefabSO 资源
3. 在 EntityPrefabSO 中填入 ComponentPresetSO 列表
4. 业务创建时由 EntityFactory 或 GameplayEntityFactory 使用
```

运行时直接创建：

```csharp
Entity entity = entityFactory.Create(entityPrefabSO, builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
});
```

## 7. GameplayEntityDefinitionSO / GameplayEntityFactory：推荐业务创建入口

推荐业务层使用 `GameplayEntityFactory`，而不是为每种单位、建筑、子弹单独写一个工厂。

```csharp
World world = new World();
EntityFactory entityFactory = new EntityFactory(world);
GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory);
```

创建上下文：

```csharp
EntityCreateContext context = EntityCreateContext.Default;
context.position = spawnPosition;
context.velocity = startVelocity;
context.ownerID = playerID;
context.campID = campID;
```

创建实体：

```csharp
Entity entity = gameplayFactory.Create(definitionSO, in context);
```

最终覆盖：

```csharp
Entity entity = gameplayFactory.Create(definitionSO, in context, builder =>
{
    builder.With(new HealthComponent(1, 80));
});
```

组件优先级：

```text
EntityPrefabSO 默认组件
    ↓
GameplayEntityDefinitionSO 业务覆盖
    ↓
EntityCreateContext 运行时参数
    ↓
overrideBuilder 最终覆盖
```

不匹配策略：

```csharp
gameplayFactory.MismatchPolicy = EntityDefinitionMismatchPolicy.WarnAndAdd;
```

| 策略 | 行为 |
|---|---|
| `AllowAdd` | DefinitionSO 可以添加 BasePrefab 中不存在的组件，不报警 |
| `WarnAndAdd` | 允许添加并输出 Warning，默认推荐 |
| `Reject` | 不允许添加，创建失败并返回 `Entity.Invalid` |

## 8. Query：查询实体

链式查询：

```csharp
List<Entity> results = new List<Entity>();

world.Query()
    .With<PositionComponent>()
    .With<VelocityComponent>()
    .Fill(results);
```

排序查询：

```csharp
world.Query()
    .With<HealthComponent>()
    .Fill(results, sorted: true);
```

高频遍历推荐 `ForEach`：

```csharp
world.ForEach<PositionComponent, VelocityComponent>((Entity entity, ref PositionComponent position, ref VelocityComponent velocity) =>
{
    position.x += velocity.x;
    position.y += velocity.y;
    position.z += velocity.z;
});
```

`ForEach` 不创建 Query 结果 List，适合高频系统。

## 9. System：逻辑帧系统

推荐继承 `FixedStepSystemBase`：

```csharp
public sealed class MovementSystem : FixedStepSystemBase
{
    public override int Sequence => 100;

    public override void Tick(World world, in SimulationContext context)
    {
        world.ForEach<PositionComponent, VelocityComponent>((Entity entity, ref PositionComponent position, ref VelocityComponent velocity) =>
        {
            position.x += velocity.x * context.tickLength;
            position.y += velocity.y * context.tickLength;
            position.z += velocity.z * context.tickLength;
        });
    }
}
```

注册：

```csharp
world.AddSystem(new MovementSystem());
```

移除：

```csharp
world.RemoveSystem(system);
```

`World.Tick` 中增删组件、销毁 Entity 会进入结构变更缓冲，并在逻辑帧末统一播放。

## 10. Singleton Component

适合保存全局配置、全局状态、逻辑时间等单例数据。

```csharp
world.SetSingleton(new GameConfigComponent(...));

if (world.TryGetSingleton(out GameConfigComponent config))
{
    // read config
}

ref GameConfigComponent configRef = ref world.GetSingleton<GameConfigComponent>();
configRef.someValue = 10;

world.RemoveSingleton<GameConfigComponent>();
```

## 11. WorldEvent：逻辑事件输出

System 中写入一次性事件：

```csharp
world.AddWorldEvent(new DamageWorldEvent(frameNumber, source, target, amount));
```

表现层读取：

```csharp
IReadOnlyList<DamageWorldEvent> events = world.GetWorldEvents<DamageWorldEvent>();
```

消费后清理：

```csharp
world.ClearWorldEvents();
```

## 12. 输入与按帧命令

输入采样通过 `InputSnapshotBuffer` 按帧记录，再通过 `WorldInputApplier` 写入 ECS。

外部 UI、网络、剧情等结构修改可以使用 `SimulationFrameCommandScheduler` 按帧调度。

```csharp
scheduler.ScheduleSetComponent(frameNumber, entity, new HealthComponent(100, 100));
scheduler.ApplyCommandsToWorld(frameNumber, world);
```

当前阶段暂不展开回滚实现，但这些接口已经为后续帧同步和回滚保留了按帧消费边界。

## 13. Unity View 接入

逻辑实体不直接持有 `GameObject`。表现对象建议通过：

```text
PrefabViewRequestComponent
ViewSpawnSystem
ViewManager
ViewComponent
ViewSyncSystem
ViewDestroySystem
```

典型链路：

```text
Entity 写入 PrefabViewRequestComponent
    ↓
ViewSpawnSystem 创建或从池中获取 GameObject
    ↓
ViewManager 分配 viewID
    ↓
Entity 写入 ViewComponent(viewID)
    ↓
ViewSyncSystem 根据 PositionComponent 同步 Transform
```

## 14. 统计与性能观测

World 统计：

```csharp
WorldStatistics statistics = world.GetStatistics();
```

System Profile：

```csharp
world.EnableSystemProfile = true;
List<SystemProfileInfo> profiles = world.GetSystemProfiles();
world.ResetSystemProfiles();
```

## 15. 测试入口

当前主要测试脚本位于：

```text
ECS/Test
```

本次新增：

```text
ECSGameplayEntityFactorySOTestBootstrap
```

它验证：

```text
PrefabSO 默认组件
DefinitionSO 覆盖组件
CreateContext 写入运行时参数
overrideBuilder 最终覆盖
WarnAndAdd / Reject 不匹配策略
重复 ComponentPresetSO 覆盖规则
EntityFactory 直接接收 IEntityPrefab
```

## 16. 推荐实践

1. 业务代码优先依赖 `World`、`Entity`、`EntityFactory`、`GameplayEntityFactory`。
2. 不要直接访问 `EntityManager`、`ComponentManager`、`ComponentStore<T>`。
3. 组件保持纯数据，逻辑放到 System 中。
4. `DefinitionSO.enabled = false` 只表示“不覆盖”，不表示删除 Prefab 中已有组件。
5. 如果需要没有某组件的实体，应创建另一个不含该组件的 `EntityPrefabSO`。
6. `overrideBuilder` 只用于少量最终覆盖，不要把大量业务规则塞进委托。
7. 表现对象通过 View 系统接入，不要让 Unity `Transform` 成为逻辑真值。
