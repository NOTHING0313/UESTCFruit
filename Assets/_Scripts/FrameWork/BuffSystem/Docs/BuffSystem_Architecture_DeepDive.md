# BuffSystem 架构深度文档

> 生成范围：`Assets/_Scripts/FrameWork/BuffSystem/**` 静态只读分析。  
> 生成时间：2026-07-06。  
> 本文只描述当前代码形态和已存在边界，不宣称 `production-ready`、`rollback-ready` 或 `view-ready`。

## 1. 总体定位

BuffSystem 是项目中的 ECS 风格 Buff 运行时与制作工具链集合。它的核心目标是把策划 / Editor 配置转换成固定帧可推进的运行时 Buff 状态，并通过 `IBuffSystem` 提供添加、移除、事件触发和只读查询能力。

当前系统可以分为七层：

| 层级 | 主要文件 | 职责 |
|---|---|---|
| runtime core | `Interface/BuffSystemCore.cs`, `Interface/ECSBuffSystem.cs` | 维护 Buff runtime entity、命令队列、Tick、生命周期 Effect、查询缓存 |
| interface / contracts | `IBuffSystem.cs`, `BuffDefinition.cs`, `BuffViewData.cs`, `BuffEffectECS.cs`, `IGameEvent.cs` | 对外 API、运行时定义、只读视图、Effect / Event 契约 |
| config / loader | `BuffConfigData.cs`, `BuffConfigDataLoader.cs`, catalog / tags | ScriptableObject authoring 数据、Resources 加载、秒转帧、tag 配置查询 |
| storage | `BuffECSComponents.cs`, `BuffSystemCore.cs` | `EntityPerStack` 与 `CompressedExpiryFrameList` 两种 runtime storage |
| trigger / event | `IGameEvent.cs`, `BuffEventFrameCommand.cs`, `BuffEffectECS.cs`, `BuffSystemCore.Raise` | EventTrigger Buff 的事件输入、候选收集与事件 Effect dispatch |
| effect | `BuffEffectRegistryBootstrap.cs`, `Effects/**`, `IBuffGraphAction.cs` | Effect 注册、生命周期回调、图生成 Effect / action 调用链 |
| authoring / test | `Editor/**`, `Test/**` | Authoring Hub、Validator、xNode 图、代码生成、功能/存储/触发/效果/高级测试 |

### 1.1 核心运行时入口

`BuffSystemCore` 是真正的运行时核心，实现 `IBuffSystem` 和 `IDisposable`。它不持有 Unity 场景对象，主要依赖：

- `World`
- `SimulationContext`
- `IBuffDefinitionProvider`
- `BuffEffectRegistry`
- ECS component：`BuffRuntimeComponent` / `CompressedParallelBuffRuntimeComponent`

`ECSBuffSystem` 是 `FixedStepSystemBase` 适配器，把 `BuffSystemCore.Tick(World, context)` 接入 ECS 固定帧 system。

### 1.2 对外 API

`IBuffSystem` 暴露：

| API | 类型 | 语义 |
|---|---|---|
| `Tick(World, SimulationContext)` | simulation | 推进一帧 Buff 逻辑 |
| `AddBuff(AddBuffCommand)` | command | Tick 外调用时排队，下一次 Tick 消费 |
| `RemoveBuff(RemoveBuffCommand)` | command | Tick 外调用时排队，下一次 Tick 消费 |
| `Raise<TEvent>(World, SimulationContext, in TEvent)` | event | 触发 EventTrigger Buff，不负责表现播放 |
| `TryGetBuff(Entity, int, Entity, out BuffViewData)` | query | 按 target/config/source 查单个聚合视图 |
| `GetBuffs(Entity)` | query | 获取 target 当前 Buff 聚合视图列表 |

当前 `IBuffSystem` 没有提供 runtime tag query API。Tag 能力主要存在于 `BuffConfigData.Tags` 与 `BuffConfigDataLoader` 的配置级查询中。

## 2. 数据模型关系

```text
BuffConfigData.asset
  -> BuffConfigData.ToDefinition(tickLength)
  -> BuffDefinition
  -> BuffSystemCore
  -> BuffRuntimeComponent / CompressedParallelBuffRuntimeComponent
  -> BuffViewData
  -> View / Debug / HUD 只读消费
```

### 2.1 BuffConfigData

所在文件：`BuffConfigData.cs`

用途：Unity `ScriptableObject` authoring 配置。包含策划可编辑字段，例如：

- `ID`
- `Name`
- `Description`
- `Icon`
- `Priority`
- `Tags`
- `IsForever`
- `Duration`
- `BuffTriggerType`
- `TickTime`
- `BuffType`
- `Unlimited`
- `MaxStack`
- `NormalStackPolicy`
- `ParallelStackUpPolicy`
- `ParallelStackDownPolicy`
- `ParallelStorageMode`
- `DurationExtendPerStack`
- `EffectId`
- `EventIds`

生命周期：Editor / Resources asset。运行时不直接把 ScriptableObject 当真状态，而是通过 `ToDefinition(float tickLength)` 转为 `BuffDefinition`。

注意事项：

- `Duration` / `TickTime` 是秒，转换为固定帧数后进入 runtime。
- `Icon` 是表现层字段，不参与 ECS 模拟。
- `Tags` 目前进入 loader 的配置级 tag 索引，但不进入 `BuffDefinition`。

### 2.2 BuffDefinition

所在文件：`Interface/BuffDefinition.cs`

用途：运行时纯数据定义，作为 `BuffSystemCore` 的配置输入。核心字段：

