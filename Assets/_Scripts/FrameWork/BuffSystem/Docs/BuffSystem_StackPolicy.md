# BuffSystem Stack Policy

## 作用

叠层策略决定重复添加同目标、同来源、同配置 Buff 时，现有 Runtime Buff 如何变化。所有策略都必须使用固定帧数据，不能依赖真实时间。

## NormalBuffStackPolicy

当前普通 Buff 策略：

```csharp
RefreshDuration = 0
AddDuration = 1
AddStackOnly = 2
AddStackAndRefreshDuration = 3
CyclicStack = 4
ResetDurationOnly = 5
```

旧枚举值保持不变，`ResetDurationOnly` 只在末尾追加。

### RefreshDuration

作用：重复添加时刷新持续时间，并保留既有“会按旧语义尝试加层”的行为。

运行时行为：

- `stack = ClampStack(stack + incomingStack)`
- `durationFrames = definition.DurationFrames`
- `remainingFrames = definition.DurationFrames`，永久 Buff 为 0
- `elapsedFrames = 0`
- `ticks = 0`

迁移说明：Phase 1 不改变它是否加层的旧语义，只补齐刷新计时重置。

### AddDuration

作用：重复添加时延长持续时间。

运行时行为：

- `durationFrames += DurationExtendFramesPerStack * incomingStack`
- `remainingFrames += DurationExtendFramesPerStack * incomingStack`
- 不重置 `elapsedFrames` 和 `ticks`

### AddStackOnly

作用：重复添加时只增加层数，不刷新持续时间。

运行时行为：

- `stack = ClampStack(stack + incomingStack)`
- 不修改 `durationFrames`
- 不修改 `remainingFrames`
- 不重置 `elapsedFrames` 和 `ticks`

### AddStackAndRefreshDuration

作用：重复添加时增加层数并刷新持续时间。

运行时行为：

- `stack = ClampStack(stack + incomingStack)`
- `durationFrames = definition.DurationFrames`
- `remainingFrames = definition.DurationFrames`，永久 Buff 为 0
- `elapsedFrames = 0`
- `ticks = 0`

### CyclicStack

作用：重复添加时循环叠层。

运行时行为：

- 无限层数时直接累加。
- 非无限层数时按 `MaxStack` 做循环。
- 不刷新持续时间。

### ResetDurationOnly

作用：重复添加时不改变当前层数，只重置持续时间和 Tick 计数。

运行时行为：

- 不修改 `stack`
- `durationFrames = definition.DurationFrames`
- `remainingFrames = definition.DurationFrames`，永久 Buff 为 0
- `elapsedFrames = 0`
- `ticks = 0`

FrameWork2 迁移关系：等价迁移 `ResetRuntimeBuffStackUpStrategy` 的核心语义，但使用 ECS 固定帧字段替代 `RunTime` 和 `Time.time`。

使用样例：

```csharp
BuffDefinition buff = new BuffDefinition(
    configId: 1001,
    name: "Shield Refresh",
    priority: 0,
    maxStack: 3,
    unlimited: false,
    isForever: false,
    durationFrames: 150,
    tickIntervalFrames: 30,
    durationExtendFramesPerStack: 0,
    triggerType: BuffTriggerType.Tick,
    buffType: BuffInstanceType.normal,
    normalStackPolicy: NormalBuffStackPolicy.ResetDurationOnly,
    parallelStackUpPolicy: ParallelBuffStackUpPolicy.Append,
    parallelStackDownPolicy: ParallelBuffStackDownPolicy.RemoveEarliest,
    effectId: 1001);
```

## 部分减层说明

Phase 1 不修改 `RemoveBuffCommand` 的普通 Buff 部分减层语义。当前普通 Buff 部分减层后会将 `remainingFrames` 刷新为当前 `durationFrames`。如果后续要改成“不刷新剩余时间”，需要单独审核。

