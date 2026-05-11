# ECSFrameWork 对外接口使用文档

本文面向业务逻辑、Unity 表现层、输入层、测试脚本以及后续接入 Buff / 网络 / 回滚模块的开发者。当前 ECS 框架统一命名空间为：

```csharp
using ECSFrameWork;
```

## 1. 对外接口总体边界

### 1.1 推荐依赖的稳定入口

外部业务代码应优先依赖下面这些类型：

| 类型 | 定位 |
|---|---|
| `World` | ECS Core 总入口，负责 Entity、Component、Query、System、Singleton、WorldEvent |
| `Entity` | 对外实体句柄，内部只保存 `ID + Version` |
| `EntityBuilder` | 链式 Entity 创建工具，用于集中设置初始组件 |
| `EntityPrefab` / `EntityFactory` | ECS 实体模板与统一创建入口 |
| `IComponentData` | 组件标记接口，业务组件应实现该接口 |
| `IFixedStepSystem` / `FixedStepSystemBase` | System 生命周期和 Tick 逻辑入口 |
| `SimulationContext` | 单次逻辑帧上下文，包含帧号、步长、回滚标记 |
| `SimulateRunner` | 固定逻辑帧推进器 |
| `EntityQueryBuilder` / `EntityQueryDescription` | 查询实体集合 |
| `SimulationFrameCommandBuffer` / `SimulationFrameCommandScheduler` | 按逻辑帧缓存外部指令 |
| `InputSnapshotBuffer` / `WorldInputApplier` | 按帧输入缓存和输入写入 |
| `IWorldEvent` / `WorldEventBuffer` 相关 API | 逻辑事件输出通道 |
| `ViewManager` / `IViewInstanceProvider` | Unity View 对象创建、释放和查找 |
| `WorldUnityExtensions` | Unity 表现对象接入便利方法 |
| `WorldStatistics` / `SystemProfileInfo` | 调试、性能统计、Editor 面板展示 |

### 1.2 不建议外部依赖的内部概念

外部代码不要直接访问或自行维护以下概念：

| 内部概念 | 原因 |
|---|---|
| `EntityManager` | Entity 创建、复用、Version 校验应由 `World` 统一管理 |
| `ComponentManager` | Component Store 的创建、查找和 ArcheType 同步应由 `World` 调度 |
| `ComponentStore<T>` | 这是 sparse/dense 存储细节，不应暴露给业务层 |
| `ArcheTypeManager` / `ArcheTypeGroup` | Query 分组由框架维护，业务层只通过 Query API 使用 |
| `StructuralChangeBuffer` | Tick 中结构变化延迟执行的内部机制 |
| `SystemChangeBuffer` | Tick 中 System 增删延迟执行的内部机制 |
| `ComponentTypeRegistry` | 组件类型到 Mask 位的映射细节 |

## 2. Entity 接口

`Entity` 是对外实体句柄，不直接保存组件数据，也不保存 View、Mask、Alive 状态。

```csharp
public readonly struct Entity : IEquatable<Entity>
{
    public static readonly Entity Invalid;

    public int ID { get; }
    public int Version { get; }
    public bool IsValid { get; }

    public Entity(int id, int version);

    public bool Equals(Entity other);
    public override bool Equals(object obj);
    public override int GetHashCode();
    public override string ToString();

    public static bool operator ==(Entity left, Entity right);
    public static bool operator !=(Entity left, Entity right);
}
```

推荐用法：

```csharp
World world = new World();
Entity entity = world.CreateEntity();

if (entity.IsValid && world.IsAlive(entity))
{
    world.DestroyEntity(entity);
}
```

注意：`Entity` 的 `ID` 可能被复用，所以不要只保存 `ID`。需要长期引用实体时，应保存完整的 `Entity`，并在使用前调用 `world.IsAlive(entity)` 校验。

## 2.5 EntityBuilder 接口

`EntityBuilder` 用于链式创建 Entity 并集中设置初始组件。它不会直接访问底层 Manager，所有组件写入仍然通过 `World.SetComponent` 完成。

```csharp
public sealed class EntityBuilder
{
    public World World { get; }
    public Entity Entity { get; }
    public bool IsBuilt { get; }

    public EntityBuilder With<T>(in T component) where T : struct, IComponentData;
    public Entity Build();
}
```

`World` 提供两个创建入口：

```csharp
public EntityBuilder CreateEntityBuilder();
public Entity BuildEntity(Action<EntityBuilder> configure);
```

推荐用法：

```csharp
Entity entity = world.CreateEntityBuilder()
    .With(new PositionComponent(0, 0, 0))
    .With(new VelocityComponent(1, 0, 0))
    .With(new HealthComponent(100, 100))
    .Build();
```

