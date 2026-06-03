# 03. 类职责索引与迭代优化记录

## 1. 文档定位

本文整合旧文档中的类职责说明、实现思路、测试入口和 ECS 迭代过程。它适合在接手代码、审查结构、或继续规划下一阶段优化时阅读。

为了降低文档数量，原来的架构、生命周期、API、输入、View、测试、优化、Inspector、EditorWindow、Fixed32 评估等内容已经收束到当前三篇文档中。本文重点保留“每个类负责什么”和“框架为什么变成现在这样”。

---

## 2. 核心类职责索引

### 2.1 World 与生命周期

| 文件 | 类型 | 职责 |
|---|---|---|
| `World.cs` | `World` | ECS Core 统一入口，整合 Entity、Component、ArcheType、Query、System、WorldEvent、Singleton、Debug API 与结构变更缓冲。 |
| `World.cs` | `WorldStates` | 描述 World 生命周期阶段：Initialization、Idle、Ticking、AfterTicking、SystemOperating、Disposing。 |
| `World.cs` | `ExecuteType` | World 内部判断结构操作是否可以立即执行的操作类型。 |
| `StructuralChangeBuffer.cs` | `StructuralChangeBuffer` | 缓存 Tick 中产生的结构变化，如 SetComponent、RemoveComponent、DestroyEntity，在 AfterTicking 播放。 |
| `WorldEvent/WorldEventBuffer.cs` | `WorldEventBuffer` | 保存某一逻辑帧产生的一次性事件，供 View / UI / Audio 等消费。 |

实现思路：

```text
World 不判断外界调用来源，只保证进入 World 后内部状态一致。
Tick 中结构变化不立即破坏 Store / ArcheType，而是延迟到 AfterTicking 播放。
```

---

### 2.2 Entity

| 文件 | 类型 | 职责 |
|---|---|---|
| `Entity/Entity.cs` | `Entity` | 对外实体句柄，保存 id / version，用于版本校验和旧句柄失效。 |
| `Entity/EntityData.cs` | `EntityData` | World 内部实体状态，保存 alive、version、componentMask、componentCount。 |
| `Manager/EntityManager.cs` | `EntityManager` | 创建、销毁、复用 Entity ID，维护 EntityData，提供存活枚举和调试信息。 |

实现思路：

```text
Entity 不持有组件。
Entity ID 可以复用，但 version 递增。
所有 Entity 是否有效都通过 World / EntityManager 校验。
```

---

### 2.3 Component

| 文件 | 类型 | 职责 |
|---|---|---|
| `Component/IComponentData.cs` | `IComponentData` | 组件标记接口，组件应为纯数据。 |
| `Component/ComponentMask256.cs` | `ComponentMask256` | 256 bit 组件组合掩码，用于 Entity、ArcheType 和 Query 匹配。 |
| `Component/ComponentTypeRegister.cs` | `ComponentTypeRegistry` | 为组件类型分配注册 ID，并创建对应 Mask。 |
| `Component/ComponentStore.cs` | `ComponentStore<T>` | 用 dense / sparse 存储某一种组件。 |
| `Manager/ComponentManager.cs` | `ComponentManager` | 管理所有 ComponentStore，负责组件增删查改、ForEach 遍历、Debug 信息。 |
| `Component/ComponentIterationDelegates.cs` | `EntityComponentAction` 系列委托 | 为 `World.ForEach<T>` 提供 ref 组件遍历回调签名。 |

实现思路：

```text
每种组件一个 ComponentStore<T>。
sparse[entity.id] 指向 dense index。
删除组件时用尾元素回填，保持 dense 紧凑。
ComponentManager 在组件变化后同步 Entity Mask 与 ArcheType。
```

---

### 2.4 ArcheType 与 Query

| 文件 | 类型 | 职责 |
|---|---|---|
| `Manager/ArcheTypeGroup.cs` | `ArcheTypeGroup` | 保存同一 ComponentMask256 下的 Entity 集合。 |
| `Manager/ArcheTypeManager.cs` | `ArcheTypeManager` | 维护 Mask 到 ArcheTypeGroup 的映射、ArcheTypeVersion 和 QueryCache。 |
| `Query/EntityQueryDescription.cs` | `EntityQueryDescription` | 描述 Query 条件，包括 With / Without 等组件 Mask。 |
| `Query/EntityQueryBuilder.cs` | `EntityQueryBuilder` | 链式构建 QueryDescription。 |
| `Query/EntityQueryCache.cs` | `EntityQueryCache` | 缓存 QueryDescription 匹配的 ArcheType 分组。 |
| `Query/EntityComparer.cs` | `EntityComparer` | 用于 Entity 稳定排序。 |

