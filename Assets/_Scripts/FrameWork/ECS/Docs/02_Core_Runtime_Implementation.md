# 02. 核心实现与运行机制

## 1. 文档定位

本文面向继续维护 ECS Core 的开发者，说明当前框架已经实现的功能、运行时生命周期、关键数据结构、命令缓冲、调试窗口和各模块协作方式。

当前 ECS 的核心目标是：

```text
用 World 统一管理 Entity / Component / ArcheType / Query / System / Event / Debug，
并通过固定逻辑帧推进 System。
```

它不是完整网络回滚系统，也不负责限制外界业务如何调用 World。

---

## 2. 总体结构

```text
World
├─ EntityManager
│  └─ EntityData[]
├─ ComponentManager
│  └─ ComponentStore<T>
├─ ComponentTypeRegistry
│  └─ Type -> RegisterID -> ComponentMask256 bit
├─ ArcheTypeManager
│  └─ ComponentMask256 -> ArcheTypeGroup
├─ SystemManager
│  └─ IFixedStepSystem + SystemChangeBuffer + SystemProfileInfo
├─ StructuralChangeBuffer
├─ WorldEventBuffer
├─ Singleton Component 映射
└─ Debug API
```

外部通常只持有 `World`，内部 Manager 不作为公共访问入口。

---

## 3. Entity 实现

### 3.1 Entity 句柄

`Entity` 是只读值类型，保存：

```text
id
version
```

它不是组件容器，也不保存逻辑数据。旧 Entity 被销毁后，ID 可以复用，但 version 会变化，因此旧句柄无法通过 `World.IsAlive(entity)` 校验。

### 3.2 EntityData

`EntityData` 是 World 内部状态，保存：

```text
alive
version
componentMask
componentCount
```

### 3.3 EntityManager

`EntityManager` 负责：

```text
创建 Entity
销毁 Entity
复用 free ID
维护 EntityData
校验 id/version
枚举存活 Entity
生成 EntityDebugInfo
```

创建 Entity 时从 free ID 栈复用或扩展数组；销毁 Entity 时标记死亡、递增版本并压入 free ID。

---

## 4. Component 存储实现

### 4.1 IComponentData

组件实现 `IComponentData`，推荐为 `struct`。组件只保存数据，行为放入 System。

### 4.2 ComponentTypeRegistry

`ComponentTypeRegistry` 为每个组件类型分配稳定的注册 ID，并映射到 `ComponentMask256` 的 bit 位。

```text
typeof(PositionComponent) -> registerID -> bit index
```

当前 Mask 支持 256 个组件类型。

### 4.3 ComponentMask256

`ComponentMask256` 使用 4 个 `ulong` 组成 256 bit 掩码，用于描述 Entity 拥有哪些组件，以及 ArcheType / Query 匹配。

主要能力：

```text
Set / Clear / Has
ContainsAll
Intersects
CountBits
Equals / GetHashCode
```

### 4.4 ComponentStore<T>

每种组件类型对应一个 `ComponentStore<T>`，内部采用 dense / sparse 结构：

```text
_denseEntities[index]     -> Entity
_denseComponents[index]   -> T component
_sparse[entity.id]        -> dense index；不存在时为 -1
```

删除组件时使用尾元素回填，保证 dense 数组紧凑：

```text
removeIndex <- lastIndex
更新 movedEntity 的 sparse 指向
清理 lastIndex
```

这种结构让 `HasComponent / GetComponent / SetComponent / RemoveComponent` 都可以保持较低成本。

### 4.5 ComponentManager

`ComponentManager` 管理所有 `ComponentStore<T>`，并在组件增删时同步 Entity 的 `ComponentMask256` 和 ArcheType 分组。

它还提供高频遍历 API：

```csharp
ForEach<T>()
ForEach<T1, T2>()
ForEach<T1, T2, T3>()
```

多组件遍历会选择组件数量最少的 Store 作为主遍历源，再通过其它 Store 的 sparse 映射校验和取 ref。

---

## 5. ArcheType 与 Query

### 5.1 ArcheTypeGroup

`ArcheTypeGroup` 维护某个组件组合 Mask 下的 Entity 列表。

```text
ComponentMask256 mask
List<Entity> entities
```

当 Entity 增删组件时，`ArcheTypeManager.ChangeGroup` 会把 Entity 从旧 Mask 分组移到新 Mask 分组。

### 5.2 ArcheTypeManager

职责：

```text
维护 Mask -> ArcheTypeGroup
维护 ArcheTypeVersion
根据 QueryDescription 匹配分组
提供 FillEntityByQuery
提供 QueryCache 调试信息
```

### 5.3 QueryCache

当前 Query 优化缓存的是 `EntityQueryDescription` 对应的 ArcheType 分组匹配结果，而不是缓存某一帧的 Entity 结果。

原因是 Entity 列表随时可能变化，缓存 Entity 容易过期；ArcheType 分组只在结构变化时更新，可以通过 `ArcheTypeVersion` 判断缓存是否失效。

