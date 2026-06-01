# BuffSystem Migration From FrameWork2

## Phase 3B - ParallelBuffRunTimeData 迁移预备

FrameWork2 的 `ParallelBuffRunTimeData` 使用内部到期队列管理多个并行层。第一套 ECS BuffSystem 不迁移它的真实时间依赖，也不使用 `Time.time`、`Time.deltaTime` 或 `float expiry`。

Phase 3B 只迁移“配置入口和数据结构骨架”：

- 新增 `ParallelBuffStorageMode.EntityPerStack = 0`，作为默认旧行为。
- 新增 `ParallelBuffStorageMode.CompressedExpiryFrameList = 1`，作为后续压缩存储预留入口。
- 新增固定帧层数据 `CompressedParallelBuffLayer`，使用 `expireFrame / elapsedFrames / ticks / layerRuntimeHandle`。
- 新增 `CompressedParallelBuffRuntimeComponent` 和固定容量值类型 `CompressedParallelBuffLayerBuffer`。

Phase 3B 不接入运行时主流程。当前即使配置选择 `CompressedExpiryFrameList`，运行时仍走 EntityPerStack。后续 Phase 3C 才会设计 Add、Refresh、Remove、Expire、Query 与 Phase 2A EffectRequest Pipeline 的接入方式。

## 目标

FrameWork2 的 BuffSystem 只作为参考实现。迁移目标是吸收它的语义优点，但第一套必须继续保持 ECS、固定帧、确定性、回滚友好。

## 已迁移或正在迁移的优点

### ResetRuntimeBuffStackUpStrategy

FrameWork2 行为：

```csharp
buff.RunTimeData.RunTime = 0;
buff.RunTimeData.Ticks = 0;
```

第一套 Phase 1 对应：

```csharp
NormalBuffStackPolicy.ResetDurationOnly = 5
```

运行时行为：

- 不改变当前 `stack`
- 重置 `durationFrames`
- 重置 `remainingFrames`
- `elapsedFrames = 0`
- `ticks = 0`

使用方式：

```csharp
normalStackPolicy: NormalBuffStackPolicy.ResetDurationOnly
```

### 并行层刷新

FrameWork2 使用真实时间到期队列。第一套不迁移 `Time.time`，而是保留固定帧 `remainingFrames`。Phase 1 修正并行层刷新时的计时重置，使 `RefreshEarliest` 和 `RefreshAll` 的刷新语义更接近参考实现。

## 暂未迁移的优点

以下内容已在 Phase 2A 概念迁移到第一套 ECS BuffSystem：

- 生命周期 Effect 请求队列。
- 本帧末尾确定性 Flush。
- Remove Effect Flush 完成后再物理销毁 Runtime Entity。

Phase 2A 排序规则为：

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

`phaseOrder` 显式定义为 `Apply=0, Refresh=1, StackChanged=2, Tick=3, Remove=4`，不依赖 enum 原始整数值。

以下内容仍属于后续阶段：

- Composite Effect 纯 C# 版本。
- 稳定 int id 的策略扩展注册表。
- 并行 Buff 压缩运行时模式。

## 禁止迁移内容

以下内容不允许搬回第一套运行时：

- `BuffHandler : MonoBehaviour`
- `Update()` / `LateUpdate()`
- `GameObject` Target / Source
- `Time.time`
- `Time.deltaTime`
- `BuffEffect : ScriptableObject` 运行时执行
- `CompositeEffect : ScriptableObject` 运行时执行
- 字符串策略 ID 热路径
- `object` 事件装箱作为回滚关键状态

## 迁移示例

FrameWork2：

```csharp
BuffStackUpStrategyID = "ResetRuntimeBuffStackUpStrategy";
```

第一套：

```csharp
normalStackPolicy = NormalBuffStackPolicy.ResetDurationOnly;
```

说明：第一套不会重置 `RunTime`，而是重置固定帧字段 `elapsedFrames` 和 `ticks`，并刷新 `remainingFrames`。