- `ConfigId`
- `Name`
- `Priority`
- `MaxStack`
- `Unlimited`
- `IsForever`
- `DurationFrames`
- `TickIntervalFrames`
- `DurationExtendFramesPerStack`
- `TriggerType`
- `BuffType`
- `NormalStackPolicy`
- `ParallelStackUpPolicy`
- `ParallelStackDownPolicy`
- `ParallelStorageMode`
- `EffectId`
- `EventIds`

谁创建：`BuffConfigData.ToDefinition` 或测试中的 `BuffDefinitionRegistry.Register`。

谁消费：`BuffSystemCore` 在 Add / Remove / Tick / Raise / query cache 构建中读取。

注意事项：

- `DurationFrames` 对非永久 Buff 最小为 1。
- `TickIntervalFrames` 最小为 0。
- `CanRespondToEvent(eventId)` 仅对 `TriggerType == EventTrigger` 且 `EventIds` 命中时返回 true。

### 2.3 BuffRuntimeComponent

所在文件：`Interface/BuffECSComponents.cs`

用途：普通 Buff 或 EntityPerStack 并行 Buff 的 ECS runtime component。

核心字段：

- `target`
- `source`
- `configId`
- `runtimeHandle`
- `stack`
- `durationFrames`
- `remainingFrames`
- `tickIntervalFrames`
- `elapsedFrames`
- `ticks`
- `maxStack`
- `priority`
- `unlimited`
- `isForever`
- `buffType`

生命周期：

- Add 时由 `CreateRuntimeBuffEntity` 创建 ECS Entity 并写入 component。
- Tick 中递减 `remainingFrames`、累加 `elapsedFrames` / `ticks`。
- Remove / expire 时先进入 pending remove，生命周期 Effect 执行后销毁 entity。

### 2.4 CompressedParallelBuffRuntimeComponent

所在文件：`Interface/BuffECSComponents.cs`

用途：压缩并行 Buff runtime，一个 runtime entity 内部保存多层并行层。

核心字段：

- `target`
- `source`
- `configId`
- `compressedRuntimeHandle`
- `priority`
- `layerCount`
- `nextLayerId`
- `CompressedParallelBuffLayerBuffer layers`

`CompressedParallelBuffLayerBuffer` 是固定容量值类型容器，容量为 `16`，内部用 `_layer0` 到 `_layer15` 存储，不用数组 / List / Dictionary 表示真状态。