也可以用委托集中配置：

```csharp
Entity entity = world.BuildEntity(builder =>
{
    builder.With(new PositionComponent(10, 0, 0));
    builder.With(new HealthComponent(80, 100));
});
```

`Build()` 多次调用会返回同一个 Entity，不会重复创建。`Build()` 后继续调用 `With<T>()` 仍然会作用于同一个 Entity。


## 2.6 EntityPrefab / EntityFactory 接口

`EntityPrefab` 用于保存一组默认组件模板，`EntityFactory` 用于注册多个 Prefab 并通过 key 创建 Entity。

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

核心接口：

```csharp
public interface IEntityPrefab
{
    string Name { get; }
    int ComponentCount { get; }

    Entity Create(World world);
    void ApplyTo(World world, Entity entity);
}

public sealed class EntityPrefab : IEntityPrefab
{
    public EntityPrefab(string name);

    public EntityPrefab With<T>(in T component) where T : struct, IComponentData;
    public bool Has<T>() where T : struct, IComponentData;
    public bool Remove<T>() where T : struct, IComponentData;
    public void Clear();
    public int FillComponentTypes(List<Type> results);

    public Entity Create(World world);
    public void ApplyTo(World world, Entity entity);
}

public sealed class EntityFactory
{
    public World World { get; }
    public int PrefabCount { get; }

    public EntityFactory(World world);

    public bool RegisterPrefab(string key, IEntityPrefab prefab);
    public bool SetPrefab(string key, IEntityPrefab prefab);
    public bool UnregisterPrefab(string key);
    public bool HasPrefab(string key);
    public bool TryGetPrefab(string key, out IEntityPrefab prefab);

    public Entity Create(string key);
    public Entity Create(string key, Action<EntityBuilder> overrideBuilder);
    public bool TryCreate(string key, out Entity entity);
    public bool TryCreate(string key, Action<EntityBuilder> overrideBuilder, out Entity entity);
    public void Clear();
}
```

`EntityPrefab` 不直接生成 Unity `GameObject`。如果实体需要表现对象，应写入 `PrefabViewRequestComponent`，然后交给 `ViewSpawnSystem` 和 `ViewManager` 处理。


## 2.7 EntityPrefabSO / GameplayEntityFactory 接口

当前框架新增 Unity Authoring 层实体配置链路：`ComponentPresetSO -> EntityPrefabSO -> GameplayEntityDefinitionSO -> GameplayEntityFactory`。

推荐业务创建方式：

```csharp
World world = new World();
EntityFactory entityFactory = new EntityFactory(world);
GameplayEntityFactory gameplayFactory = new GameplayEntityFactory(entityFactory);

EntityCreateContext context = EntityCreateContext.Default;
context.position = spawnPosition;

Entity entity = gameplayFactory.Create(definitionSO, in context, builder =>
{
    builder.With(new HealthComponent(1, 80));
});
```

组件优先级为：`EntityPrefabSO 默认组件 -> GameplayEntityDefinitionSO 业务覆盖 -> EntityCreateContext 运行时参数 -> overrideBuilder 最终覆盖`。

`DefinitionSO` 启用的组件不在 `BasePrefab` 中时，由 `EntityDefinitionMismatchPolicy` 决定处理方式：

```csharp
gameplayFactory.MismatchPolicy = EntityDefinitionMismatchPolicy.WarnAndAdd;
```

| 策略 | 行为 |
|---|---|
| `AllowAdd` | 允许添加，不输出警告 |
| `WarnAndAdd` | 允许添加并输出 Warning，默认推荐 |
| `Reject` | 不允许添加，创建失败并返回 `Entity.Invalid` |

完整说明见 `18_Prefab_SO_GameplayFactory.md` 和 `19_Public_API_Usage_Guide_Latest.md`。

## 3. Component 接口

### 3.1 定义组件

所有 ECS 组件都应是 `struct`，并实现 `IComponentData`。

```csharp
public interface IComponentData
{
}
```

示例：

```csharp
public struct PositionComponent : IComponentData
{
    public float x;
    public float y;
    public float z;

    public PositionComponent(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}
```

当前框架的组件 API 统一约束为：

```csharp
where T : struct, IComponentData
```

### 3.2 组件增删查改

通过 `World` 操作组件：

```csharp
public void SetComponent<T>(Entity entity, in T component) where T : struct, IComponentData;
public bool RemoveComponent<T>(Entity entity) where T : struct, IComponentData;
public ref T GetComponent<T>(Entity entity) where T : struct, IComponentData;
public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponentData;
public bool HasComponent<T>(Entity entity) where T : struct, IComponentData;
```

示例：

