# 01. 对外接口与接入边界

## 1. 文档定位

本文面向使用 ECSFrameWork 的外部代码：业务逻辑、测试脚本、Unity Adapter、View 同步、Buff 模块、输入模块以及调试工具。它说明当前 ECS 对外暴露哪些能力、外部应该如何调用、哪些接口只是协作边界而不是强制访问模型。

当前结论是：

```text
World 是 ECS Core 的主要公共入口。
Contracts 只保留少量跨模块受限接口。
外界可以通过 World 读取或修改 ECS 状态，ECS 只负责保证进入 World 后内部状态一致。
```

ECS 不判断调用方属于 View、UI、Buff、网络、测试还是 Bootstrap；这些属于上层模块职责。ECS Core 只保证：Entity 版本有效、ComponentStore 与 Entity Mask 同步、ArcheType 分组正确、System Tick 期间结构变化被安全缓冲。

---

## 2. 命名空间与目录

当前 ECS Core 位于：

```csharp
using ECSFrameWork;
```

目前额外存在一个独立 `Contracts` 文件夹，用于存放外部模块可选依赖的受限接口：

```csharp
using Contracts;
```

当前不要把 `SimulationContext`、`IFixedStepSystem`、`IComponentData`、`Entity`、组件类型或 ECS 内部 Manager 移入 `Contracts`。这些仍然属于 ECSFrameWork 内部或核心公开模型。

推荐结构：

```text
ECSFrameWork
├─ World
├─ Entity
├─ IComponentData
├─ SimulationContext
├─ IFixedStepSystem
├─ ComponentStore / Manager / Query / System / Command / Debug

Contracts
├─ IWorldViewReader
└─ IBuffTargetResolver
```

---

## 3. World：核心访问入口

`World` 是当前 ECS Core 的统一入口，外部代码应优先通过它操作 ECS，而不是直接访问 `EntityManager`、`ComponentManager`、`ArcheTypeManager`、`SystemManager` 或 `ComponentStore<T>`。

### 3.1 创建与释放

```csharp
World world = new World();
world.Dispose();
```

`World.Dispose()` 会进入 `Disposing` 状态，清空结构命令、事件缓存和 System 队列。进入释放状态后，大多数写入请求会被忽略。

### 3.2 Entity 操作

```csharp
Entity entity = world.CreateEntity();
bool alive = world.IsAlive(entity);
world.DestroyEntity(entity);
```

`Entity` 是 `id + version` 组成的实体句柄，不保存业务数据。Entity 被销毁后，版本号机制用于避免旧句柄误操作复用后的 Entity。

`World.CreateEntity()` 在 `World.Tick` 期间仍会立即返回一个存活实体句柄；但 Tick 中给该实体新增组件会进入结构变更缓冲，直到本帧 `AfterTicking` 播放后才会进入对应 ComponentStore / ArcheType。系统作者不应假设“Tick 中新建并新增组件”的实体会立刻被同一 Tick 内的组件 Query 命中。

### 3.3 Component 操作

```csharp
world.SetComponent(entity, new HealthComponent(100, 100));

if (world.TryGetComponent(entity, out HealthComponent health))
{
    // 读取组件副本
}

if (world.HasComponent<HealthComponent>(entity))
{
    ref HealthComponent healthRef = ref world.GetComponent<HealthComponent>(entity);
    healthRef.current -= 10;
}

world.RemoveComponent<HealthComponent>(entity);
```

组件实现 `IComponentData`，推荐使用 `struct`，只保存数据，不保存 `GameObject`、`Transform`、`MonoBehaviour` 等 Unity 对象引用。

### 3.4 Singleton Component

Singleton 本质上仍然由内部 Entity 承载，只是通过类型映射提供全局访问入口。

```csharp
Entity singletonEntity = world.SetSingleton(new GameConfigComponent(...));

if (world.TryGetSingleton(out GameConfigComponent config))
{
    // 读取配置
}

ref GameConfigComponent configRef = ref world.GetSingleton<GameConfigComponent>();
world.RemoveSingleton<GameConfigComponent>();
```

适合保存全局配置、当前回合状态、逻辑层共享状态。不建议用 Singleton 替代普通 Entity 组件关系。