适用条件：

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
configId 在 compressed whitelist 中
```

production whitelist 当前只包含 `991001`。

### 2.5 BuffViewData

所在文件：`Interface/BuffViewData.cs`

用途：面向 UI、Debug、View adapter 的只读 Buff 聚合视图。

字段：

- `Target`
- `Source`
- `ConfigId`
- `Stack`
- `RemainingFrames`
- `RuntimeHandle`

语义：

- `RemainingFrames == -1` 表示永久 Buff。
- 对 EntityPerStack 并行 Buff，多个 runtime 会合并为同一个 `(target, source, configId)` view。
- 对 CompressedParallel，多个 layer 会聚合为一个 view，`Stack` 为 active layer 数，`RemainingFrames` 取最小剩余帧。

### 2.6 BuffEffectContext

所在文件：`Interface/BuffEffectECS.cs`

用途：传给 Effect 的只读上下文。

字段：

- `World`
- `SimulationContext`
- `BuffEntity`
- `Runtime`
- `Definition`

Effect 可以通过 `World` 修改 ECS component，但不应持有需要回滚的私有状态；持久状态应写入 ECS。

### 2.7 命令结构

所在文件：`Interface/IBuffSystem.cs`, `Interface/BuffECSComponents.cs`, `Interface/BuffEventFrameCommand.cs`

| 结构 | 用途 |
|---|---|
| `AddBuffCommand` | Tick 外添加 / 刷新 Buff 的命令数据 |
| `RemoveBuffCommand` | Tick 外移除 Buff 层数的命令数据 |
| `AddBuffRequestComponent` | 通过 ECS 单帧请求 entity 输入 Add |
| `RemoveBuffRequestComponent` | 通过 ECS 单帧请求 entity 输入 Remove |
| `RaiseBuffEventFrameCommand<TEvent>` | 把 `IGameEvent` 作为 frame command 重放 |

`BuffSystemCore` 同时支持直接队列命令和 ECS request component。两者都在 Tick 内消费，保证时序集中。

## 3. 核心机制

### 3.1 AddBuff 机制

输入：

- `AddBuffCommand(Target, ConfigId, Source, Stack)`
- `IBuffDefinitionProvider`
- 当前 `World` / `SimulationContext`

过程：

1. `AddBuff(command)` 只把有效命令加入 `_queuedCommands`。
2. 下一次 `Tick()` 中 `ConsumeQueuedCommands()` 调用 `ApplyAddCommand`。
3. `ApplyAddCommand` 先检查 target 是否存活，再按 `ConfigId` 解析 `BuffDefinition`。
4. 如果是 `parallel`：
   - 满足 compressed 条件则进入 `ApplyCompressedParallelAdd`。
   - 否则进入 `ApplyParallelAdd`。
5. 如果是 `normal`，进入 `ApplyNormalAdd`。
6. 创建 / 刷新 / 叠层 / 替换后，排队生命周期 Effect。
7. 最后由 `FlushLifecycleEffects` 排序执行 Effect。

输出：

- ECS runtime component 更新。
- public view cache 标记 dirty。
- lifecycle effect request 进入待执行队列。

风险点：

- `ConfigId` 找不到时静默无效果。
- EffectId 未注册时 runtime 状态仍可存在，但生命周期回调不会执行。

### 3.2 命令队列与 ECS request component

`BuffSystemCore.Tick` 的前半段顺序是：

```text
EnsureQueries
CaptureRuntimeEntities
CaptureCompressedRuntimeEntities
RebuildRuntimeLookup
RebuildCompressedRuntimeLookup
ConsumeRequestComponents
ConsumeQueuedCommands
```

设计意图：

- request component 允许 frame command / ECS 外部系统用 entity 表达一次性请求。
- `_queuedCommands` 允许普通 API 调用方在 Tick 外提交 Add / Remove。
- 两类命令都在 BuffSystem Tick 内消费，避免调用时立即改 World。

性能影响：

- 每帧会抓取 Add / Remove request query。
- request entities 按 Entity 排序，避免 query 或 dictionary 遍历顺序影响确定性。

### 3.3 Tick / Duration / Expire

`TickRuntimeBuffs` 对 EntityPerStack runtime：

1. 跳过 pending remove。
2. 找 definition。
3. target 不存活或 stack <= 0，则排队 remove。
4. `elapsedFrames++`。
5. 命中 `TickIntervalFrames` 时 `ticks++` 并排队 `OnTick`。
6. 非永久 Buff 执行 `remainingFrames--`。
7. `remainingFrames <= 0` 时：
   - parallel 或 stack <= 1：整 runtime remove。
   - normal 多层：`stack--`，重置 `remainingFrames`，排队 `OnStackChanged(-1)`。

CompressedParallel 通过 `TickCompressedParallelRuntimes` 对每个 layer 增加 elapsed/ticks，并通过 `ExpireCompressedParallelLayers` 移除到期 layer。

### 3.4 Stack / Refresh / Replace

普通 Buff 使用 `NormalBuffStackPolicy`：

| 策略 | 语义 |
|---|---|
| `RefreshDuration` | 增加 stack 并重置 duration |
| `AddDuration` | 按 `DurationExtendFramesPerStack` 延长 duration |
| `AddStackOnly` | 只增加 stack |
| `AddStackAndRefreshDuration` | 增加 stack 并刷新 duration |
| `CyclicStack` | 非 unlimited 时循环层数 |
| `ResetDurationOnly` | 不改变 stack，只重置 duration / tick 计数 |

并行 Buff 使用 `ParallelBuffStackUpPolicy`：

| 策略 | EntityPerStack 行为 | Compressed 行为 |
|---|---|---|
| `Append` | 追加 runtime entity，受 MaxStack 限制 | 追加 layer，受 capacity / MaxStack 限制 |
| `RefreshEarliest` | 优先刷新最早到期层，不足再 append | 优先刷新最早 layer，不足再 append |
| `RefreshAll` | 刷新全部已有层，同时未满时仍可 append incoming | 刷新全部已有 layer，同时未满时仍可 append incoming |
| `ReplaceEarliestWhenFull` | 未满 append，满时移除最早再创建 | 未满 append，满时替换最早 layer |

并行移除使用 `ParallelBuffStackDownPolicy`：

- `RemoveEarliest`
- `RemoveLatest`
- `ClearAll`

排序比较使用剩余时间 / expireFrame、runtimeHandle、Entity ID / Version 等稳定 tie-breaker，避免依赖容器遍历顺序。

### 3.5 Remove / Clear

Remove 输入是 `RemoveBuffCommand`：

- `Target`
- `ConfigId`
- `Source`
- `StackCount`
- `MatchAnySource`
- `ClearAllStacks`

处理过程：

1. 如果 definition 是 compressed eligible，进入 compressed remove。
2. 否则收集 runtime entities。
3. 非 `MatchAnySource` 按 `(target, source, configId)` 查 lookup。
4. `MatchAnySource` 从本帧 runtime 快照和 pending runtime 中收集相同 target/config。
5. 按 remove policy 排序。
6. normal 多层可减少 stack；parallel 通常移除 runtime entity。
7. remove 先写 pending remove、排队 `OnStackChanged` / `OnRemove`，最后 `DestroyPendingRemoveRuntimes` 销毁 entity。

### 3.6 Source / Target 机制

`BuffRuntimeKey` 是核心定位 key：

```text
(target, source, configId)
```

`source` 无效时会归一化为 `Entity.Invalid`。这意味着同 target/config 但不同 source 的 Buff 会形成不同查询结果；`TryGetBuff` 必须传入匹配 source。`RemoveBuffCommand.MatchAnySource` 可以跨 source 移除。

### 3.7 BuffViewData 查询机制

`TryGetBuff` 和 `GetBuffs` 都先调用 `EnsureViewCache()`。

View cache 核心结构：

- `_viewByKey: Dictionary<BuffRuntimeKey, BuffViewData>`
- `_viewsByTarget: Dictionary<Entity, List<BuffViewData>>`
- `_validTargetViewCache: HashSet<Entity>`

构建策略：

1. runtime component 改动时 `MarkViewCacheDirty()`。
2. 外部首次 query 时延迟重建。
3. 普通 / EntityPerStack runtime 由 `ToViewData` 转换。
4. 同 key 的多个 view 通过 `MergeViewData` 聚合：
   - `Stack` 相加。
   - `RemainingFrames` 取最小，永久优先为 -1。
   - `RuntimeHandle` 取最小。
5. compressed runtime 通过 `ToCompressedViewData` 把 active layers 聚合成一个 view。

边界：

- View cache 是派生缓存，不是真状态。
- restore 后必须清空并重建。
- View 层应只读调用 `IBuffSystem.GetBuffs` / `TryGetBuff`，不应直接枚举 runtime component。

### 3.8 Lifecycle Effect 机制

Effect 接口：

- `OnApply`
- `OnRefresh`
- `OnStackChanged`
- `OnTick`
- `OnRemove`

运行时并不直接立即执行生命周期回调，而是创建 `BuffEffectRequest` 进入 `_pendingLifecycleEffects`。flush 前排序：

1. frameNumber
2. lifecycle phase order：Apply -> Refresh -> StackChanged -> Tick -> Remove
3. priority
4. runtimeHandle
5. runtime entity
6. sequence

设计目的：

- 生命周期回调顺序稳定。
- Remove effect 能看到 pre-removal snapshot。
- 同帧多个变更不依赖字典 / query 顺序。

### 3.9 EventTrigger 机制

EventTrigger 依赖：

- `IGameEvent`
- `IBuffSystem.Raise<TEvent>`
- `IBuffEventEffectExecutor<TEvent>`
- `BuffDefinition.EventIds`

流程：

1. 外部调用 `Raise(world, context, in gameEvent)`。
2. `gameEvent.EventId <= 0` 直接返回。
3. `EnsureEventRuntimeIndex` 按当前帧构建 eventId -> runtime entity 索引。
4. 候选 runtime 必须：
   - target 存活。
   - definition 是 `EventTrigger`。
   - `definition.CanRespondToEvent(eventId)`。
   - effect registry 中存在匹配 `IBuffEventEffectExecutor<TEvent>`。
   - `ShouldTrigger(context, event)` 返回 true。
5. 候选按 priority / runtimeHandle / entity 排序。
6. 执行 `OnEvent`。

边界：

- EventTrigger 不进入 compressed whitelist。
- Event effect 使用泛型 struct event，避免 Raise 热路径装箱。

### 3.10 Restore Hook

`BuffSystemCore.OnWorldRestored(World world)` 是内部 restore hook。

处理：

1. 清空 `_queuedCommands`、pending effect、pending remove、lookup、view cache、event index 等派生状态。
2. 不修改 ECS component 真状态。
3. 重新 query runtime / compressed runtime。
4. 重建 lookup。
5. 标记 view cache 和 event index dirty。

边界：

- 当前注释明确只支持 stable snapshot boundary。
- 半帧命令不会被重放。
- rollback 真正确认依赖外部 snapshot / restore / replay 时序，不应仅凭该 hook 宣称 rollback-ready。

## 4. 核心算法详解

### 4.1 Runtime lookup

输入：本帧 runtime entity 快照。

过程：

```text
CaptureRuntimeEntities
  -> _runtimeEntitiesThisFrame
  -> RebuildRuntimeLookup
  -> Dictionary<BuffRuntimeKey, List<Entity>>