---

## 6. World 生命周期

`WorldStates`：

| 状态 | 含义 |
|---|---|
| `Initialization` | World 正在初始化内部 Manager 与 Buffer |
| `Idle` | World 空闲，可立即执行大多数结构修改 |
| `Ticking` | System 正在 Tick，结构修改进入 StructuralChangeBuffer |
| `AfterTicking` | System Tick 结束，正在播放结构变更 |
| `SystemOperating` | 正在播放 SystemChangeBuffer |
| `Disposing` | World 正在释放，外部修改请求被忽略 |

一次 `World.Tick` 的流程：

```text
SetWorldState(Ticking)
    ↓
SystemManager.Tick(context)
    ↓
SetWorldState(AfterTicking)
    ↓
StructuralChangeBuffer.Playback(world)
    ↓
SetWorldState(SystemOperating)
    ↓
SystemManager.PlaybackSystemChanges()
    ↓
SetWorldState(Idle)
```

### 6.1 结构变化规则

在 `Idle / Initialization / AfterTicking` 中，新增组件、移除组件、销毁 Entity 可立即执行。

在 `Ticking` 中，结构性变化会进入 `StructuralChangeBuffer`，避免 System 遍历期间破坏 Store / ArcheType 结构。

已有组件的数据覆盖不是结构变化，可以立即写入。

`World.CreateEntity()` 在 `Ticking` 中仍会立即分配实体句柄。此时实体已经 `IsAlive == true`，但如果它的组件是在同一次 `Tick` 中新增的，这些新增组件会先进入 `StructuralChangeBuffer`，直到 `AfterTicking` 播放后才同步到 ComponentStore / ArcheType。因此，新建实体可能在当前 Tick 内存活，但在组件播放前不会被依赖对应组件的 Query 命中。

### 6.2 System 变化规则

System 增删由 `SystemManager` 和 `SystemChangeBuffer` 管理。System 列表变更在安全阶段播放，避免 Tick 遍历过程中修改 System 列表。

---

## 7. System 执行与性能统计

System 实现 `IFixedStepSystem` 或继承 `FixedStepSystemBase`。

典型生命周期：

```text
OnCreate(World)
Tick(in SimulationContext)
OnDestroy()
```

`SystemManager` 负责：

```text
注册 / 移除 / 清空 System
按 SystemTickSequence 排序
Tick 所有 System
维护 SystemProfileInfo
播放 SystemChangeBuffer
```

`SystemProfileInfo` 记录：

```text
Last Tick ms
Average Tick ms
Max Tick ms
Tick Count
```

EditorWindow 中显示的 `Last / Avg / Max` 均以毫秒为单位。

---

## 8. 固定逻辑帧推进

### 8.1 SimulationContext

`SimulationContext` 是一次逻辑帧执行上下文，当前包含：

```text
frameNumber
tickLength
isRollback
```

它属于 ECSFrameWork 内部执行上下文，不建议移动到 Contracts。

### 8.2 SimulateRunner

`SimulateRunner` 持有 `World`，并负责按固定步长推进逻辑帧。

主要职责：

```text
保存当前帧号
按 tickLength 构造 SimulationContext
调用 BeforeTick / World.Tick / AfterTick
限制单次 Update 最大补帧数量
```

### 8.3 TimeSimulator

`TimeSimulator` 是 Unity MonoBehaviour Adapter。它在 Unity `Update` 中累计 `Time.deltaTime`，再驱动 `SimulateRunner` 补帧。

同时它实现：

```text
IECSRuntimeDebugSource
IECSFrameCommandDebugSource
```

因此 EditorWindow 可以从 TimeSimulator 获取 World、Runner、CommandBuffer 和 CommandApplier。

---

## 9. FrameCommand 运行机制

`FrameCommand` 文件夹提供按逻辑帧执行外部指令的通道。

### 9.1 基础协议

```text
ISimulationFrameCommand
SimulationFrameCommandTiming
ICommandDebugView
```

`ISimulationFrameCommand.Execute(World world)` 的职责是把已确定的命令数据应用到 World。它不判断帧号、不记录 Debug、不防重复执行。

### 9.2 内置命令

```text
CreateEntityFrameCommand
DestroyEntityFrameCommand
SetComponentFrameCommand<T>
RemoveComponentFrameCommand<T>
AddSystemFrameCommand
RemoveSystemFrameCommand
ClearSystemFrameCommand
```

这些命令都通过 `World` API 修改 ECS，不直接访问内部 Manager。

### 9.3 Buffer / History / Applier

```text
SimulationFrameCommandBuffer
    按 frameNumber + timing 保存命令。

FrameCommandHistory
    记录最近加入 Buffer 的命令摘要，用于 EditorWindow 展示。

SimulationFrameCommandApplier
    读取 Buffer，执行 command.Execute(world)，并记录 CommandDebugHistory。
```

`FrameCommandHistory` 只是命令加入历史，不表示执行成功或失败。

`CommandDebugHistory` 记录执行结果：

