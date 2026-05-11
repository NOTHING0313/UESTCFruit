# EntityBuilder 使用说明

`EntityBuilder` 是 ECSFrameWork 提供的链式 Entity 创建工具，用于把“创建 Entity + 设置初始组件”的流程集中到一个入口中。

它只是一层便利封装，不会绕过 `World`，也不会直接访问 `EntityManager`、`ComponentManager` 或 `ComponentStore<T>`。所有组件写入仍然通过 `World.SetComponent` 完成，因此会继续遵守 World 生命周期、StructuralChangeBuffer、ArcheType 更新和 QueryCache 刷新规则。

## 1. 推荐使用场景

适合用于：

1. 初始化阶段批量创建实体。
2. MonoBehaviour / Adapter 层创建 ECS 实体。
3. 测试脚本快速构造实体。
4. 后续 EntityPrefab / EntityFactory 的底层创建入口。

不建议把它当作高频 Tick 内的复杂事务系统。Tick 中需要大量延迟生成时，后续应优先走按帧命令或专用 Spawn Command。

## 2. 基础用法

```csharp
World world = new World();

Entity entity = world.CreateEntityBuilder()
    .With(new PositionComponent(0, 0, 0))
    .With(new VelocityComponent(1, 0, 0))
    .With(new HealthComponent(100, 100))
    .Build();
```

等价于：

```csharp
Entity entity = world.CreateEntity();
world.SetComponent(entity, new PositionComponent(0, 0, 0));
world.SetComponent(entity, new VelocityComponent(1, 0, 0));
world.SetComponent(entity, new HealthComponent(100, 100));
```

## 3. 委托配置入口

`World.BuildEntity` 适合把创建逻辑封装在一段配置委托里：

```csharp
Entity entity = world.BuildEntity(builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
    builder.With(new HealthComponent(80, 100));
});
```

`configure` 为 `null` 时，该方法只创建并返回一个 Entity。

## 4. Build 规则

`Build()` 只返回当前 Builder 已经创建的 Entity，不会重复创建。

```csharp
EntityBuilder builder = world.CreateEntityBuilder();
Entity a = builder.Build();
Entity b = builder.Build();

// a == b
```

`Build()` 后继续调用 `With<T>()` 仍然会作用于同一个 Entity：

```csharp
EntityBuilder builder = world.CreateEntityBuilder();
Entity entity = builder.Build();

builder.With(new PositionComponent(1, 2, 3));
```

这样设计是为了让 Builder 保持轻量，不引入额外事务状态。

## 5. World 释放时的行为

当 `World` 已经处于 `Disposing` 状态时：

```csharp
Entity entity = world.CreateEntityBuilder()
    .With(new PositionComponent(1, 1, 1))
    .Build();
```

会返回：

```csharp
Entity.Invalid
```

`With<T>()` 会被安全忽略。

## 6. 和后续 EntityPrefab / EntityFactory 的关系

`EntityBuilder` 是第一层能力，主要解决单个实体的链式创建。

后续可以在它之上继续实现：

1. `EntityPrefab`：保存可复用组件模板。
2. `EntityFactory`：注册多个 Prefab，并按 key 创建实体。
3. Unity Authoring Adapter：把 ScriptableObject 配置转换成 Builder / Prefab 创建流程。

推荐分层：

```text
EntityBuilder
    ↓
EntityPrefab
    ↓
EntityFactory
    ↓
Unity ScriptableObject Authoring
```