```

输出：按 `(target, source, configId)` 快速定位 runtime entities。

性能：

- 避免每次 Add / Remove / Query 都全量扫描 World。
- 空 list 延迟若干帧回收，降低频繁创建销毁 Buff 时的分配抖动。

风险：

- lookup 是派生缓存，restore / component 写入 / remove 后必须 dirty 或重建。

### 4.2 EntityPerStack storage

输入：parallel Buff，且不走 compressed。

过程：

- 每个 stack layer 创建一个 `BuffRuntimeComponent` entity。
- 同一 `(target, source, configId)` 下可以有多个 runtime entity。
- query 时聚合成一个 `BuffViewData`。

优点：

- 语义直观。
- 每层有独立 runtime entity / remainingFrames / runtimeHandle。

缺点：

- 高 stack / 大量目标下 entity 数、query、排序和 rollback snapshot 成本更高。

### 4.3 CompressedExpiryFrameList storage

输入：parallel + Tick + CompressedExpiryFrameList + 非 unlimited + MaxStack <= 16 + whitelist。

过程：

- 每个 `(target, source, configId)` 只创建一个 `CompressedParallelBuffRuntimeComponent`。
- 每个 layer 记录：
  - `layerId`
  - `expireFrame`
  - `elapsedFrames`
  - `ticks`
  - `layerRuntimeHandle`
- append / refresh / replace 在固定容量 buffer 内操作。
- expire 按 `expireFrame` 找最早过期 layer 并移除。

输出：

- runtime entity 数从“每层一个”降为“每组一个”。
- public view 聚合成 active layer count 和最小 remainingFrames。

性能影响：

- 降低 Entity 数和 snapshot 体积。
- 层操作是固定容量线性扫描，容量上限 16。

适用边界：

- 当前 production whitelist 只含 991001。
- EventTrigger / Unlimited / MaxStack 超容量 / 依赖逐层 runtime entity 的 Buff 不适合 compressed。

### 4.4 Effect registry dispatch

输入：`EffectId` 与 `IBuffEffectExecutor` 实例。

过程：

- `BuffEffectRegistry.Register(effectId, effect)` 保存普通生命周期 executor。
- 注册时反射扫描 effect 是否实现 `IBuffEventEffectExecutor<TEvent>`，并缓存到 eventType -> effectId -> executor。
- 生命周期通过 `TryGet(effectId)` 调度。
- EventTrigger 通过 `TryGetEventEffect<TEvent>(effectId)` 调度。

输出：Effect 回调执行。

风险：

- `EffectId == 0` 不执行 effect。
- 未注册 EffectId 不阻止 Buff 状态创建，但 effect 不会运行。
- `BuffEffectRegistryBootstrap` 自动注册块不等价于 whitelist 审批或 runtime 验证。

### 4.5 CompositeEffect / Graph action

当前图形化 authoring 允许从 `BuffCandidateGraph` 构建 Effect 草稿或 CompositeEffect 草稿。核心原则：

- Graph 是 Editor-only 设计 / 审查输入，不是 runtime 配置源。
- 真实 runtime 仍依赖 `BuffConfigData` 与 `BuffEffectRegistryBootstrap`。
- `ScriptActionNode` 通过 `MonoScript` / type name 引用实现 `IBuffGraphAction` 的 action。
- 生成的 Effect 类继承 `BuffEffectExecutorBase`，在生命周期方法中顺序调用 action 的 `Execute(in BuffEffectContext)`。
- CompositeEffect 会按 EffectNode 顺序组织多段 lifecycle action。

顺序解析：

- 没有 `Next` 边时按 `ExecutionOrder` 排序。
- 有 `Next` 边时要求链路单起点、无分叉、覆盖全部 EffectNode，且与 `ExecutionOrder` 不冲突。
- 若链路非法，报告 error，不应生成可投入生产的代码。

边界：

- 生成模板不等于 production 可用。
- 自动注册不等于 whitelist。
- `IBuffGraphAction` 不应持有需要回滚的私有状态。

## 5. 关键流程图

### 5.1 AddBuff 流程

```text
AddBuffCommand
  -> IBuffSystem.AddBuff
  -> _queuedCommands
  -> Tick()
  -> ConsumeQueuedCommands()
  -> ApplyAddCommand()
  -> Resolve BuffDefinition
  -> normal / parallel / compressed branch
  -> Create / Refresh / Stack / Replace
  -> Queue OnApply / OnRefresh / OnStackChanged
  -> FlushLifecycleEffects()
  -> Update / rebuild public view cache on demand