实现思路：

```text
Query 缓存的是匹配的 ArcheType 分组，不缓存 Entity 结果。
ArcheTypeVersion 变化后 QueryCache 失效。
稳定排序只在调用方明确需要时执行。
```

---

### 2.5 System

| 文件 | 类型 | 职责 |
|---|---|---|
| `System/IFixedStepSystem.cs` | `IFixedStepSystem` | 固定逻辑帧 System 协议。 |
| `System/FixedStepSystemBase.cs` | `FixedStepSystemBase` | System 基类，缓存 World，并提供 OnCreate / OnDestroy 扩展点。 |
| `Manager/SystemManager.cs` | `SystemManager` | 管理 System 注册、排序、Tick、Profile、SystemChangeBuffer。 |
| `System/SystemChangeBuffer.cs` | `SystemChangeBuffer` | 缓冲 System 增删清空操作，在安全阶段播放。 |
| `Stat/SystemProfileInfo.cs` | `SystemProfileInfo` | 保存 System 最近、平均、最大 Tick 耗时和 Tick 次数。 |

示例 System：

| 文件 | 职责 |
|---|---|
| `MovementSystem.cs` | 根据 Velocity 更新 Position。 |
| `InputMoveSystem.cs` | 根据 PlayerInputComponent 和 MoveSpeedComponent 写入 Velocity。 |
| `DamageResolveSystem.cs` | 消费伤害事件或伤害数据并修改 Health。 |
| `DeadCleanupSystem.cs` | 根据死亡状态发出清理请求。 |
| `EntityDestroySystem.cs` | 销毁满足条件的 Entity。 |

实现思路：

```text
System 只在 World.Tick 中执行。
System 列表变更不直接打断当前 Tick。
性能统计以毫秒显示，用于 EditorWindow 观察。
```

---

### 2.6 Time

| 文件 | 类型 | 职责 |
|---|---|---|
| `Time/SimulatorRunner.cs` | `SimulateRunner` | 持有 World，按固定逻辑帧构造 SimulationContext 并调用 Tick。 |
| `Time/SimulatorRunner.cs` | `SimulationContext` | 单次逻辑帧上下文，包含 frameNumber、tickLength、isRollback。 |
| `Time/TimeSimulator.cs` | `TimeSimulator` | Unity MonoBehaviour Adapter，在 Update 中累计 deltaTime 并驱动 Runner。 |

实现思路：

```text
World 不主动读取 Time.deltaTime。
Unity 时间由 TimeSimulator 适配后驱动 SimulateRunner。
```

---

### 2.7 FrameCommand

| 文件 | 类型 | 职责 |
|---|---|---|
| `FrameCommand/ISimulationFrameCommand.cs` | `ISimulationFrameCommand` | 帧命令协议，包含 FrameNumber 与 Execute(World)。 |
| `FrameCommand/SimulationFrameCommandTiming.cs` | `SimulationFrameCommandTiming` | 命令执行时机：BeforeTick / AfterTick。 |
| `FrameCommand/ICommandDebugView.cs` | `ICommandDebugView` | 命令调试摘要接口。 |
| `FrameCommand/SimulationFrameCommands.cs` | 多个内置命令 | 创建 / 销毁 Entity，设置 / 移除 Component，添加 / 移除 / 清空 System。 |
| `FrameCommand/SimulationFrameCommandBuffer.cs` | `SimulationFrameCommandBuffer` | 按 frame + timing 缓存命令。 |
| `FrameCommand/FrameCommandHistory.cs` | `FrameCommandHistory` | 记录最近加入 Buffer 的命令摘要。 |
| `FrameCommand/SimulationFrameCommandApplier.cs` | `SimulationFrameCommandApplier` | 执行指定帧命令，并写入 CommandDebugHistory。 |
| `FrameCommand/CommandDebugUtility.cs` | `CommandDebugUtility` | 把 Command 转换为调试记录。 |
| `FrameCommand/SimulationFrameCommandBufferExtensions.cs` | 扩展方法 | 绝对帧号快捷提交 API。 |
| `FrameCommand/SimulationFrameCommandScheduler.cs` | `SimulationFrameCommandScheduler` | 根据 Runner 当前帧调度命令到目标帧。 |
| `FrameCommand/SimulationFrameCommandSchedulerExtensions.cs` | 扩展方法 | NextFrameStart / NextFrameEnd 等语义化快捷 API。 |