```csharp
Entity entity = world.CreateEntity();

world.SetComponent(entity, new PositionComponent(0, 0, 0));
world.SetComponent(entity, new VelocityComponent(1, 0, 0));

if (world.HasComponent<PositionComponent>(entity))
{
    ref PositionComponent position = ref world.GetComponent<PositionComponent>(entity);
    position.x += 1f;
}
```

`GetComponent<T>()` 返回的是组件存储中的 `ref`，可以直接写回。不要把这个 `ref` 缓存在 System 字段中，只应在当前方法或当前回调内使用。

### 3.3 Tick 中的结构变化规则

在 `World.Tick()` 过程中调用：

```csharp
world.SetComponent(entity, component);      // 新增组件
world.RemoveComponent<T>(entity);           // 移除组件
world.DestroyEntity(entity);                // 销毁 Entity
```

如果这些操作会改变 Entity 的组件结构，它们不会立即修改 Store，而是进入 `StructuralChangeBuffer`，在当前逻辑帧末尾统一播放。这样可以避免 System 正在遍历时 ComponentStore 被改乱。

覆盖已有组件数据通常会立即执行，因为它不改变 ArcheType。

## 4. World 核心接口

### 4.1 创建、释放与状态

```csharp
public class World
{
    public WorldStates CurrentState { get; }

    public World();
    public void Tick(in SimulationContext context);
    public void Dispose();

    public bool IsDisposing();
    public bool IsSystemOperating();
}
```

生命周期阶段：

```csharp
public enum WorldStates
{
    Initialization = 0,
    Ticking = 1,
    AfterTicking = 2,
    SystemOperating = 3,
    Disposing = 4,
}
```

典型使用：

```csharp
World world = new World();
SimulationContext context = new SimulationContext(frameNumber: 1, tickLength: 0.02f);

world.Tick(in context);
world.Dispose();
```

实际项目中更推荐使用 `SimulateRunner` 推进逻辑帧，而不是外部手动构造 `SimulationContext`。

### 4.2 Entity 管理

```csharp
public Entity CreateEntity();
public bool IsAlive(Entity entity);
public void DestroyEntity(Entity entity);
public IEnumerable<Entity> GetAliveEntities();
```

### 4.3 预分配与容量观察

```csharp
public void EnsureEntityCapacity(int capacity);
public void EnsureComponentCapacity<T>(int capacity) where T : struct, IComponentData;
public int GetComponentStoreCapacity<T>() where T : struct, IComponentData;
```

适合在大量创建 Entity 前预热：

```csharp
world.EnsureEntityCapacity(10000);
world.EnsureComponentCapacity<PositionComponent>(10000);
world.EnsureComponentCapacity<VelocityComponent>(10000);
```

## 5. Singleton Component 接口

Singleton Component 用于保存全局唯一状态，例如逻辑时间、战斗状态、随机种子、玩家资源等。

```csharp
public Entity SetSingleton<T>(in T component) where T : struct, IComponentData;
public bool HasSingleton<T>() where T : struct, IComponentData;
public ref T GetSingleton<T>() where T : struct, IComponentData;
public bool TryGetSingleton<T>(out T component) where T : struct, IComponentData;
public bool TryGetSingletonEntity<T>(out Entity entity) where T : struct, IComponentData;
public bool RemoveSingleton<T>() where T : struct, IComponentData;
```

示例：

```csharp
public struct GameTimeComponent : IComponentData
{
    public int frame;
    public float elapsedTime;
}

world.SetSingleton(new GameTimeComponent { frame = 0, elapsedTime = 0f });

ref GameTimeComponent time = ref world.GetSingleton<GameTimeComponent>();
time.frame++;
time.elapsedTime += 0.02f;
```

`Singleton Component` 本质上仍然是一个普通 `Entity + Component`，只是由 `World` 保证同一组件类型只存在一份。

## 6. Query 查询接口

### 6.1 创建查询

```csharp
public EntityQueryBuilder Query();
```

### 6.2 EntityQueryBuilder

```csharp
public sealed class EntityQueryBuilder
{
    public EntityQueryBuilder With<T>() where T : struct, IComponentData;
    public EntityQueryBuilder Without<T>() where T : struct, IComponentData;

    public EntityQueryDescription BuildDescription();

    public List<Entity> Execute();
    public List<Entity> ExecuteSorted();
    public List<Entity> Execute(bool sorted);

    public List<Entity> ToList(bool sorted = false);
    public int Fill(List<Entity> results, bool sorted = false);
}
```

推荐在 System 中缓存查询描述或复用结果列表：