```

### 5.2 Tick 流程

```text
BuffSystemCore.Tick(world, context)
  -> EnsureQueries
  -> CaptureRuntimeEntities
  -> CaptureCompressedRuntimeEntities
  -> RebuildRuntimeLookup
  -> RebuildCompressedRuntimeLookup
  -> ConsumeRequestComponents
  -> ConsumeQueuedCommands
  -> TickRuntimeBuffs
  -> TickCompressedParallelRuntimes
  -> FlushLifecycleEffects
  -> DestroyPendingRemoveRuntimes
```

### 5.3 Query 流程

```text
TryGetBuff / GetBuffs
  -> EnsureViewCache
  -> if dirty: clear view cache
  -> add EntityPerStack runtime views
  -> add same-frame created runtime views
  -> add compressed runtime aggregate views
  -> merge by target/source/configId
  -> return BuffViewData / IReadOnlyList<BuffViewData>
```

### 5.4 Effect 执行流程

```text
Runtime state change
  -> QueueLifecycleEffect(phase)
  -> _pendingLifecycleEffects
  -> sort by frame / phase / priority / handle / entity / sequence
  -> BuffEffectRegistry.TryGet(effectId)
  -> BuffEffectContext
  -> OnApply / OnRefresh / OnStackChanged / OnTick / OnRemove
```

### 5.5 EventTrigger 流程

```text
IGameEvent
  -> IBuffSystem.Raise(world, context, in event)
  -> EnsureEventRuntimeIndex
  -> filter EventTrigger Buffs by EventId
  -> BuffEffectRegistry.TryGetEventEffect<TEvent>
  -> ShouldTrigger
  -> sort candidates
  -> OnEvent
```

### 5.6 CompressedParallel 查询流程

```text
CompressedParallelBuffRuntimeComponent
  -> layers[0..layerCount)
  -> filter active layers by expireFrame
  -> Stack = activeLayerCount
  -> RemainingFrames = min(layer.expireFrame - currentFrame)
  -> RuntimeHandle = min(layerRuntimeHandle)
  -> BuffViewData
```

### 5.7 Restore Hook 流程

```text
World restored by external rollback/snapshot layer
  -> BuffSystemCore.OnWorldRestored(world)
  -> clear queued commands / lifecycle effects / lookup / caches
  -> EnsureQueries
  -> CaptureRuntimeEntities
  -> CaptureCompressedRuntimeEntities
  -> RebuildRuntimeLookup
  -> RebuildCompressedRuntimeLookup
  -> mark ViewCache and EventIndex dirty
```

### 5.8 View Adapter 只读消费流程

```text
View / Debug / HUD
  -> IBuffSystem.GetBuffs(ownerEntity)
  -> IReadOnlyList<BuffViewData>
  -> View-only adapter / formatter
  -> Text HUD / debug panel