实现思路：

```text
FrameCommand 是可选接入通道，不是所有 World 修改的强制入口。
FrameCommandHistory 不是回滚历史，只是调试和联调用的加入历史。
CommandDebugHistory 记录实际执行结果。
```

---

### 2.8 Input

| 文件 | 类型 | 职责 |
|---|---|---|
| `Input/IInputProvider.cs` | `IInputProvider` | 输入提供者接口。 |
| `Input/InputButtonFlags.cs` | `InputButtonFlags` | 输入按钮 bit flags。 |
| `Input/PlayerInputSnapshot.cs` | `PlayerInputSnapshot` | 某一帧输入快照。 |
| `Input/InputSnapshotBuffer.cs` | `InputSnapshotBuffer` | 按帧缓存输入快照。 |
| `Input/PlayerInputComponent.cs` | `PlayerInputComponent` | 写入 Entity 的输入组件。 |
| `Input/WorldInputApplier.cs` | `WorldInputApplier` | 将输入快照应用到 World。 |
| `Adapter/UnityAdapter/UnityInputAdapter.cs` | `UnityInputAdapter` | 从 Unity Input 采样并生成 PlayerInputSnapshot。 |

实现思路：

```text
Unity 输入不直接在 ECS System 中读取。
输入先变成可缓存的数据快照，再进入 World。
```

---

### 2.9 Factory / Authoring / Gameplay

| 文件 | 类型 | 职责 |
|---|---|---|
| `Factory/EntityBuilder.cs` | `EntityBuilder` | 链式创建 Entity 并写入初始组件。 |
| `Factory/IEntityPrefab*.cs` | Prefab 协议 | 定义运行时实体模板和组件写入单元。 |
| `Factory/EntityPrefab.cs` | `EntityPrefab` | 运行时实体模板实现。 |
| `Factory/EntityFactory.cs` | `EntityFactory` | 注册模板并按 key 创建 Entity。 |
| `Authoring/ComponentPresetSO.cs` | `ComponentPresetSO` | Unity Authoring 层组件预设基类。 |
| `Authoring/EntityPrefabSO.cs` | `EntityPrefabSO` | Unity ScriptableObject 形式的 Entity 模板。 |
| `Gameplay/GameplayEntityDefinitionSO.cs` | `GameplayEntityDefinitionSO` | 玩法实体定义，负责校验和引用组件配置。 |
| `Gameplay/GameplayEntityFactory.cs` | `GameplayEntityFactory` | 根据 GameplayEntityDefinitionSO 创建 Entity。 |

实现思路：

```text
运行时创建通过 World 完成。
SO 只负责配置，不直接绕过 ECS Core。
DefinitionSO 增加校验，避免组件配置重复或缺失。
```

---

### 2.10 View 与 Adapter

| 文件 | 类型 | 职责 |
|---|---|---|
| `Adapter/WorldViewReader.cs` | `WorldViewReader` | 实现 Contracts.IWorldViewReader，给 View 层只读访问。 |
| `Adapter/WorldBuffTargetResolver.cs` | `WorldBuffTargetResolver` | 实现 Contracts.IBuffTargetResolver，给 Buff 层受限读写访问。 |
| `Adapter/UnityAdapter/WorldUnityExtension.cs` | `WorldUnityExtension` | Unity 相关 World 扩展方法。 |
| `Adapter/UnityAdapter/ViewSyncSystem.cs` | `ViewSyncSystem` | 将 ECS Position 同步到表现对象。 |
| `View/ViewManager.cs` | `ViewManager` | 管理 viewId 到表现对象的映射。 |
| `View/IViewInstanceProvider.cs` | `IViewInstanceProvider` | 表现对象创建/释放抽象。 |
| `View/DefaultViewInstanceProvider.cs` | `DefaultViewInstanceProvider` | 默认 Instantiate / Destroy。 |
| `View/PoolSystemViewInstanceProvider.cs` | `PoolSystemViewInstanceProvider` | 适配已有对象池，失败时回退默认创建。 |
| `View/ViewSpawnSystem.cs` | `ViewSpawnSystem` | 根据 PrefabViewRequestComponent 创建表现对象。 |
| `View/ViewDestroySystem.cs` | `ViewDestroySystem` | 回收表现对象并清理 ViewComponent。 |

实现思路：

```text
ECS Core 不直接依赖对象池实现。
ViewManager 只通过 IViewInstanceProvider 创建和释放表现对象。
```