```csharp
private EntityQueryDescription _query;
private readonly List<Entity> _results = new List<Entity>();

protected override void OnSystemCreate()
{
    _query = World.Query()
        .With<HealthComponent>()
        .Without<DeadTagComponent>()
        .BuildDescription();
}

public override void Tick(in SimulationContext context)
{
    World.FillQuery(_query, _results, sorted: true);

    for (int i = 0; i < _results.Count; i++)
    {
        Entity entity = _results[i];
        ref HealthComponent health = ref World.GetComponent<HealthComponent>(entity);
    }
}
```

### 6.3 EntityQueryDescription

```csharp
public readonly struct EntityQueryDescription : IEquatable<EntityQueryDescription>
{
    public readonly ComponentMask256 includeMask;
    public readonly ComponentMask256 excludeMask;

    public EntityQueryDescription(ComponentMask256 includeMask, ComponentMask256 excludeMask = default);
}
```

### 6.4 Query 使用建议

| 场景 | 推荐方式 |
|---|---|
| 复杂条件：`With + Without` | Query |
| 需要稳定顺序 | Query + `sorted: true` |
| 高频数值更新 | `ForEach<T...>()` |
| 不需要分配结果 List | Query 的 `Fill()` 或 `ForEach<T...>()` |

## 7. ForEach 高频遍历接口

ForEach 直接遍历 ComponentStore 的 dense 数组，不创建 Query 结果列表，适合移动、冷却、生命周期、输入转速度等高频逻辑。

```csharp
public int ForEach<T>(EntityComponentAction<T> action) where T : struct, IComponentData;
public int ForEach<T1, T2>(EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData;
public int ForEach<T1, T2, T3>(EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData;
```

对应委托：

```csharp
public delegate void EntityComponentAction<T>(Entity entity, ref T component) where T : struct, IComponentData;
public delegate void EntityComponentAction<T1, T2>(Entity entity, ref T1 component1, ref T2 component2) where T1 : struct, IComponentData where T2 : struct, IComponentData;
public delegate void EntityComponentAction<T1, T2, T3>(Entity entity, ref T1 component1, ref T2 component2, ref T3 component3) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData;
```

示例：

```csharp
public sealed class MovementSystem : FixedStepSystemBase
{
    private readonly EntityComponentAction<PositionComponent, VelocityComponent> _moveAction;
    private float _tickLength;

    public MovementSystem()
    {
        _moveAction = Move;
    }

    public override SystemTickSequence sequence => SystemTickSequence.movement;

    public override void Tick(in SimulationContext context)
    {
        _tickLength = context.tickLength;
        World.ForEach<PositionComponent, VelocityComponent>(_moveAction);
    }

    private void Move(Entity entity, ref PositionComponent position, ref VelocityComponent velocity)
    {
        position.x += velocity.x * _tickLength;
        position.y += velocity.y * _tickLength;
        position.z += velocity.z * _tickLength;
    }
}
```

不要在 ForEach 中依赖遍历顺序；如果逻辑结算必须稳定排序，应使用 Query + `sorted: true`。

## 8. System 接口

### 8.1 System 生命周期

```csharp
public interface ISystemInitialize
{
    void OnCreate(World world);
}

public interface ISystemDestroy
{
    void OnDestroy(World world);
}

public interface IFixedStepSystem : ISystemInitialize, ISystemDestroy
{
    void Tick(in SimulationContext context);
    SystemTickSequence sequence { get; }
}
```

推荐继承 `FixedStepSystemBase`：

```csharp
public abstract class FixedStepSystemBase : IFixedStepSystem
{
    protected World World { get; }

    public abstract SystemTickSequence sequence { get; }
    public abstract void Tick(in SimulationContext context);

    protected virtual void OnSystemCreate();
    protected virtual void OnSystemDestroy();
}
```

### 8.2 添加和移除 System

```csharp
public void AddSystem(IFixedStepSystem system);
public bool RemoveSystem(IFixedStepSystem system);
public void ClearSystem();
```

示例：

```csharp
World world = new World();

world.AddSystem(new InputMoveSystem());
world.AddSystem(new MovementSystem());
world.AddSystem(new ViewSyncSystem(viewManager));
```

### 8.3 执行顺序

```csharp
public enum SystemTickSequence
{
    input = -400,
    command = -350,
    spawn = -300,
    logic = -200,
    movement = -100,
    damage = -50,
    normal = 0,
    cleanup = 50,
    view = 100,
    viewCleanup = 200,
    entityCleanup = 300,
}
```

数值越小越早执行。

## 9. 固定逻辑帧接口

### 9.1 SimulationContext

```csharp
public readonly struct SimulationContext
{
    public readonly int frameNumber;
    public readonly float tickLength;
    public readonly bool isRollback;

    public SimulationContext(int frameNumber = 0, float tickLength = 0, bool isRollback = false);
}
```

