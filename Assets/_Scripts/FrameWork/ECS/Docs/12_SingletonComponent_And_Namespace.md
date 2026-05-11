# 12. Singleton Component 与命名空间整理

## 1. 命名空间

当前 ECS 框架统一放入：

```csharp
namespace ECSFrameWork
```

外部业务脚本使用 ECS 时，需要添加：

```csharp
using ECSFrameWork;
```

这样可以避免 `World`、`Entity`、`PositionComponent`、`MovementSystem` 等类型污染全局命名空间，也便于后续把 ECS 独立为 Assembly Definition 或 UPM 包。

## 2. Singleton Component 的定位

Singleton Component 用于保存全局唯一的 ECS 状态，例如：

- 游戏时间
- 战斗状态
- 随机种子
- 当前地图状态
- 玩家资源状态
- 帧同步状态

它本质上仍然是 `Entity + Component`，只是由 `World` 保证每种 Singleton Component 只有一个承载 Entity。

## 3. World API

```csharp
public Entity SetSingleton<T>(in T component) where T : struct, IComponentData;
public bool HasSingleton<T>() where T : struct, IComponentData;
public ref T GetSingleton<T>() where T : struct, IComponentData;
public bool TryGetSingleton<T>(out T component) where T : struct, IComponentData;
public bool TryGetSingletonEntity<T>(out Entity entity) where T : struct, IComponentData;
public bool RemoveSingleton<T>() where T : struct, IComponentData;
```

## 4. 使用示例

```csharp
public struct GameTimeComponent : IComponentData
{
    public int frame;
    public float timeScale;
}

World world = new World();
world.SetSingleton(new GameTimeComponent { frame = 1, timeScale = 1f });

ref GameTimeComponent time = ref world.GetSingleton<GameTimeComponent>();
time.frame++;
```

## 5. 生命周期规则

- `SetSingleton<T>()` 第一次调用会创建内部 Entity。
- 再次调用 `SetSingleton<T>()` 会覆盖同一个 Entity 上的组件，不会创建重复 Singleton。
- `RemoveSingleton<T>()` 会移除映射并销毁承载 Entity。
- 如果外部直接 `DestroyEntity(singletonEntity)`，World 会同步清理 Singleton 映射。
- 如果外部直接 `RemoveComponent<T>(singletonEntity)`，World 会在组件真正移除时清理 Singleton 映射。

## 6. internal 可见性整理

以下类型属于 ECS 内部实现，不建议外部业务直接依赖，已改为 `internal`：

- `EntityManager`
- `ComponentManager`
- `SystemManager`
- `ArcheTypeManager`
- `ArcheTypeGroup`
- `ComponentStore<T>`
- `IComponentStore`
- `ComponentTypeRegistry`
- `StructuralChangeBuffer`
- `SystemChangeBuffer`
- `WorldEventBuffer`
- `EntityQueryCache`
- `EntityComparer`
- `ToolFunction`
- `ExcuteType`

外部业务应优先通过 `World`、`Entity`、`IComponentData`、`IFixedStepSystem`、`EntityQueryBuilder` 等公开 API 使用框架。
