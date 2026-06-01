# BuffSystem Changelog

## Phase 3C-1 - Compressed parallel preparation helpers

### 新增

- `BuffSystemCore` 新增 `_compressedRuntimeEntityByKey` lookup cache 字段，并只在清理路径中清空；本阶段不在主流程读写它。
- 新增 `ShouldUseCompressedParallel`，本阶段保持返回 false，确保 `CompressedExpiryFrameList` 不会实际生效。
- 新增 `IsCompressedParallelEligible`，规则为 parallel buff、`CompressedExpiryFrameList`、Tick 触发、非 Unlimited、`MaxStack <= CompressedParallelBuffLayerBuffer.Capacity`。
- `CompressedParallelBuffLayerBuffer` 新增 `RemoveAt`、`FindEarliestIndex`、`FindLatestIndex`、`FindExpiredEarliestIndex`、`AppendLayer`、`RefreshLayer` helper。

### 保持不变

- 不接入 Add、Refresh、Remove、Tick、Query 或 EffectRequest 主流程。
- 不扩展 `BuffEffectRequest` 或 `BuffEffectContext`。
- 不修改 public BuffSystem API、`IBuffEffectExecutor` 或 `IBuffEventEffectExecutor<TEvent>`。
- 当前 EntityPerStack 行为不变。

### Tick / Expire 基准

当前 EntityPerStack 的 `TickRuntimeBuffs` 顺序是先 Tick，再 Expire：先推进 `elapsedFrames` 并在满足间隔时 Queue `OnTick`，随后非永久 Buff 才扣减 `remainingFrames` 并处理自然到期。后续压缩模式正式接入时必须对齐该顺序。

## Phase 3B - Parallel Buff compressed storage skeleton

### 新增

- 新增 `ParallelBuffStorageMode.EntityPerStack = 0`。
- 新增 `ParallelBuffStorageMode.CompressedExpiryFrameList = 1`。
- `BuffConfigData` 新增并行 Buff 存储模式配置字段，默认值为 `EntityPerStack`。
- `BuffDefinition` 新增 `ParallelStorageMode` 只读字段，并通过构造函数尾部可选参数保持旧调用兼容。
- 新增 `CompressedParallelBuffLayer`、`CompressedParallelBuffRuntimeComponent` 和固定容量值类型 `CompressedParallelBuffLayerBuffer`。

### 保持不变

- Phase 3B 不修改 `BuffSystemCore.cs`。
- Phase 3B 不接入 Add、Refresh、Remove、Tick、Expire、TryGetBuff 或 GetBuffs 主流程。
- 当前所有并行 Buff 仍走 EntityPerStack。
- 即使配置选择 `CompressedExpiryFrameList`，当前运行时也不会启用压缩逻辑。
- Phase 2A 生命周期 EffectRequest Pipeline 和事件型 Effect 热路径不变。
- 不使用 `Time.time`、`Time.deltaTime`、`float expiry`、GameObject runtime、MonoBehaviour runtime 或 runtime ScriptableObject Effect。

### 后续

Phase 3C 才会单独设计 `CompressedExpiryFrameList` 如何接入 Add、Refresh、Remove、Expire、Query 与生命周期 EffectRequest Pipeline。

## Phase 2A - Lifecycle EffectRequest Pipeline

### 新增

- 生命周期 Effect 请求队列，覆盖 `Apply / Refresh / StackChanged / Tick / Remove`。
- Remove 延迟物理销毁：Runtime 立即退出有效 Buff 语义，`OnRemove` Flush 后再 `DestroyEntity`。
- 显式生命周期 phase order：`Apply=0, Refresh=1, StackChanged=2, Tick=3, Remove=4`。

### 行为变化

生命周期 Effect 由立即执行改为本帧末尾 Flush。排序规则统一为：

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

Flush 期间新增的 `AddBuff` / `RemoveBuff` 不递归处理，会进入 `_queuedCommands`，由下一次 `BuffSystemCore.Tick -> ConsumeQueuedCommands` 消费。