### 9.2 SimulateRunner

```csharp
public class SimulateRunner
{
    public event Action<SimulationContext> BeforeTick;
    public event Action<SimulationContext> AfterTick;

    public int FrameCount { get; }
    public int CurrentFrameNumber { get; }
    public int NextFrameNumber { get; }
    public bool IsTicking { get; }
    public float TickLength { get; }
    public float TickCounter { get; }

    public SimulateRunner(World world, float tickLength, int maxConpensationTickCount);

    public bool Update(float time);
    public bool StepNextFrame(bool isRollback = false);
    public bool TickFrame(int frameNumber, bool isRollback = false);
    public void SetFrameCount(int frameCount);
}
```

Unity 中典型接入：

```csharp
private World _world;
private SimulateRunner _runner;

private void Awake()
{
    _world = new World();
    _runner = new SimulateRunner(_world, 0.02f, 5);
}

private void Update()
{
    _runner.Update(Time.deltaTime);
}
```

## 10. FrameCommand 外部指令接口

FrameCommand 用于把 UI、输入、网络、剧情等外部修改请求按逻辑帧记录下来，再在指定时机消费。

### 10.1 指令接口

```csharp
public interface ISimulationFrameCommand
{
    int FrameNumber { get; }
    void Execute(World world);
}
```

可重建指令接口：

```csharp
public interface IRebuildableSimulationFrameCommand : ISimulationFrameCommand
{
    ISimulationFrameCommand Rebuild(int frameNumber);
}
```

执行时机：

```csharp
public enum SimulationFrameCommandTiming
{
    BeforeTick = 0,
    AfterTick = 1,
}
```

### 10.2 指令缓存

```csharp
public sealed class SimulationFrameCommandBuffer
{
    public int FrameCount { get; }

    public void AddCommand(ISimulationFrameCommand command);
    public void AddCommand(ISimulationFrameCommand command, SimulationFrameCommandTiming timing);

    public bool TryGetCommands(int frameNumber, out IReadOnlyList<ISimulationFrameCommand> commands);
    public bool TryGetCommands(int frameNumber, SimulationFrameCommandTiming timing, out IReadOnlyList<ISimulationFrameCommand> commands);

    public void RemoveBefore(int frameNumber);
    public void Clear();
}
```

### 10.3 指令应用器

```csharp
public sealed class SimulationFrameCommandApplier
{
    public SimulationFrameCommandApplier(World world, SimulationFrameCommandBuffer commandBuffer);

    public void ApplyCommandsToWorld(int frameNumber);
    public void ApplyCommandsToWorld(int frameNumber, SimulationFrameCommandTiming timing);

    public void ReplayCommandsToWorld(int frameNumber, SimulationFrameCommandTiming timing);

    public void ClearAppliedHistory();
    public void RemoveAppliedBefore(int frameNumber);
}
```

### 10.4 指令调度器

```csharp
public sealed class SimulationFrameCommandScheduler
{
    public int DefaultDelayFrames { get; set; }

    public SimulationFrameCommandScheduler(SimulateRunner runner, SimulationFrameCommandBuffer commandBuffer, int defaultDelayFrames = 0);

    public void AddNextFrameStart(ISimulationFrameCommand command);
    public void AddNextFrameEnd(ISimulationFrameCommand command);
    public void AddCurrentFrameEndOrNextFrameEnd(ISimulationFrameCommand command);
    public void AddWithDefaultDelay(ISimulationFrameCommand command);
    public void AddAfterFrames(int delayFrames, SimulationFrameCommandTiming timing, ISimulationFrameCommand command);
    public void AddAtFrame(int frameNumber, SimulationFrameCommandTiming timing, ISimulationFrameCommand command);
}
```

### 10.5 常用内置指令

```csharp
public sealed class CreateEntityFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand;
public readonly struct DestroyEntityFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand;
public readonly struct SetComponentFrameCommand<T> : ISimulationFrameCommand, IRebuildableSimulationFrameCommand where T : struct, IComponentData;
public readonly struct RemoveComponentFrameCommand<T> : ISimulationFrameCommand, IRebuildableSimulationFrameCommand where T : struct, IComponentData;
public readonly struct AddSystemFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand;
public readonly struct RemoveSystemFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand;
public readonly struct ClearSystemFrameCommand : ISimulationFrameCommand, IRebuildableSimulationFrameCommand;
```

示例：

```csharp
SimulationFrameCommandBuffer buffer = new SimulationFrameCommandBuffer();
SimulationFrameCommandApplier applier = new SimulationFrameCommandApplier(world, buffer);

buffer.AddCommand(new SetComponentFrameCommand<VelocityComponent>(10, entity, new VelocityComponent(1, 0, 0)));
applier.ApplyCommandsToWorld(10, SimulationFrameCommandTiming.BeforeTick);
```

