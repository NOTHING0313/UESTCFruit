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
EntityInfo entity = world.CreateEntity();
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
foreach (EntityInfo entity in world.Query().With<PositionComponent>().With<VelocityComponent>().ExecuteSorted())
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