```text
Executed
Skipped
Failed
Replay
```

### 9.4 Scheduler 与 Extensions

```text
SimulationFrameCommandBufferExtensions
    绝对帧号快捷 API。

SimulationFrameCommandScheduler
    基于 Runner 当前帧自动计算目标帧。

SimulationFrameCommandSchedulerExtensions
    next frame / current frame end 等语义化快捷 API。
```

---

## 10. WorldEventBuffer

`WorldEventBuffer` 是 ECS 逻辑层向表现层、UI、音效层输出一次性事件的通道。

当前事件示例：

```text
DamageWorldEvent
EntityDeadWorldEvent
```

事件有 frame 信息，可以按帧清理：

```csharp
world.ClearWorldEventsBeforeFrame(frameNumber);
```

事件不应代替组件状态；它只表达某一帧发生过的结果。

---

## 11. Entity 创建体系

### 11.1 EntityBuilder

链式创建 Entity 并设置初始组件：

```csharp
Entity entity = world.CreateEntityBuilder()
    .With(new PositionComponent(...))
    .With(new HealthComponent(...))
    .Build();
```

`EntityBuilder` 只是便利封装，内部仍通过 `World.SetComponent` 写入组件。

### 11.2 EntityPrefab 与 EntityFactory

`EntityPrefab` 描述运行时实体模板，`EntityFactory` 负责注册和创建。

职责拆分：

| 类型 | 职责 |
|---|---|
| `IEntityPrefabElement` | 单个组件写入单元 |
| `IEntityPrefab` | 多个组件元素组成的实体模板 |
| `EntityPrefab` | 运行时模板实现 |
| `EntityFactory` | 根据 key 创建 Entity |

### 11.3 SO Authoring 与 GameplayEntityFactory

Unity Authoring 层通过：

```text
ComponentPresetSO
EntityPrefabSO
GameplayEntityDefinitionSO
GameplayEntityFactory
```

把 Inspector 配置转成 ECS Entity。

这条链路适合编辑器配置、怪物模板、建筑模板、关卡单位等数据驱动创建场景。

---

## 12. View 同步机制

View 模块不是 ECS Core 的必要部分，但当前提供了基础适配：

```text
ViewManager
IViewInstanceProvider
DefaultViewInstanceProvider
PoolSystemViewInstanceProvider
ViewSpawnSystem
ViewSyncSystem
ViewDestroySystem
```

核心思想：

```text
PrefabViewRequestComponent 触发生成表现对象
ViewComponent 绑定 viewId
PositionComponent 同步 Transform
销毁 Entity 或移除 ViewComponent 后释放表现对象
```

`ViewManager` 不关心对象池具体实现，只依赖 `IViewInstanceProvider`。

---

## 13. Debug 与 EditorWindow

### 13.1 Debug API

`World` 提供一组只读 Debug API：

```text
GetDebugSnapshot
FillAliveEntities
TryGetEntityDebugInfo
FillEntityComponentTypes
TryGetComponentDebugValue
FillComponentStoreDebugInfos
FillArcheTypeDebugInfos
FillSystemDebugInfos
FillSingletonDebugInfos
FillWorldEventDebugInfos
```

调试工具不直接访问内部 Manager。

### 13.2 Runtime Inspector

`ECSRuntimeInspector` 是挂在 `TimeSimulator` 或 `ECSRuntimeDebugTarget` Inspector 下方的轻量调试面板。

适合快速查看：

```text
World 总览
Runner 状态
System Profile 简表
ArcheType / Entity / Store / Singleton / Event 概览
```

### 13.3 ECSWorldDebuggerWindow

入口：

```text
Window / ECSFrameWork / World Debugger
```

当前页面：

```text
Overview
Runner
Systems
ArcheTypes
Entities
Components
Singletons
Events
Commands
```

`Commands` 页显示：

```text
Frame Command Buffer
Frame Command History
Debug Execution History
```

其中 `Frame Command History` 是加入历史，`Debug Execution History` 是执行结果。

---

## 14. 当前不负责的范围

当前 ECS Core 不实现：

```text
RollbackCoordinator
SnapshotRingBuffer
InputBuffer<TInput> 的完整网络回滚语义
ResimulateTo
迟到输入检测
预测修正
```

当前保留的 `FrameCommandHistory` 只是调试和联调历史，不是完整 Rollback 历史。

---

## 15. 维护原则

后续修改 ECS Core 时优先保证：

```text
1. 所有外部操作通过 World API 进入 ECS。
2. EntityData、ComponentStore、Entity Mask、ArcheTypeGroup 始终一致。
3. Tick 中结构变化进入 StructuralChangeBuffer。
4. System 列表变化进入 SystemChangeBuffer。
5. QueryCache 以 ArcheTypeVersion 失效。
6. Debug API 只读，不改变 World 状态。
7. EditorWindow 不直接访问内部 Manager。
8. Command Debug 与 FrameCommandHistory 不参与逻辑推进控制。
```