## 11. Input 输入接口

### 11.1 输入快照

```csharp
public struct PlayerInputSnapshot
{
    public int frameNumber;
    public int playerID;

    public float moveX;
    public float moveY;

    public float mouseX;
    public float mouseY;
    public float mouseDeltaX;
    public float mouseDeltaY;
    public float scrollX;
    public float scrollY;

    public InputButtonFlags pressedButtons;
    public InputButtonFlags heldButtons;
    public InputButtonFlags releasedButtons;

    public PlayerInputSnapshot(int frameNumber, int playerID);

    public bool IsHeld(InputButtonFlags button);
    public bool WasPressed(InputButtonFlags button);
    public bool WasReleased(InputButtonFlags button);
}
```

按钮标记：

```csharp
[Flags]
public enum InputButtonFlags : ulong
{
    None,
    KeySpace,
    KeyE,
    KeyQ,
    KeyR,
    KeyF,
    KeyLeftShift,
    KeyLeftCtrl,
    KeyEscape,
    MouseLeft,
    MouseRight,
    MouseMiddle,
    MouseBack,
    MouseForward,
}
```

### 11.2 输入组件

```csharp
public struct PlayerInputComponent : IComponentData
{
    public int inputFrame;
    public int playerID;

    public float moveX;
    public float moveY;

    public float mouseX;
    public float mouseY;
    public float mouseDeltaX;
    public float mouseDeltaY;
    public float scrollX;
    public float scrollY;

    public InputButtonFlags pressedButtons;
    public InputButtonFlags heldButtons;
    public InputButtonFlags releasedButtons;

    public static PlayerInputComponent FromSnapshot(in PlayerInputSnapshot snapshot);

    public bool IsValidForFrame(int frameNumber);
    public bool IsHeld(InputButtonFlags button);
    public bool WasPressed(InputButtonFlags button);
    public bool WasReleased(InputButtonFlags button);
}
```

### 11.3 输入提供者和输入缓存

```csharp
public interface IInputProvider
{
    bool TryGetInput(int frameNumber, int playerID, out PlayerInputSnapshot input);
}
```

```csharp
public sealed class InputSnapshotBuffer : IInputProvider
{
    public int FrameCount { get; }

    public void SetInput(in PlayerInputSnapshot snapshot);
    public bool TryGetInput(int frameNumber, int playerID, out PlayerInputSnapshot input);
    public bool HasInput(int frameNumber, int playerID);
    public void RemoveBefore(int frameNumber);
    public void Clear();
}
```

### 11.4 输入写入 World

```csharp
public sealed class WorldInputApplier
{
    public int RegisteredPlayerCount { get; }

    public WorldInputApplier(World world, IInputProvider inputProvider);

    public void RegisterPlayerEntity(int playerID, Entity entity);
    public bool UnregisterPlayerEntity(int playerID);
    public void ClearPlayerEntities();

    public void ApplyInputToWorld(int frameNumber);
}
```

推荐流程：

```csharp
InputSnapshotBuffer inputBuffer = new InputSnapshotBuffer();
WorldInputApplier inputApplier = new WorldInputApplier(world, inputBuffer);

inputApplier.RegisterPlayerEntity(playerID: 0, entity: playerEntity);

runner.BeforeTick += context =>
{
    inputApplier.ApplyInputToWorld(context.frameNumber);
};
```

## 12. WorldEvent 逻辑事件接口

WorldEvent 用于从 ECS 逻辑层向表现层、UI、音效层输出“一次性逻辑结果”。

### 12.1 事件接口

```csharp
public interface IWorldEvent
{
    int frameNumber { get; }
}
```

### 12.2 World 事件 API

```csharp
public void AddWorldEvent<T>(T worldEvent) where T : struct, IWorldEvent;
public IReadOnlyList<T> GetWorldEvents<T>() where T : struct, IWorldEvent;
public void ClearWorldEvents();
public void ClearWorldEventsBeforeFrame(int frameNumber);
```

示例：

```csharp
public readonly struct SkillHitWorldEvent : IWorldEvent
{
    public readonly int frameNumber;
    public readonly Entity source;
    public readonly Entity target;

    int IWorldEvent.frameNumber => frameNumber;

    public SkillHitWorldEvent(int frameNumber, Entity source, Entity target)
    {
        this.frameNumber = frameNumber;
        this.source = source;
        this.target = target;
    }
}

world.AddWorldEvent(new SkillHitWorldEvent(context.frameNumber, source, target));
```

表现层读取：

