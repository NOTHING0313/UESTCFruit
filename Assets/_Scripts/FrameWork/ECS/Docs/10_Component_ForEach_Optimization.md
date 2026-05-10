# 10. Component ForEach 优化说明

## 目标

这一阶段的优化目标是降低高频 System 的组件访问成本。

旧写法通常是：

```csharp
World.FillQuery(query, entities, false);

for (int i = 0; i < entities.Count; i++)
{
    EntityInfo entity = entities[i];
    ref PositionComponent position = ref World.GetComponent<PositionComponent>(entity);
    ref VelocityComponent velocity = ref World.GetComponent<VelocityComponent>(entity);
}
```

这条路径会产生几类成本：

1. 每帧填充查询结果 List。
2. 每个 Entity 对每种组件执行一次 sparse 查找。
3. System 需要维护临时结果容器。

新写法使用：

```csharp
World.ForEach<PositionComponent>(callback);
World.ForEach<PositionComponent, VelocityComponent>(callback);
World.ForEach<PlayerInputComponent, MoveSpeedComponent, VelocityComponent>(callback);
```

它会直接遍历组件数量更少的 ComponentStore，并通过 sparse 映射定位其它组件。

## 新增 API

### ComponentStore<T>

```csharp
public bool TryGetDenseIndex(EntityInfo entity, out int denseIndex)
public EntityInfo GetEntityByDenseIndex(int denseIndex)
public ref T GetComponentByDenseIndex(int denseIndex)
```

这些方法暴露 dense 数组访问能力，但仍然通过 EntityInfo 的 ID 和 Version 保证组件对应关系正确。

### ComponentManager

```csharp
public int ForEach<T>(EntityComponentAction<T> action) where T : struct, IComponentData
public int ForEach<T1, T2>(EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData
public int ForEach<T1, T2, T3>(EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
```

ComponentManager 会选择 Count 最小的 Store 作为主遍历源，从而减少无效实体检查。

### World

```csharp
public int ForEach<T>(EntityComponentAction<T> action) where T : struct, IComponentData
public int ForEach<T1, T2>(EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData
public int ForEach<T1, T2, T3>(EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
```

外部 System 应该通过 World 入口调用，不直接依赖 ComponentManager。

## 已改写系统

当前补齐了单组件 ForEach<T>，适合 Lifetime、Cooldown、HealthRegen 等只需要遍历一种组件的高频系统。

当前已改写：

1. MovementSystem：使用 `ForEach<PositionComponent, VelocityComponent>`。
2. InputMoveSystem：使用 `ForEach<PlayerInputComponent, MoveSpeedComponent, VelocityComponent>`。
3. ViewSyncSystem：使用 `ForEach<PositionComponent, ViewComponent>`。

这些系统都不依赖确定性排序，因此不再需要 `FillQuery(..., false)` 和临时 Entity List。

## 使用约束

ForEach 回调中推荐只修改传入的组件 ref。

不建议在回调中立即执行会改变当前遍历 Store 的结构操作，例如：

```csharp
World.RemoveComponent<PositionComponent>(entity);
World.DestroyEntity(entity);
World.SetComponent(entity, new SomeNewComponent());
```

在 Tick 阶段，这些结构变化会进入 StructuralChangeBuffer，通常是安全的；但在 Initialization 阶段直接调用 ForEach 并立即修改结构，可能导致当前遍历 Store 被改变。

如果 System 需要结构修改，应继续通过 World API 发起请求，让 World 生命周期和 Buffer 处理。

## 适用场景

适合：

1. LifetimeSystem / CooldownSystem 这类单组件高频状态更新。
2. MovementSystem 这类高频、无排序、组件原地修改逻辑。
3. InputMoveSystem 这类多组件读取后写入组件的逻辑。
4. ViewSyncSystem 这类表现层同步逻辑。

不适合：

1. 需要稳定排序的结算系统。
2. 需要复杂 include/exclude Query 的系统。
3. 回调中会大量改变 Entity 结构的系统。

需要排序或复杂过滤时，仍然使用 QueryDescription + FillQuery。
