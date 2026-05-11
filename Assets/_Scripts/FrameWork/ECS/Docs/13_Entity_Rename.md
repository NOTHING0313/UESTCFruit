# Entity 命名规范与对外句柄说明

当前版本中，ECS 对外实体句柄统一命名为 `Entity`。

`Entity` 只保存实体 `id` 和 `version`，它不是组件容器，也不直接保存业务数据。业务数据统一由 `ComponentStore<T>` 管理，外部应通过 `World` 提供的 API 访问。

## 推荐用法

```csharp
using ECSFrameWork;

World world = new World();
Entity entity = world.CreateEntity();

world.SetComponent(entity, new HealthComponent(100, 100));

if (world.TryGetComponent(entity, out HealthComponent health))
{
    // read component value
}
```

## 职责边界

| 类型 | 职责 |
|---|---|
| `Entity` | 对外实体句柄，保存 `id/version` |
| `EntityData` | World 内部实体状态，保存存活状态、版本号、组件 Mask |
| `EntityManager` | 创建、销毁、复用 Entity ID，并维护 EntityData |
| `World` | 对外统一入口，负责 Entity / Component / System / Query 等操作 |

## 外部命名空间

所有 ECS Core 类型均位于：

```csharp
namespace ECSFrameWork
```

外部脚本使用 ECS 类型时应添加：

```csharp
using ECSFrameWork;
```

如果脚本本身已经声明在 `namespace ECSFrameWork` 内，可以省略该 using。