```

原则：View 层只读消费 public query，不应调用 Add / Remove / Raise，也不应直接读取 `BuffRuntimeComponent` 或 `CompressedParallelBuffRuntimeComponent` 作为正式表现数据源。

## 6. 每个脚本作用索引

### A. Runtime Core

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `Interface/BuffSystemCore.cs` | `BuffSystemCore` | Buff runtime 核心，处理命令、Tick、storage、effect、query cache、restore hook | runtime |
| `Interface/ECSBuffSystem.cs` | `ECSBuffSystem` | `FixedStepSystemBase` 适配器，把 core 接入 ECS Tick | runtime |

### B. Interface / Contracts

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `Interface/IBuffSystem.cs` | `IBuffSystem`, `AddBuffCommand`, `RemoveBuffCommand` | 对外 API 与命令结构 | runtime contract |
| `Interface/BuffDefinition.cs` | `BuffDefinition`, `IBuffDefinitionProvider`, `BuffDefinitionRegistry` | 运行时 Buff 定义与内存 provider | runtime contract |
| `Interface/BuffViewData.cs` | `BuffViewData` | UI / Debug 只读视图 | runtime contract |
| `Interface/BuffEffectECS.cs` | `BuffEffectContext`, `IBuffEffectExecutor`, `IBuffEventEffectExecutor<T>`, `BuffEffectRegistry` | Effect 上下文、生命周期 / 事件回调与注册表 | runtime contract |
| `Interface/IGameEvent.cs` | `IGameEvent`, `IReframeableGameEvent<T>` | 逻辑事件契约 | runtime contract |
| `Interface/BuffEventFrameCommand.cs` | `RaiseBuffEventFrameCommand<T>`, extensions | 把 Buff event 纳入 frame command | runtime integration |
| `Interface/IBuffGraphAction.cs` | `IBuffGraphAction` | graph-generated Effect 调用 action 的 runtime-safe 接口 | runtime contract |

### C. Config / Definition / Loader

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `BuffConfigData.cs` | `BuffConfigData` | Buff ScriptableObject authoring 配置，转 `BuffDefinition` | config asset |
| `BuffConfigDataLoader.cs` | `BuffConfigDataLoader` | 从 `Resources/BuffSystem/Buff` 加载配置，建立 definition registry 与 tag index | runtime/config |
| `BuffEffectCatalogData.cs` | `BuffEffectCatalogData`, `BuffEffectCatalogEntry` | Editor / authoring 用 Effect 目录 | config/editor helper |
| `BuffEventCatalogData.cs` | `BuffEventCatalogData`, `BuffEventCatalogEntry` | Editor / authoring 用 Event 目录 | config/editor helper |
| `BuffTags.cs` | `BuffTags`, `TagPair<T>` | Tag 配置资产 | config/editor helper |
| `TagRegistry.cs` | `TagRegistry` | string tag 到 int id 的映射 | config helper |
| `BuffSystemEnumCollection.cs` | 多个 enum | Buff 类型、触发、叠层、storage 策略枚举 | shared |

### D. Effect / Registry / CompositeEffect

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `BuffEffectRegistryBootstrap.cs` | `BuffEffectRegistryBootstrap` | production Effect 注册入口；当前注册 `990101 DebugNoOpTickEffect` | runtime bootstrap |
| `Effects/DebugNoOpTickEffect.cs` | `DebugNoOpTickEffect` | 991001 compressed production smoke 用空 Tick effect | runtime effect |
| `Effects/Generated/NewBuffCandidateGraph_1Effect.cs` | `NewBuffCandidateGraph_1Effect` | Authoring Hub 从图生成的 Effect 草稿；需要人工注册审批 | generated draft |
| `Graph/TestGraphAction.cs` | `TestGraphAction_DeleteMe` | 测试 / 原型用 `IBuffGraphAction` 实现 | runtime/test residue |

### E. Trigger / Event

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `Interface/IGameEvent.cs` | `IGameEvent` | EventTrigger 输入契约 | runtime contract |
| `Interface/BuffEventFrameCommand.cs` | `RaiseBuffEventFrameCommand<TEvent>` | 可按帧重放 Buff event | runtime integration |
| `Interface/BuffEffectECS.cs` | `IBuffEventEffectExecutor<TEvent>` | Event effect 回调契约 | runtime contract |

### F. Storage / CompressedParallel

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `Interface/BuffECSComponents.cs` | `BuffRuntimeComponent` | 普通 / EntityPerStack runtime 真状态 | runtime ECS component |
| `Interface/BuffECSComponents.cs` | `CompressedParallelBuffLayer` | compressed 单层数据 | runtime ECS data |
| `Interface/BuffECSComponents.cs` | `CompressedParallelBuffLayerBuffer` | 固定容量 layer 容器，capacity=16 | runtime ECS data |
| `Interface/BuffECSComponents.cs` | `CompressedParallelBuffRuntimeComponent` | compressed runtime 聚合 component | runtime ECS component |
| `Interface/BuffECSComponents.cs` | `AddBuffRequestComponent`, `RemoveBuffRequestComponent` | 单帧 ECS 请求 component | runtime ECS component |

### G. Authoring / xNode / Codegen

| 文件 | 类 / 结构 | 职责 | 类型 |
|---|---|---|---|
| `Editor/BuffAuthoringHubWindow.cs` | `BuffAuthoringHubWindow` | Authoring Hub 主窗口，整合 Validator / Create Buff / Effect Template / Graph | editor-only |
| `Editor/BuffAuthoringValidatorWindow.cs` | `BuffAuthoringValidatorWindow` | 扫描 Buff asset，显示 eligibility / registry / category | editor-only |
| `Editor/BuffCreateWizardWindow.cs` | `BuffCreateWizardWindow` | 创建 BuffConfigData 草稿 | editor-only |
| `Editor/EffectTemplateGeneratorPanel.cs` | `EffectTemplateGeneratorPanel` | 生成 Effect `.cs` 草稿与 registry snippet | editor-only |
| `Editor/BuffAuthoringValidationUtility.cs` | `BuffAuthoringValidationUtility` | 共享只读扫描、effect 注册检查、compressed eligibility、文件名安全处理 | editor-only |
| `Editor/BuffAuthoringHubSettings.cs` | `BuffAuthoringHubSettings`, `BuffAuthoringHubSettingsData` | Authoring Hub 设置 / 默认路径 / ID 策略 | editor-only |
| `Editor/BuffAuthoringText.cs` | `BuffAuthoringText` | Authoring UI 文案集中管理 | editor-only |
| `Editor/AuthoringData/*IdRegistry*` | 多个 registry model / scanner / store / service | Buff / Effect ID Registry 扫描、分配、持久化与报告 | editor-only |
| `Editor/AuthoringData/BuffEffectBootstrapAutoRegistryPatcher.cs` | patcher | 维护 Bootstrap auto registration block | editor-only |
| `Editor/AuthoringData/BuffEffectBootstrapRegistrationScanner.cs` | scanner | 扫描 Bootstrap 注册项 | editor-only |
| `Editor/AuthoringValidation/BuffAuthoringPreflightValidator.cs` | preflight validator | 创建 Buff / Effect 前的字段修正与阻断检查 | editor-only |
| `Editor/AuthoringGraphs/BuffCandidateGraph.cs` | `BuffCandidateGraph` | xNode 候选图资产，Editor-only review input | editor-only |
| `Editor/AuthoringGraphs/BuffCandidateNodes.cs` | candidate nodes | 候选提交、形态、eligibility、风险、决策节点 | editor-only |
| `Editor/AuthoringGraphs/BuffEffectGraphNodes.cs` | effect graph nodes | EffectCompositionRoot / EffectNode / ScriptActionNode 等 | editor-only |
| `Editor/AuthoringGraphs/BuffCandidateGraphBridge.cs` | bridge / summary / drafts | Graph -> Authoring Hub 单向导入 | editor-only |
| `Editor/AuthoringGraphs/BuffCandidateGraphEvaluation.cs` | evaluator | 候选图最小完整性评估 | editor-only |
| `Editor/AuthoringGraphs/BuffCandidateNodeEditors.cs` | xNode editors | 节点 UI 显示与可读性 | editor-only |
| `Editor/AuthoringGraphs/BuffGraphGenerateService.cs` | generate service | 从 graph 创建 Buff / Effect / CompositeEffect 草稿 | editor-only |
| `Editor/AuthoringGraphs/BuffGraphEffectCodegen*` | codegen plan/builder/emitter | Graph action 调用链 Effect 模板生成 | editor-only |
| `Editor/AuthoringGraphs/BuffGraphCompositeEffect*` | composite plan/builder/emitter | 多 EffectNode 合成 CompositeEffect 草稿 | editor-only |
| `Editor/AuthoringGraphs/BuffGraphEffectOrderUtility.cs` | order utility | 解析 EffectNode 顺序 | editor-only |
| `Editor/AuthoringGraphs/BuffGraphGeneratePlan.cs` | plan | 图生成请求计划 | editor-only |
| `Editor/AuthoringGraphs/BuffGraphGenerateReport.cs` | report | 图生成结果报告 | editor-only |
| `Editor/AuthoringGraphs/BuffScriptActionNodeValidation.cs` | validator | 校验 ScriptActionNode action 类型 | editor-only |
| `Editor/AuthoringGraphs/BuffCandidateGraphCreateMenu.cs` | menu | 创建候选图菜单 | editor-only |

### H. Debug / Probe

BuffSystem 目录内当前主要 debug 形态是：

- `Effects/DebugNoOpTickEffect.cs`：991001 smoke effect。
- 多个 `Test/**Runner.cs`：通过 MonoBehaviour / Editor entry 形成验证探针。

View debug 数据如 `BuffDebugSnapshot` / `BuffDebugViewRow` 位于 View 目录，不属于本次 BuffSystem 目录扫描范围。它们应只读消费 public query / adapter 输出，不应反向修改 BuffSystem。

### I. Test / EditorTesting

| 文件 | 职责 | 类型 |
|---|---|---|
| `Editor/Testing/BuffSystemFullTestRunner.cs` | 全量轻量 smoke/unit/integration/whitebox/blackbox/authoring smoke 编排 | editor test |
| `Editor/Testing/BuffSystemMcpTestEntry.cs` | Editor menu / bridge 可调用测试入口 | editor test |
| `Editor/Testing/BuffSystemTestReport.cs` | full runner 报告模型 | editor test |
| `Editor/Testing/BuffSystemTestCaseResult.cs` | full runner case 结果模型 | editor test |
| `Test/BuffSystemPhase2AValidationRunner.cs` | 早期 Add/Tick/Event/Effect 验证 Runner | scene/manual runner |
| `Test/BuffSystemCompressedParallelValidationRunner.cs` | compressed parallel 验证 Runner | scene/manual runner |
| `Test/BuffSystemRestoreHookValidationRunner.cs` | restore hook / cache 验证 Runner | scene/manual runner |
| `Test/BuffSystemStorageBehaviorConsistencyRunner.cs` | EntityPerStack vs Compressed 行为一致性 Runner | scene/manual runner |
| `Test/BuffSystemStoragePerformanceRunner.cs` | storage 性能观察 Runner | scene/manual runner |
| `Test/Editor/BuffSystemAdvanced*` | stress / performance / fuzz / soak advanced runner 与报告 | editor test |
| `Test/Editor/BuffSystemFunctionalCoverage*` | 功能覆盖测试与报告 | editor test |
| `Test/Editor/BuffSystemLifecycle*` | lifecycle 专项测试与报告 | editor test |
| `Test/Editor/BuffSystemStorage*` | storage / compressed 专项测试与报告 | editor test |
| `Test/Editor/BuffSystemTrigger*` | trigger / EventTrigger 专项测试与报告 | editor test |
| `Test/Editor/BuffSystemEffect*` | effect / CompositeEffect / GraphStyle 专项测试与报告 | editor test |
| `Test/Editor/BuffSystemTag*` | tag 能力发现与边界报告 | editor test |

### J. Docs / Reports

`Docs` 下已有 Overview、API、StackPolicy、ParallelBuff、EffectPipeline、EventPipeline、TestingGuide、AuthoringGuide、CompositeEffectAuthoring、xNodeAuthoringGraph 等文档。本文是深度架构索引，不替代已有指南。

## 7. 当前生产 smoke 与边界

### 7.1 991001 compressed production smoke

当前 Resources 中的 `Debug_CompressedParallel_TickSmoke.asset`：

- `ConfigId = 991001`
- `EffectId = 990101`
- `BuffType = parallel`
- `TriggerType = Tick`
- `ParallelStorageMode = CompressedExpiryFrameList`
- `Unlimited = false`
- `MaxStack = 3`

`BuffEffectRegistryBootstrap` 注册了 `990101 DebugNoOpTickEffect`。`BuffSystemCore.CreateForProduction` 的 compressed production whitelist 仅包含 `991001`。

边界：

- 991001 是 debug / smoke asset，不是正式 gameplay Buff。
- whitelist 单点 smoke 不代表更多生产 Buff 已可进入 compressed path。

### 7.2 当前 Resources 状态注意

当前 `Assets/Resources/BuffSystem/Buff` 下存在：

- `Debug_CompressedParallel_TickSmoke.asset`
- `100001_NewBuffCandidateGraph_1.asset`

因此 `BuffConfigDataLoader` 会扫描到不止一个 asset。任何 production path 验证都应明确目标 configId，不应把全部 Resources asset 都视为正式 production candidate。

### 7.3 Tag runtime query 边界

当前：

- `BuffConfigData.Tags` 存在。
- `BuffConfigDataLoader` 支持配置级 tag 查询，如 `BuffHasTag`、`FindBuffsWithTag`、`FindBuffWithAllTags`。
- `BuffDefinition` 不包含 tag 字段。
- `IBuffSystem` / `BuffSystemCore` 没有公开 live runtime tag query API。

因此 Tag 专项测试中的 runtime tag case 当前应标记为能力缺失 / not supported，而不是 runtime bug。

### 7.4 Rollback 边界

`OnWorldRestored` 只重建 BuffSystem 派生缓存，不负责：

- 保存 / 恢复 World snapshot。
- 重放半帧命令。
- 校验 View resync。
- 证明 deterministic replay 完整闭环。

因此当前不能宣称 BuffSystem rollback-ready。

### 7.5 View 边界

BuffSystem runtime 只提供 `BuffViewData` public query。正式 View / HUD 应只读消费：

```text
IBuffSystem.GetBuffs(ownerEntity)
IBuffSystem.TryGetBuff(target, configId, source, out view)
```

View 不应直接枚举 runtime component，也不应调用 Add / Remove / Raise 影响权威状态。当前 View smoke 已有代码侧路径，但这不等价于完整 view-ready 或 PlayMode 全场景覆盖。

## 8. 风险清单

| 风险 | 当前状态 | 建议 |
|---|---|---|
| 正式 gameplay Buff 候选缺失 | 只有 smoke / draft asset 证据 | 候选提交后先走只读审查与单 configId 验证 |
| compressed whitelist 扩大风险 | production whitelist 仅 991001 | 不应批量放开，逐个审批 |
| Effect 未注册 | 未注册 effect 不阻止状态创建，但不会执行回调 | Validator / preflight 必须检查 |
| Resources 草稿污染 production 验证 | 当前 Resources 有 100001 draft | 验证报告必须列出扫描 asset，明确目标 |
| runtime tag query 缺失 | config-level tag 有，live runtime tag 无 | 若需要 runtime tag，另开 contract 阶段 |
| rollback-ready 过度声明 | restore hook 只管派生缓存 | rollback 闭环另行验证 |
| View 直接读 runtime | 不应作为正式路径 | 只用 `BuffViewData` / View adapter |
| Graph 生成误认为生产可用 | Graph / template / auto registration 不等于 production approval | 文档、工具和流程继续强调人工审查 |

## 9. 推荐阅读路径

如果后续接手 BuffSystem，建议按以下顺序读代码：

1. `Interface/IBuffSystem.cs`
2. `Interface/BuffDefinition.cs`
3. `Interface/BuffECSComponents.cs`
4. `Interface/BuffEffectECS.cs`
5. `Interface/BuffSystemCore.cs`
6. `BuffConfigData.cs`
7. `BuffConfigDataLoader.cs`
8. `BuffEffectRegistryBootstrap.cs`
9. `Editor/BuffAuthoringValidationUtility.cs`
10. `Editor/AuthoringGraphs/BuffCandidateGraph.cs`
11. `Editor/AuthoringGraphs/BuffGraphGenerateService.cs`
12. `Test/Editor/BuffSystemFunctionalCoverageRunner.cs`
13. `Test/Editor/BuffSystemStorageTestRunner.cs`
14. `Test/Editor/BuffSystemEffectTestRunner.cs`

## 10. 当前结论

BuffSystem 当前已经具备较完整的 ECS Buff runtime 基础：命令队列、固定帧 Tick、普通 / 并行 / 压缩并行 storage、生命周期 Effect、EventTrigger、只读 view query、restore hook 缓存刷新、Editor authoring、xNode 图生成和多套测试 runner。

但当前仍必须保守：

- 不宣称 production-ready。
- 不宣称 rollback-ready。
- 不宣称 view-ready。
- 不宣称 991001 是正式玩法 Buff。
- 不宣称所有真实生产场景已完整回归。

下一步若要推进生产化，应优先围绕真实 gameplay Buff 候选执行单 configId 审查、effect 注册审查、EntityPerStack vs Compressed 行为一致性验证、View production path 手动验证和回退方案确认。