```csharp
IReadOnlyList<SkillHitWorldEvent> events = world.GetWorldEvents<SkillHitWorldEvent>();

for (int i = 0; i < events.Count; i++)
{
    SkillHitWorldEvent evt = events[i];
    // 播放命中特效、音效、飘字等
}

world.ClearWorldEvents();
```

### 12.3 当前内置事件

```csharp
public readonly struct DamageWorldEvent : IWorldEvent;
public readonly struct EntityDeadWorldEvent : IWorldEvent;
```

## 13. Unity View 接口

### 13.1 ViewManager

```csharp
public sealed class ViewManager
{
    public int PrefabCount { get; }
    public int ViewCount { get; }

    public ViewManager();
    public ViewManager(IViewInstanceProvider instanceProvider);

    public void SetInstanceProvider(IViewInstanceProvider instanceProvider);

    public void RegisterPrefab(int prefabID, GameObject prefab);
    public int Register(Transform transform, bool canRelease = false);

    public int SpawnView(int prefabID, Vector3 position, Quaternion rotation);

    public bool TryGetTransform(int viewID, out Transform transform);

    public bool Unregister(int viewID);
    public bool DestroyView(int viewID);

    public void Clear();
}
```

### 13.2 View 实例提供者

```csharp
public interface IViewInstanceProvider
{
    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
    void Release(GameObject instance);
    void Clear();
}
```

内置实现：

```csharp
public sealed class DefaultViewInstanceProvider : IViewInstanceProvider;
public sealed class PoolSystemViewInstanceProvider : IViewInstanceProvider;
```

### 13.3 Unity 扩展方法

```csharp
public static class WorldUnityExtensions
{
    public static Entity CreateEntityWithView(this World world, int prefabID, Vector3 position);
    public static Entity CreateMovingEntityWithView(this World world, int prefabID, Vector3 position, Vector3 velocity);

    public static bool RequestView(this World world, Entity entity, int prefabID);
    public static bool DestroyEntityWithView(this World world, Entity entity);
    public static bool DestroyViewOnly(this World world, Entity entity);
}
```

### 13.4 Unity View 相关 System

```csharp
public sealed class ViewSpawnSystem : FixedStepSystemBase;
public sealed class ViewSyncSystem : FixedStepSystemBase;
public sealed class ViewDestroySystem : FixedStepSystemBase;
```

典型流程：

```csharp
ViewManager viewManager = new ViewManager();
viewManager.RegisterPrefab(1, playerPrefab);

world.AddSystem(new ViewSpawnSystem(viewManager));
world.AddSystem(new ViewSyncSystem(viewManager));
world.AddSystem(new ViewDestroySystem(viewManager));

Entity player = world.CreateMovingEntityWithView(1, Vector3.zero, Vector3.right);
```

## 14. 统计与性能接口

### 14.1 WorldStatistics

```csharp
public WorldStatistics GetStatistics();
```

`WorldStatistics` 包含：

```csharp
public readonly struct WorldStatistics
{
    public readonly int createdEntityCount;
    public readonly int aliveEntityCount;
    public readonly int freeEntityCount;

    public readonly int componentStoreCount;
    public readonly int archeTypeCount;
    public readonly int queryCacheCount;
    public readonly int archeTypeVersion;

    public readonly int systemCount;
    public readonly int singletonCount;

    public readonly int pendingStructuralChangeCount;
    public readonly int pendingSystemChangeCount;

    public readonly WorldStates currentState;
}
```

### 14.2 SystemProfile

```csharp
public bool EnableSystemProfile { get; set; }
public int SystemProfileCount { get; }

public bool TryGetSystemProfile(IFixedStepSystem system, out SystemProfileInfo profile);
public List<SystemProfileInfo> GetSystemProfiles();
public void ResetSystemProfiles();
```

`SystemProfileInfo` 用于记录 System 的总耗时、最近一次耗时、平均耗时和 Tick 次数。

## 15. 当前内置模板组件与模板 System

这些类型可用于测试或作为业务代码参考，但它们不一定属于长期稳定的 ECS Core。

### 15.1 Unity / 移动相关模板组件

```csharp
public struct PositionComponent : IComponentData;
public struct VelocityComponent : IComponentData;
public struct ViewComponent : IComponentData;
public struct PrefabViewRequestComponent : IComponentData;
public struct ViewDestroyRequestComponent : IComponentData;
public struct EntityDestroyRequestComponent : IComponentData;
public struct PlayerTagComponent : IComponentData;
public struct MoveSpeedComponent : IComponentData;
```

### 15.2 玩法相关模板组件

```csharp
public struct HealthComponent : IComponentData;
public struct StatComponent : IComponentData;
public struct DeadTagComponent : IComponentData;
public struct DamageRequestComponent : IComponentData;
```