---

## 4. System 接入

System 实现 `IFixedStepSystem` 或继承 `FixedStepSystemBase`。

推荐写法：

```csharp
public sealed class MovementSystem : FixedStepSystemBase
{
    protected override void OnSystemCreate()
    {
        // 缓存 QueryDescription 或初始化临时容器
    }

    public override void Tick(in SimulationContext context)
    {
        World.ForEach<PositionComponent, VelocityComponent>((Entity entity, ref PositionComponent position, ref VelocityComponent velocity) =>
        {
            position.x += velocity.x * context.tickLength;
            position.y += velocity.y * context.tickLength;
            position.z += velocity.z * context.tickLength;
        });
    }
}
```

注册方式：

```csharp
world.AddSystem(new MovementSystem());
world.RemoveSystem(system);
world.ClearSystem();
```

`SystemTickSequence` 决定 System 执行顺序。需要确定性执行的逻辑应保持 System 列表和排序稳定。

---

## 5. Query 与遍历 API

### 5.1 QueryDescription + FillQuery

适合复杂 include / exclude 查询，或者需要稳定排序的逻辑。

```csharp
private readonly List<Entity> _entities = new List<Entity>(128);
private EntityQueryDescription _query;

protected override void OnSystemCreate()
{
    _query = World.Query()
        .With<PositionComponent>()
        .With<VelocityComponent>()
        .BuildDescription();
}

public override void Tick(in SimulationContext context)
{
    World.FillQuery(_query, _entities, sorted: false);

    for (int i = 0; i < _entities.Count; i++)
    {
        Entity entity = _entities[i];
    }
}
```

### 5.2 ForEach 高频遍历

适合 Movement、InputMove、ViewSync 等高频、无排序需求的系统。

```csharp
world.ForEach<PositionComponent, VelocityComponent>((Entity entity, ref PositionComponent position, ref VelocityComponent velocity) =>
{
    position.x += velocity.x;
});
```

`ForEach<T1, T2, T3>` 会选择组件数量最少的 Store 作为主遍历源，以降低 sparse 查找次数。

需要稳定顺序、复杂过滤或大量结构修改时，优先使用 `QueryDescription + FillQuery`。

---

## 6. 输入与帧命令接入

### 6.1 输入快照

当前输入链路将 Unity 输入采样为 `PlayerInputSnapshot`，再写入 `InputSnapshotBuffer`，最后由 `WorldInputApplier` 写入 ECS 组件。

```text
UnityInputAdapter.SampleInput()
    ↓
InputSnapshotBuffer
    ↓
WorldInputApplier
    ↓
PlayerInputComponent
    ↓
InputMoveSystem
```

输入模块只负责把外部输入变成确定的数据快照，不应该在 ECS System 中直接读取 Unity Input。

### 6.2 FrameCommand

`FrameCommand` 是按逻辑帧缓存、调度和执行外部 World 操作的可选通道。它适合测试注入、固定帧输入、调试追踪，以及未来网络同步接入点。

```csharp
SimulationFrameCommandBuffer buffer = new SimulationFrameCommandBuffer();

buffer.AddCommand(
    new SetComponentFrameCommand<VelocityComponent>(frameNumber, entity, velocity),
    SimulationFrameCommandTiming.BeforeTick
);
```

执行路径：

```text
外界创建 ISimulationFrameCommand
    ↓
SimulationFrameCommandBuffer.AddCommand
    ↓
FrameCommandHistory 记录加入历史
    ↓
SimulationFrameCommandApplier.ApplyCommandsToWorld
    ↓
command.Execute(world)
    ↓
CommandDebugHistory 记录执行结果
```

`ISimulationFrameCommand.Execute(World world)` 只负责把已确定的命令数据应用到 World，不负责判断帧号、不负责记录 Debug、不负责防重复执行。

---

## 7. Contracts：受限访问接口

`Contracts` 不是 ECS 的强制访问模型。外界如果已经持有 `World`，可以直接通过 `World` 调用公开 API。`Contracts` 的意义是为特定协作模块提供更窄的访问面。

### 7.1 IWorldViewReader

面向 View 层，只读 ECS 状态。

