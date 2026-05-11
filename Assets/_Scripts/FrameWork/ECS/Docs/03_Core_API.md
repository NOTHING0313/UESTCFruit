# 03. ECS Core API 速查

## 1. 创建 World

```csharp
World world = new World();
```

`World` 构造时会初始化：

- `EntityManager`
- `ComponentManager`
- `ArcheTypeManager`
- `SystemManager`
- `ComponentTypeRegistry`
- `StructuralChangeBuffer`

## 2. Entity API

```csharp
Entity entity = world.CreateEntity();
bool alive = world.IsAlive(entity);
world.DestroyEntity(entity);
```

注意：

- `DestroyEntity` 在 `Ticking` 阶段会进入 `StructuralChangeBuffer`。
- Entity 销毁时会先移除所有组件，再回收 Entity ID。
- 复用 ID 时会刷新 Version，旧句柄不会误操作新 Entity。

## 3. Component API

```csharp
world.SetComponent(entity, new PositionComponent(0f, 0f, 0f));
world.SetComponent(entity, new HealthComponent(100, 100));

bool hasPosition = world.HasComponent<PositionComponent>(entity);
ref PositionComponent position = ref world.GetComponent<PositionComponent>(entity);

if (world.TryGetComponent(entity, out HealthComponent health))
{
    // safe read
}

world.RemoveComponent<HealthComponent>(entity);
```

约定：

- 组件必须实现 `IComponentData`。
- 当前实现建议组件使用 `struct`。
- `GetComponent<T>` 返回 ref，调用前应确认 Entity 存活且拥有该组件。
- `TryGetComponent<T>` 适合外部模块或只读访问场景。

## 4. Query API

```csharp
foreach (Entity entity in world.Query().With<PositionComponent>().With<VelocityComponent>().ExecuteSorted())
{
    ref PositionComponent position = ref world.GetComponent<PositionComponent>(entity);
    ref VelocityComponent velocity = ref world.GetComponent<VelocityComponent>(entity);
}
```

推荐规则：

- 逻辑结果依赖遍历顺序时，使用 `ExecuteSorted()`。
- `Execute()` 更轻量，但不应假设 `Dictionary` 遍历顺序稳定。
- `Without<T>()` 可用于排除指定组件，例如排除死亡标记。

```csharp
world.Query()
    .With<HealthComponent>()
    .Without<DeadTagComponent>()
    .ExecuteSorted();
```

## 5. System API

```csharp
world.AddSystem(new MovementSystem());
world.AddSystem(new DamageResolveSystem());
world.AddSystem(new DeadCleanupSystem());

SimulationContext context = new SimulationContext(1, 0.02f, false);
world.Tick(in context);
```

System 需要实现：

```csharp
public interface IFixedStepSystem
{
    void Tick(in SimulationContext context);
    SystemTickSequence sequence { get; }
}
```

更推荐继承：

```csharp
public abstract class FixedStepSystemBase : IFixedStepSystem
```

这样可以直接使用受保护的 `World` 字段。


## EntityBuilder 链式创建

`EntityBuilder` 用于简化 Entity 初始化：

```csharp
Entity entity = world.CreateEntityBuilder()
    .With(new PositionComponent(0, 0, 0))
    .With(new VelocityComponent(1, 0, 0))
    .Build();
```

等价于手动调用 `CreateEntity` 后多次 `SetComponent`，但代码更集中。

```csharp
public EntityBuilder CreateEntityBuilder();
public Entity BuildEntity(Action<EntityBuilder> configure);
```

## EntityPrefab / EntityFactory 创建入口

`EntityPrefab` 用于保存一组默认组件，`EntityFactory` 用于统一注册和创建多个实体模板。

```csharp
EntityPrefab unitPrefab = new EntityPrefab("Unit")
    .With(new HealthComponent(100, 100))
    .With(new MoveSpeedComponent(5));

EntityFactory factory = new EntityFactory(world);
factory.RegisterPrefab("Unit", unitPrefab);

Entity unit = factory.Create("Unit", builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
});
```

`EntityPrefab` 中同类型组件重复写入时，后写入的组件会覆盖前写入的组件。`EntityFactory.Create(key, overrideBuilder)` 会先应用 Prefab 默认组件，再执行运行时覆盖。