---

### 2.11 Debug / Editor

| 文件 | 类型 | 职责 |
|---|---|---|
| `Debug/WorldDebugSnapshot.cs` | 多个 DebugInfo 结构 | World、Entity、ArcheType、ComponentStore、System、Singleton、Event 的只读调试数据。 |
| `Debug/IECSRuntimeDebugSource.cs` | `IECSRuntimeDebugSource` | Editor / Inspector 获取 World 与 Runner 的调试源接口。 |
| `Debug/CommandDebugModels.cs` | 命令调试模型 | Command 执行状态、执行记录、FrameCommandHistory 展示模型。 |
| `Debug/CommandDebugHistory.cs` | `CommandDebugHistory` | 保存最近一段时间的命令执行记录。 |
| `Editor/ECSRuntimeInspector.cs` | `ECSRuntimeInspector` | 轻量 Inspector 调试面板。 |
| `Editor/ECSWorldDebuggerWindow.cs` | `ECSWorldDebuggerWindow` | 独立 EditorWindow，提供 Overview / Runner / Systems / Entities / Components / Commands 等页面。 |

实现思路：

```text
调试工具只通过 World Debug API 和 DebugSource 接口读取数据。
EditorWindow 不直接访问内部 Manager。
Command 页面区分加入历史和执行历史。
```

---

## 3. 测试脚本职责索引

| 文件 | 主要验证点 |
|---|---|
| `ECSWorldCoreLogicTestBootstrap.cs` | World 基础创建、组件读写、System Tick、Buff/View Adapter 基础接入。 |
| `ECSCoreEntityComponentTestBootstrap.cs` | Entity / ComponentStore / 组件增删改查基础行为。 |
| `ECSLifecycleBufferTestBootstrap.cs` | Tick 中结构变更缓冲、AfterTicking 播放、System 变更缓冲。 |
| `ECSSimulateRunnerTestBootstrap.cs` | 固定逻辑帧推进与 Runner 行为。 |
| `ECSFrameSyncBufferTestBootstrap.cs` | InputSnapshotBuffer、SimulationFrameCommandBuffer、Command Applier、Command Debug。 |
| `ECSQueryCacheRegressionTestBootstrap.cs` | QueryCache 与 ArcheTypeVersion 失效刷新。 |
| `ECSQuerySystemViewProviderTestBootstrap.cs` | Query / System / View Provider 优化链路。 |
| `ECSComponentForEachTestBootstrap.cs` | ForEach 遍历优化与组件 ref 修改。 |
| `ECSPerformanceBenchmarkBootstrap.cs` | Entity 创建、组件写入、Query、ForEach 等性能观测。 |
| `ECSSingletonComponentTestBootstrap.cs` | Singleton Component 设置、读取、移除、调试信息。 |
| `ECSWorldEventBufferTestBootstrap.cs` | WorldEvent 写入、读取、清理。 |
| `ECSSystemProfileTestBootstrap.cs` | System Profile 统计和 Editor 显示数据。 |
| `ECSEntityBuilderTestBootstrap.cs` | EntityBuilder 链式创建。 |
| `ECSGameplayEntityFactorySOTestBootstrap.cs` | SO 配置、Definition 校验、GameplayEntityFactory 创建链路。 |
| `DebuggerTest.cs` | EditorWindow 调试场景，包括 Command 页面测试数据。 |

---

## 4. ECS 迭代与优化过程

这一部分保留旧文档中的迭代记录，便于理解当前代码为什么这样设计。

### 阶段 1：ECS Core 基础闭环

最初目标是完成轻量 ECS 的基本运行能力：

```text
World
Entity / EntityData / EntityManager
IComponentData
ComponentStore<T>
ComponentManager
IFixedStepSystem
SystemManager
SimulationContext
SimulateRunner
```

形成最小闭环：

```text
创建 Entity
写入 Component
注册 System
固定逻辑帧 Tick
System 修改组件
查询 Entity 状态
```

关键设计：

```text
Entity 只是 id/version 句柄。
组件保存在 ComponentStore<T>。
World 是唯一推荐入口。
```

---

### 阶段 2：World 生命周期与结构变更缓冲

在 System Tick 期间直接增删组件或销毁 Entity，可能破坏当前遍历中的 Store / Query / ArcheType。于是引入：

```text
WorldStates
StructuralChangeBuffer
SystemChangeBuffer
```

形成当前 Tick 流程：

