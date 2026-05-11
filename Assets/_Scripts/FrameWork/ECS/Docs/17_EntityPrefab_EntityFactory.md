# EntityPrefab 与 EntityFactory

本篇说明 ECSFrameWork 中 `EntityPrefab` 与 `EntityFactory` 的设计目的、使用方式和边界。它们用于解决实体创建代码分散、重复、难维护的问题。

## 1. 三层职责

| 类型 | 职责 |
|---|---|
| `EntityBuilder` | 链式创建或配置一个 Entity，集中写入多个组件 |
| `EntityPrefab` | 保存一组默认组件模板，描述“这种实体默认拥有哪些组件” |
| `EntityFactory` | 管理多个 Prefab，并通过 key 统一创建 Entity |

它们都只通过 `World` 的公开 API 操作组件，不直接访问 `EntityManager`、`ComponentManager`、`ComponentStore<T>` 或 `ArcheTypeManager`。

## 2. EntityPrefab

`EntityPrefab` 是 ECS 层实体模板，不是 Unity 的 `GameObject Prefab`。它只描述组件组合。

```csharp
EntityPrefab unitPrefab = new EntityPrefab("Unit")
    .With(new HealthComponent(100, 100))
    .With(new MoveSpeedComponent(5))
    .With(new PrefabViewRequestComponent(unitPrefabID));
```

创建实体：

```csharp
Entity unit = unitPrefab.Create(world);
```

应用到已有实体：

```csharp
Entity entity = world.CreateEntity();
unitPrefab.ApplyTo(world, entity);
```

### 2.1 重复组件规则

同一个 `EntityPrefab` 中，同类型组件后写覆盖前写：

```csharp
EntityPrefab prefab = new EntityPrefab("Test")
    .With(new HealthComponent(100, 100))
    .With(new HealthComponent(200, 200));
```

最终创建出的实体只会得到后一份 `HealthComponent(200, 200)`。

### 2.2 常用接口

```csharp
public sealed class EntityPrefab : IEntityPrefab
{
    public string Name { get; }
    public int ComponentCount { get; }

    public EntityPrefab(string name);

    public EntityPrefab With<T>(in T component) where T : struct, IComponentData;
    public bool Has<T>() where T : struct, IComponentData;
    public bool Remove<T>() where T : struct, IComponentData;
    public void Clear();
    public int FillComponentTypes(List<Type> results);

    public Entity Create(World world);
    public void ApplyTo(World world, Entity entity);
}
```

## 3. EntityFactory

`EntityFactory` 用于注册和创建多个 `EntityPrefab`。

```csharp
EntityFactory factory = new EntityFactory(world);

factory.RegisterPrefab("Unit", unitPrefab);
factory.RegisterPrefab("Bullet", bulletPrefab);
```

创建实体：

```csharp
Entity unit = factory.Create("Unit");
```

安全创建：

```csharp
if (factory.TryCreate("Bullet", out Entity bullet))
{
    // 创建成功
}
```

## 4. 运行时覆盖

Prefab 只描述默认值，但实际生成实体时通常需要运行时参数，例如位置、方向、阵营、来源 Entity。

可以通过 `overrideBuilder` 在 Prefab 应用后覆盖组件：

```csharp
Entity bullet = factory.Create("Bullet", builder =>
{
    builder.With(new PositionComponent(spawnPosition.x, spawnPosition.y, spawnPosition.z));
    builder.With(new VelocityComponent(direction.x * speed, direction.y * speed, direction.z * speed));
});
```

执行顺序是：

```text
Factory.Create(key, overrideBuilder)
    ↓
找到 IEntityPrefab
    ↓
Prefab.Create(world)
    ↓
写入默认组件
    ↓
overrideBuilder 写入运行时组件
    ↓
返回 Entity
```

所以运行时覆盖会覆盖 Prefab 中的同类型默认组件。

## 5. 注册规则

```csharp
public bool RegisterPrefab(string key, IEntityPrefab prefab);
public bool SetPrefab(string key, IEntityPrefab prefab);
```

二者区别：

| 方法 | 行为 |
|---|---|
| `RegisterPrefab` | key 已存在时返回 `false`，不会覆盖 |
| `SetPrefab` | key 已存在时覆盖，不存在时新增 |

推荐项目初始化阶段使用 `RegisterPrefab`，需要热更新或测试覆盖时使用 `SetPrefab`。

## 6. 和 ViewManager 的关系

`EntityPrefab` 不直接生成 `GameObject`。如果实体需要表现对象，应在 Prefab 中添加：

```csharp
.With(new PrefabViewRequestComponent(prefabID))
```

随后由 `ViewSpawnSystem` 在逻辑帧中消费请求并调用 `ViewManager` 创建表现对象。

这样逻辑层和表现层保持解耦。

## 7. 和 SingletonComponent 的关系

`EntityPrefab` 不应该用于创建 Singleton。Singleton 表示全局唯一状态，应继续使用：

```csharp
world.SetSingleton(new GameTimeComponent(...));
```

Prefab 更适合普通实体：单位、子弹、建筑、掉落物、区域触发器等。

## 8. 和 StructuralChangeBuffer 的关系

`EntityPrefab` / `EntityFactory` 都通过 `World.SetComponent` 写入组件，因此会自动遵守 World 当前阶段规则。

如果在 `World.Tick()` 中创建实体或新增组件，结构变化是否立即执行仍由 `World` 判断，不由 Prefab / Factory 自己决定。

## 9. 推荐使用场景

适合：

```text
单位创建
子弹创建
建筑创建
掉落物创建
一次性触发器创建
测试样例中快速构建实体
```

不适合：

```text
高频每帧组件遍历
全局唯一状态
直接管理 GameObject 生命周期
回滚快照恢复
```

## 10. 推荐创建流程

```csharp
EntityPrefab unitPrefab = new EntityPrefab("Unit")
    .With(new HealthComponent(100, 100))
    .With(new MoveSpeedComponent(5))
    .With(new PrefabViewRequestComponent(unitPrefabID));

EntityFactory factory = new EntityFactory(world);
factory.RegisterPrefab("Unit", unitPrefab);

Entity unit = factory.Create("Unit", builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
});
```

这套流程把默认配置和运行时差异分开：

```text
EntityPrefab：默认组件
EntityFactory：统一创建入口
overrideBuilder：运行时参数覆盖
```
