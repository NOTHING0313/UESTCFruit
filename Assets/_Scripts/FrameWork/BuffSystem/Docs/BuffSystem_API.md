# BuffSystem API

## IBuffSystem

`IBuffSystem` 是 BuffSystem 的主要对外接口。

```csharp
void Tick(World world, SimulationContext context);
void AddBuff(AddBuffCommand command);
void RemoveBuff(RemoveBuffCommand command);
void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent)
    where TEvent : struct, IGameEvent;
bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data);
IReadOnlyList<BuffViewData> GetBuffs(Entity target);
```

## Phase 3B - ParallelBuffStorageMode

Phase 3B 新增并行 Buff 存储模式配置入口：

```csharp
public enum ParallelBuffStorageMode
{
    EntityPerStack = 0,
    CompressedExpiryFrameList = 1
}
```

`EntityPerStack` 仍是默认值和当前唯一运行时行为：每一个并行层对应一个 Runtime Entity。

`CompressedExpiryFrameList` 目前只是预留配置入口和数据结构骨架，用于后续 Phase 3C 设计一个 Runtime Entity 内部管理多个并行层。Phase 3B 不接入 `Add / Refresh / Remove / Tick / Expire / Query` 主流程，`BuffSystemCore` 当前不会读取该字段。

`BuffConfigData -> BuffDefinition` 会传递 `ParallelStorageMode` 字段，但运行时行为不变。压缩模式设计继续使用固定帧字段，不使用 `Time.time`、`Time.deltaTime` 或 `float expiry`。

### Tick

作用：推进一帧 Buff 逻辑。通常由 `ECSBuffSystem` 在 ECS 固定帧中调用。

参数：

- `world`：当前 ECS World。
- `context`：固定帧上下文，包含帧号和回滚标记。

运行时行为：消费排队的增删请求，推进运行中 Buff 的 `elapsedFrames`、`remainingFrames` 和 `ticks`，并触发对应 Effect。

### AddBuff

作用：添加或刷新 Buff。

参数：

- `AddBuffCommand.Target`：目标实体，必须有效且存活。
- `AddBuffCommand.Source`：来源实体，无来源时为 `Entity.Invalid`。
- `AddBuffCommand.ConfigId`：Buff 配置编号，必须大于 0。
- `AddBuffCommand.Stack`：本次添加层数，小于等于 0 时会被修正为 1。

运行时行为：Tick 外调用会进入内部请求队列，在下一次 Buff Tick 消费。

示例：

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, 1001, source, 1));
```

### RemoveBuff

作用：移除 Buff 层数或清空 Buff。

参数：

- `RemoveBuffCommand.Target`：目标实体。
- `RemoveBuffCommand.Source`：来源实体。
- `RemoveBuffCommand.ConfigId`：Buff 配置编号。
- `RemoveBuffCommand.StackCount`：移除层数。
- `RemoveBuffCommand.MatchAnySource`：是否忽略来源匹配。
- `RemoveBuffCommand.ClearAllStacks`：是否清空全部层数。

运行时行为：第一阶段不改变现有部分减层语义。当前普通 Buff 部分减层后会刷新 `remainingFrames` 为当前 `durationFrames`，该行为保留。

Phase 2A 后，完整移除 Runtime Buff 时会立即退出有效 Buff 查询语义，但物理销毁延迟到生命周期 Effect Flush 之后。`OnRemove` 使用移除前的 Runtime snapshot。

### Raise

作用：触发 ECS 逻辑事件，只响应 `TriggerType == EventTrigger` 且事件编号匹配的 Buff。

参数：

- `world`：当前 ECS World。
- `context`：固定帧上下文。
- `gameEvent`：实现 `IGameEvent` 的 struct 事件。

示例：

```csharp
AttackHitEvent e = new AttackHitEvent(context.frameNumber, 2001, attacker, target);
buffSystem.Raise(world, context, in e);
```

### TryGetBuff / GetBuffs

作用：为 View、UI 和调试面板读取 Buff 只读视图。

返回：

- `BuffViewData.RemainingFrames == -1` 表示永久 Buff。
- 并行 Buff 的视图会合并同目标、同来源、同配置的层数，并取最早到期层的剩余帧。

## API 变化

Phase 1 新增：

```csharp
NormalBuffStackPolicy.ResetDurationOnly = 5
```

旧枚举值顺序和整数值保持不变。

Phase 2A 无 public API 变更。`IBuffEffectExecutor`、`BuffEffectContext`、`IBuffEventEffectExecutor<TEvent>` 签名保持不变；变化仅发生在 `BuffSystemCore` 内部生命周期 Effect 调度时机。