```text
Ticking: 执行 System
AfterTicking: 播放结构变更
SystemOperating: 播放 System 变更
Idle: 回到稳定状态
```

这保证外部或 System 调用 `World.SetComponent / RemoveComponent / DestroyEntity` 时，ECS 内部仍能保持一致。

---

### 阶段 3：ArcheType 与 QueryCache 优化

为了避免每次 Query 都扫描所有 Entity，引入 ArcheType 分组：

```text
ComponentMask256 -> ArcheTypeGroup
```

然后 Query 缓存匹配的 ArcheType 分组，而不是缓存 Entity 列表。

原因：

```text
Entity 列表随组件增删变化频繁，容易过期。
ArcheType 分组只在结构变化时更新，可用 ArcheTypeVersion 判断缓存是否失效。
```

当前推荐高频写法是：

```text
System OnCreate 中构建 QueryDescription。
Tick 中用 World.FillQuery 复用 List。
需要排序时显式 sorted=true。
```

---

### 阶段 4：ViewManager 与对象池解耦

早期 View 同步可能直接 Instantiate / Destroy。后续将表现对象创建释放抽象为：

```text
IViewInstanceProvider
DefaultViewInstanceProvider
PoolSystemViewInstanceProvider
```

使 `ViewSpawnSystem / ViewDestroySystem / ViewManager` 不依赖具体对象池实现。

这保留了默认路径，也允许接入已有 PoolSystem。

---

### 阶段 5：性能 Benchmark

新增 `ECSPerformanceBenchmarkBootstrap`，用于粗略观察：

```text
Entity 创建成本
Component 写入成本
Query 成本
ForEach 成本
System Tick 成本
```

该测试不是严格性能测试框架，而是用于迭代过程中发现明显回退。

---

### 阶段 6：Component ForEach 优化

旧 System 高频路径通常是：

```text
FillQuery -> Entity List -> 对每个 Entity 多次 GetComponent sparse 查找
```

优化后新增：

```text
World.ForEach<T>
World.ForEach<T1, T2>
World.ForEach<T1, T2, T3>
```

`ComponentManager` 会选择 Count 最小的 Store 作为主遍历源，减少无效实体检查和重复查询。

已改写示例：

```text
MovementSystem
InputMoveSystem
ViewSyncSystem
```

适用场景：高频、无排序、主要原地修改组件的 System。

不适用场景：需要稳定顺序、复杂 include/exclude、或大量结构变化的逻辑。

---

### 阶段 7：WorldEventBuffer

为了让逻辑层向表现层输出一次性结果，引入：

```text
IWorldEvent
WorldEventBuffer
DamageWorldEvent
EntityDeadWorldEvent
```

它解决的是“这一帧发生了什么”而不是“当前状态是什么”。

例如伤害数字、死亡特效、音效触发都适合由事件表达。

---

### 阶段 8：Singleton Component 与命名空间整理

统一命名空间为：

```csharp
namespace ECSFrameWork
```

并引入 Singleton Component，用内部 Entity 承载全局唯一组件。

主要用途：

```text
全局配置
当前游戏阶段
共享逻辑状态
```

同时将对外实体句柄统一命名为 `Entity`，不再强调 `EntityHandle`，避免概念重复。

---

### 阶段 9：Public API 整理

整理 `World` 对外 API，使外部脚本可以完成：

```text
Entity 创建 / 销毁
Component 增删查改
Singleton 管理
Query 查询
System 注册
WorldEvent 读取
Debug 信息读取
```

原则是：

```text
外部可以拿 World 操作 ECS。
ECS 不限制业务调用来源。
ECS 只维护内部一致性。
```

---

### 阶段 10：EntityBuilder / EntityPrefab / EntityFactory

为了减少创建 Entity 时重复写组件的样板代码，引入：

```text
EntityBuilder
EntityPrefab
EntityFactory
```

形成三层：

```text
EntityBuilder: 单次链式创建
EntityPrefab: 可复用运行时模板
EntityFactory: 按 key 注册和创建模板
```

这些都是 World API 的便利封装，不绕过 ECS 内部生命周期。

---

### 阶段 11：SO Authoring 与 GameplayEntityFactory

为了接入 Unity Inspector 配置，引入：

```text
ComponentPresetSO
EntityPrefabSO
GameplayEntityDefinitionSO
GameplayEntityFactory
```

形成链路：

```text
SO 配置
    ↓
Definition 校验
    ↓
EntityFactory / GameplayEntityFactory
    ↓
World 创建 Entity
```