### 15.3 模板 System

```csharp
public sealed class MovementSystem : FixedStepSystemBase;
public sealed class InputMoveSystem : FixedStepSystemBase;
public sealed class DamageResolveSystem : FixedStepSystemBase;
public sealed class DeadCleanupSystem : FixedStepSystemBase;
public sealed class EntityDestroySystem : FixedStepSystemBase;
```

## 16. 推荐的最小接入样例

```csharp
using ECSFrameWork;
using UnityEngine;

public sealed class ECSBootstrap : MonoBehaviour
{
    private World _world;
    private SimulateRunner _runner;

    private void Awake()
    {
        _world = new World();
        _runner = new SimulateRunner(_world, 0.02f, 5);

        _world.AddSystem(new MovementSystem());

        Entity entity = _world.CreateEntity();
        _world.SetComponent(entity, new PositionComponent(0, 0, 0));
        _world.SetComponent(entity, new VelocityComponent(1, 0, 0));
    }

    private void Update()
    {
        _runner.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _world?.Dispose();
    }
}
```

## 17. 当前还欠缺或可以补强的对外接口

这一版已经具备可用的 ECS Core，但如果要继续提高框架易用性，我建议后续优先补下面几类接口。

### 17.1 System 查询与按类型移除接口

当前外部可以：

```csharp
world.AddSystem(system);
world.RemoveSystem(system);
world.ClearSystem();
```

但如果调用方没有保存原 System 实例，就无法方便地移除或查询。建议后续增加：

```csharp
public bool HasSystem<T>() where T : class, IFixedStepSystem;
public bool TryGetSystem<T>(out T system) where T : class, IFixedStepSystem;
public bool RemoveSystem<T>() where T : class, IFixedStepSystem;
```

这对 Demo、调试面板、运行时启停模块很有用。

### 17.2 WorldEvent 消费式读取接口

当前事件读取和清理是分开的：

```csharp
var events = world.GetWorldEvents<T>();
world.ClearWorldEvents();
```

后续可以增加：

```csharp
public int ConsumeWorldEvents<T>(List<T> results, bool clearAfterRead = true) where T : struct, IWorldEvent;
public void ClearWorldEvents<T>() where T : struct, IWorldEvent;
```

这样表现层可以只清理自己消费过的事件类型，避免不同表现系统之间互相影响。

### 17.3 Entity 调试信息接口

当前外部可以判断 `IsAlive` 和 `HasComponent<T>`，但无法直接看到某个 Entity 的组件 Mask 或组件类型列表。建议后续增加只读调试接口：

```csharp
public bool TryGetEntityComponentMask(Entity entity, out ComponentMask256 mask);
public int FillEntityComponentTypes(Entity entity, List<Type> results);
```

这些接口适合 Editor Debugger，不建议业务逻辑依赖。

### 17.4 Query Count 接口

当前 Query 常用方式是填充 `List<Entity>`。如果只想统计数量，仍然要构造结果列表。后续可以增加：

```csharp
public int CountQuery(EntityQueryDescription query);
```

适合 AI、调试、测试断言、性能面板。

### 17.5 世界重置接口

当前释放后通常重新创建 `World`。如果某些场景需要保留外部 Runner / Adapter 引用，可以考虑增加：

```csharp
public void Reset();
```

不过这个接口需要谨慎设计，因为它会影响 Entity、Component、System、Event、Singleton、Command 的生命周期。当前阶段不急着加。

### 17.6 Snapshot / StateHash 接口

如果后续正式推进帧同步和回滚，还需要：

```csharp
public WorldSnapshot CreateSnapshot();
public void RestoreSnapshot(in WorldSnapshot snapshot);
public ulong CalculateStateHash();
```

这部分涉及确定性序列化、组件注册顺序、浮点一致性和对象引用隔离，应单独设计，不建议现在仓促接入。

## 18. 本次清理说明

本版已经删除旧的遗留文件：

```text
FrameWork/ECS/Entity/Entity.cs
ECS/SimulationContext.cs
```

原因：

1. 对外实体句柄统一使用 `ECSFrameWork.Entity`。
2. 旧 `Contracts.SimulationContext` 与当前 `ECSFrameWork.SimulationContext` 语义重复。
3. 旧 `Contracts.PlayerInput` 与当前 `PlayerInputSnapshot / PlayerInputComponent` 输入链路不一致。

后续对外文档和业务代码应只使用：

```csharp
ECSFrameWork.Entity
ECSFrameWork.SimulationContext
ECSFrameWork.PlayerInputSnapshot
ECSFrameWork.PlayerInputComponent
```