### 保持不变

- `IBuffEffectExecutor` public API 不变。
- `BuffEffectContext` public API 不变。
- `IBuffEventEffectExecutor<TEvent>` 泛型事件热路径不变。
- 不引入 `GameObject`、`MonoBehaviour`、`Time.time`、`Time.deltaTime` 或 runtime `ScriptableObject Effect`。

## Phase 1.1 - Documentation strictness

### 变更影响示例

`ResetDurationOnly` 用于重复添加时只刷新持续时间，不改变当前层数。下面的示例中，目标已有 2 层 Buff，再次添加 1 层后仍保持 2 层，但持续帧与 Tick 计数会重置。

```csharp
// before: stack = 2, remainingFrames = 40, elapsedFrames = 20, ticks = 1
definition.NormalStackPolicy = NormalBuffStackPolicy.ResetDurationOnly;
buffSystem.AddBuff(new AddBuffCommand(target, configId: 1001, source, stack: 1));

// after: stack = 2, remainingFrames = definition.DurationFrames,
//        elapsedFrames = 0, ticks = 0
```

`RefreshDuration` 保留旧的加层语义。重复添加时仍会按旧规则尝试增加层数，但刷新持续时间后会同步重置 Tick 计数，避免周期效果沿用刷新前的计时状态。

```csharp
// before: stack = 1, elapsedFrames = 29, ticks = 0
definition.NormalStackPolicy = NormalBuffStackPolicy.RefreshDuration;
definition.TickIntervalFrames = 30;
buffSystem.AddBuff(new AddBuffCommand(target, configId: 1002, source, stack: 1));

// after: stack = ClampStack(2), remainingFrames = definition.DurationFrames,
//        elapsedFrames = 0, ticks = 0
```

普通 Buff 的部分减层行为本阶段暂未变更。当前 `RemoveBuffCommand` 只移除部分层数时，仍会保留既有行为：减少 stack 后将 `remainingFrames` 刷新为当前 `durationFrames`。如果后续要改成“减层不刷新剩余时间”，需要单独审核。

## Phase 1 - Low-risk semantic fixes

### 新增

- 新增 `NormalBuffStackPolicy.ResetDurationOnly = 5`。
- 新增标准文档集合，用于记录 API、叠层策略、Effect、事件、并行 Buff、迁移说明、样例和变更历史。

### 行为变化

- `ResetDurationOnly` 重复添加时不改变当前层数，只重置持续时间与 Tick 计数。
- `RefreshDuration` 刷新持续时间时，现在同步重置 `elapsedFrames` 和 `ticks`。
- `AddStackAndRefreshDuration` 刷新持续时间时，现在同步重置 `elapsedFrames` 和 `ticks`。
- 并行 Buff 的 `RefreshEarliest` 和 `RefreshAll` 刷新层持续时间时，现在同步重置该层 `elapsedFrames` 和 `ticks`。

### 保持不变

- 旧枚举值顺序和整数值保持不变。
- `RefreshDuration` 是否加层的旧语义保持不变。
- `RemoveBuffCommand` 的普通 Buff 部分减层语义保持不变。
- ViewCache dirty 行为保持默认安全路径；本阶段只为 `WriteRuntimeComponent` 预留 `markViewDirty` 参数，现有调用默认仍标记 dirty。

### 禁止项自查

- 未引入 `GameObject` 运行时依赖。
- 未引入 `MonoBehaviour` 运行时依赖。
- 未引入 `Time.time` 或 `Time.deltaTime`。
- 未引入 `ScriptableObject` runtime effect。

### 迁移说明

FrameWork2 的 `ResetRuntimeBuffStackUpStrategy` 迁移为第一套 ECS BuffSystem 的 `NormalBuffStackPolicy.ResetDurationOnly`。迁移后使用固定帧字段 `elapsedFrames`、`ticks` 和 `remainingFrames` 表达刷新语义。