这让设计数据和运行时代码解耦，同时可以通过 DefinitionValidation 检查组件冲突、缺失和配置错误。

---

### 阶段 12：Runtime Inspector

先实现轻量 Inspector，挂在 `TimeSimulator` 或 `ECSRuntimeDebugTarget` 上。

它验证了 Debug API 是否足够：

```text
World.GetDebugSnapshot
FillAliveEntities
FillSystemDebugInfos
FillArcheTypeDebugInfos
FillComponentStoreDebugInfos
FillSingletonDebugInfos
FillWorldEventDebugInfos
```

Runtime Inspector 定位为快速查看，不追求完整交互。

---

### 阶段 13：EditorWindow Debugger

在 Runtime Inspector 基础上实现独立窗口：

```text
Window / ECSFrameWork / World Debugger
```

迭代重点：

```text
自动扫描 World
保留手动 Refresh
窗口布局扩展
Entity Component 可展开查看字段
System Profile 显示单位 ms
Commands 页显示命令加入历史和执行历史
```

当前 EditorWindow 是主要调试工具，旧 Inspector 保留为轻量入口。

---

### 阶段 14：DebugCommand 与 FrameCommandHistory 收束

一开始命令历史曾使用 Rollback 相关命名。根据分工文档确认：

```text
ECS Core 当前提供 World-level Snapshot Capture / Restore 接口。
Rollback 历史管理、输入保存、回滚触发和 Resimulate 流程属于 2 号范围。
1 号 ECS Core 不实现完整回滚流程。
```

因此将相关命名收束为：

```text
RollbackCommandHistory -> FrameCommandHistory
RollbackCommandDebugRecord -> FrameCommandHistoryRecord
RollbackCommandFrameDebugInfo -> FrameCommandHistoryFrameDebugInfo
```

当前定位：

```text
FrameCommandHistory 记录最近加入 Buffer 的命令。
CommandDebugHistory 记录命令实际执行结果。
它们服务 EditorWindow 和联调，不是 RollbackCoordinator。
```

---

### 阶段 15：Fixed32 迁移评估

已经单独评估过 `float -> Fixed32` 的改造复杂度，结论是：中等偏高，不建议当前阶段一次性全局迁移。

原因：

```text
Unity 表现层天然使用 float / Vector2 / Vector3。
ECS 模拟层可以逐步改为 Fixed32。
SimulationContext.tickLength、MovementSystem、Position / Velocity 等会被牵连。
测试断言和 Adapter 边界也需要调整。
```

推荐后续顺序：

```text
FixedVector2 / FixedVector3
PositionComponent / VelocityComponent / MoveSpeedComponent
MovementSystem / InputMoveSystem
SimulationContext.tickLength
Command 中的位置、方向、速度参数
ViewSync 层 Fixed -> Vector3 转换
```

当前文档整理版本暂时不迁移 Fixed32。

---

## 5. 当前设计边界总结

当前 ECS Core 的职责：

```text
World / Entity / ComponentStore / ArcheType / Query / System
固定逻辑帧推进
结构变更缓冲
FrameCommand 调试接入
WorldEvent 输出
Debug API / EditorWindow 数据源
Unity View / Input 的基础 Adapter
```

当前 ECS Core 不负责：

```text
完整 RollbackCoordinator
SnapshotRingBuffer
远端迟到输入修正
Resimulate
业务规则合法性判断
View 层表现规则
Buff 具体逻辑
```

外界可以通过 `World` 一定程度上操控 ECS 内部对象。ECS 不关心这些修改来自哪里，只保证操作进入 World 后内部数据结构保持一致。

---

## 6. 后续维护建议

优先检查和维护：

```text
1. DestroyEntity 是否清理所有组件和 Singleton 映射。
2. RemoveComponent 是否同步 Entity Mask、ArcheTypeGroup、QueryCache 版本。
3. SetComponent 新增组件和覆盖组件路径是否清晰。
4. StructuralChangeBuffer 播放顺序是否稳定。
5. SystemChangeBuffer 是否不会在 Tick 中破坏 System 列表遍历。
6. FrameCommand 失败策略是否避免半执行后重复执行。
7. CommandBuffer 是否有长期运行清理策略。
8. EditorWindow 是否只通过 Debug API 读数据。
9. Contracts 是否保持受限接口定位，不扩张为 ECS 内部接口目录。
10. Fixed32 迁移应单独开阶段，不与 EditorWindow / Command 调试混在一起。
```