```csharp
namespace Contracts
{
    public interface IWorldViewReader
    {
        bool TryGetViewId(Entity entity, out int viewId);
        bool TryGetPosition(Entity entity, out PositionComponent position);
        bool TryGetHealth(Entity entity, out HealthComponent health);
        IEnumerable<Entity> GetAliveEntities();
    }
}
```

当前实现：

```csharp
IWorldViewReader reader = new WorldViewReader(world);
```

View 层通过它同步表现对象位置、血条和存活实体，不直接修改 ECS 逻辑状态。

### 7.2 IBuffTargetResolver

面向 Buff 模块，受限读写目标 Entity 的逻辑组件。

```csharp
namespace Contracts
{
    public interface IBuffTargetResolver
    {
        bool IsAlive(Entity entity);

        bool HasHealth(Entity entity);
        ref HealthComponent GetHealth(Entity entity);

        bool HasPosition(Entity entity);
        ref PositionComponent GetPosition(Entity entity);

        bool HasStat(Entity entity);
        ref StatComponent GetStat(Entity entity);
    }
}
```

当前实现：

```csharp
IBuffTargetResolver resolver = new WorldBuffTargetResolver(world);
```

调用 `GetHealth / GetPosition / GetStat` 前应先调用对应 `HasXxx`。这些方法返回 `ref`，可以修改组件。

---

## 8. View 接入

当前 View 同步链路由 ECS 侧提供基础实现：

```text
ViewManager
IViewInstanceProvider
DefaultViewInstanceProvider
PoolSystemViewInstanceProvider
ViewSpawnSystem
ViewSyncSystem
ViewDestroySystem
WorldViewReader
```

基本原则是：

```text
ECS 负责产生逻辑状态。
View 层负责根据状态生成、移动、销毁表现对象。
View 不应反向决定逻辑结果。
```

`ViewManager` 通过 `IViewInstanceProvider` 与对象池解耦。默认 Provider 使用 `Instantiate / Destroy`；池化 Provider 可适配已有对象池，不要求 ECS Core 直接依赖对象池实现。

---

## 9. WorldEvent 接入

`WorldEventBuffer` 用于输出一次性逻辑事件，例如伤害、死亡、命中特效请求等。

```csharp
world.AddWorldEvent(new DamageWorldEvent(frame, source, target, damage));
IReadOnlyList<DamageWorldEvent> events = world.GetWorldEvents<DamageWorldEvent>();
world.ClearWorldEventsBeforeFrame(frame);
```

事件用于 Logic -> View / UI / Audio 的单向通知。它不是组件状态，也不应该被当作持久数据。

---

## 10. Debug 接入

当前 Debug API 统一从 `World` 暴露：

```csharp
WorldDebugSnapshot snapshot = world.GetDebugSnapshot();
world.FillAliveEntities(results);
world.FillComponentStoreDebugInfos(storeInfos);
world.FillArcheTypeDebugInfos(archeTypeInfos);
world.FillSystemDebugInfos(systemInfos);
world.FillSingletonDebugInfos(singletonInfos);
world.FillWorldEventDebugInfos(eventInfos);
```

`ECSRuntimeInspector` 和 `ECSWorldDebuggerWindow` 都只通过这些 Debug API 读取数据，不直接访问内部 Manager。

调试源接口：

```text
IECSRuntimeDebugSource
IECSFrameCommandDebugSource
```

当前 `TimeSimulator` 已实现这两个接口，`ECSRuntimeDebugTarget` 可用于自定义 Bootstrap 手动绑定 World / Runner / CommandBuffer / CommandApplier。

---

## 11. 当前边界结论

当前版本的外部接入原则：

```text
1. World 是主要访问入口。
2. Contracts 只是受限访问面，不替代 World。
3. ECS 不关心调用方是谁，也不判断业务时机。
4. ECS 只保证所有进入 World 的操作在内部保持一致。
5. System 执行期间的结构变化由 StructuralChangeBuffer / SystemChangeBuffer 延迟处理。
6. FrameCommand 是可选的按帧外部指令通道，不是所有修改的强制入口。
7. Rollback / Snapshot / Resimulate 不属于当前 ECS Core 负责实现的范围。
```
