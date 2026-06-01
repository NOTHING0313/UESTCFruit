# BuffSystem Parallel Buff

## Phase 3B - 压缩存储预留入口

Phase 3B 新增 `ParallelBuffStorageMode`：

```csharp
public enum ParallelBuffStorageMode
{
    EntityPerStack = 0,
    CompressedExpiryFrameList = 1
}
```

当前默认模式仍是 `EntityPerStack`，并且 Phase 3B 不改变任何现有并行 Buff 运行时行为。即使配置中选择了 `CompressedExpiryFrameList`，当前运行时也不会启用压缩逻辑，所有并行 Buff 仍然走每层一个 Runtime Entity 的旧路径。

`CompressedExpiryFrameList` 在 Phase 3B 只包含配置入口和数据结构骨架：

- `CompressedParallelBuffLayer`
- `CompressedParallelBuffRuntimeComponent`
- `CompressedParallelBuffLayerBuffer`

这些结构当前不创建、不写入 World、不参与 Tick、不参与查询，也不接入 Phase 2A 生命周期 EffectRequest Pipeline。后续 Phase 3C 才会单独设计 Add、Refresh、Remove、Expire 和查询接入。

压缩模式预期使用固定帧字段，例如 `expireFrame`、`elapsedFrames`、`ticks` 和 `layerRuntimeHandle`。禁止使用 `Time.time`、`Time.deltaTime` 或 `float expiry` 作为模拟时间依据。

## 作用

并行 Buff 用于表示多层 Buff 独立存在、独立到期的场景。当前第一套 ECS BuffSystem 仍采用“每层一个 Runtime Entity”的设计。

## 当前运行时模型

每个并行层都会创建一个带 `BuffRuntimeComponent` 的 Runtime Entity。

优点：

- 每层有独立 `remainingFrames`。
- 移除最早或最晚层的顺序清晰。
- 回滚快照可以直接捕获每层状态。

成本：

- 层数高时会增加 Entity 数量。
- 查询、排序和快照体积会随层数增加。

## ParallelBuffStackUpPolicy

```csharp
Append = 0
RefreshEarliest = 1
RefreshAll = 2
ReplaceEarliestWhenFull = 3
```

### Append

追加新层。每个新层使用新的 Runtime Entity。

### RefreshEarliest

刷新最早到期的若干层，然后把剩余 incoming 层追加为新层。

Phase 1 行为：被刷新的层会同步重置：

- `durationFrames`
- `remainingFrames`
- `tickIntervalFrames`
- `elapsedFrames = 0`
- `ticks = 0`

### RefreshAll

刷新当前匹配的全部并行层，然后追加 incoming 层。

Phase 1 行为：所有被刷新的层都会同步重置 `elapsedFrames` 和 `ticks`。

### ReplaceEarliestWhenFull

未满时追加新层；满层时移除最早到期层并创建新层。

说明：当前实现通过移除旧层再创建新层表达替换，因此新层天然从完整持续时间和 0 Tick 计数开始。它不直接刷新旧层。

## ParallelBuffStackDownPolicy

```csharp
RemoveEarliest = 0
RemoveLatest = 1
ClearAll = 2
```

排序依据：

- `remainingFrames`
- `runtimeHandle`
- `Entity.ID`
- `Entity.Version`

该排序不依赖 Dictionary 遍历顺序。

## 使用样例

```csharp
BuffDefinition poison = new BuffDefinition(
    configId: 2001,
    name: "Parallel Poison",
    priority: 0,
    maxStack: 5,
    unlimited: false,
    isForever: false,
    durationFrames: 120,
    tickIntervalFrames: 30,
    durationExtendFramesPerStack: 0,
    triggerType: BuffTriggerType.Tick,
    buffType: BuffInstanceType.parallel,
    normalStackPolicy: NormalBuffStackPolicy.RefreshDuration,
    parallelStackUpPolicy: ParallelBuffStackUpPolicy.RefreshEarliest,
    parallelStackDownPolicy: ParallelBuffStackDownPolicy.RemoveEarliest,
    effectId: 2001);
```

## 迁移说明

FrameWork2 使用 `ParallelBuffRunTimeData` 和到期时间队列表达并行层。第一套不迁移该运行时对象，也不使用 `Time.time`。其“独立层到期”和“刷新最早层”的语义已经映射到 ECS Runtime Entity 和固定帧字段上。

