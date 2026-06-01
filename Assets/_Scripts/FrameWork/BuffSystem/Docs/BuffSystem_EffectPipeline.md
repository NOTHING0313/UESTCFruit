# BuffSystem Effect Pipeline

## Phase 3C-2 - Dormant compressed helpers

Phase 3C-2 预埋压缩并行 Buff 的 Add / Refresh / Remove helper，但不接入主流程，也不扩展 `BuffEffectRequest`。这些 helper 复用现有 `BuffRuntimeComponent` 单层 snapshot 表达层级 Effect：`stack = 1`，`runtimeHandle = layerRuntimeHandle`，`remainingFrames` 由 `expireFrame - frameNumber` 计算，`elapsedFrames` 与 `ticks` 来自对应 layer。

由于 `ShouldUseCompressedParallel` 仍返回 false，本阶段不会产生压缩层 EffectRequest，Phase 2A 生命周期排序和事件型 Effect 热路径保持不变。

## Phase 3C-1 - 压缩并行 Buff 准备阶段

Phase 3C-1 不扩展 `BuffEffectRequest`，不修改 `BuffEffectContext`，也不改变 `IBuffEffectExecutor` 或 `IBuffEventEffectExecutor<TEvent>`。`CompressedExpiryFrameList` 仍未接入生命周期 EffectRequest Pipeline。

本阶段只允许为后续压缩并行 Buff 接入预留 helper 和 lookup cache。Add、Refresh、Remove、Tick、Query 和 EffectRequest 主流程仍完全使用当前 EntityPerStack 路径。

## 作用

Effect Pipeline 负责在 Buff 生命周期节点执行纯 C# Effect。运行时 Effect 不允许依赖 `ScriptableObject`、`GameObject`、`MonoBehaviour` 或 Unity 真实时间。

## 核心接口

```csharp
public interface IBuffEffectExecutor
{
    void OnApply(in BuffEffectContext context);
    void OnRefresh(in BuffEffectContext context);
    void OnStackChanged(in BuffEffectContext context, int delta);
    void OnTick(in BuffEffectContext context);
    void OnRemove(in BuffEffectContext context);
}
```

## BuffEffectContext 字段

- `World`：当前 ECS World。
- `SimulationContext`：固定帧上下文。
- `BuffEntity`：运行时 Buff Entity。
- `Runtime`：当前 `BuffRuntimeComponent` 快照。
- `Definition`：当前 `BuffDefinition`。

## 运行时行为

- 新建 Buff 时触发 `OnApply`，随后触发 `OnStackChanged`。
- 重复添加并刷新时触发 `OnRefresh`。
- 层数变化时触发 `OnStackChanged`。
- Tick 间隔到达时触发 `OnTick`。
- Buff 移除时触发 `OnRemove`。

## Phase 2A 生命周期队列

Phase 2A 将生命周期 Effect 从立即执行改为本帧末尾统一 Flush。覆盖范围仅包括：

- `OnApply`
- `OnRefresh`
- `OnStackChanged`
- `OnTick`
- `OnRemove`

事件型 `IBuffEventEffectExecutor<TEvent>` 保持原泛型热路径，不进入生命周期 Effect 队列。

生命周期 Effect 的确定性排序规则固定为：

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

`phaseOrder` 使用显式映射，不依赖 enum 原始整数值：

```text
Apply = 0
Refresh = 1
StackChanged = 2
Tick = 3
Remove = 4
```

Remove 采用延迟物理销毁：`QueueRemoveRuntimeEntity` 后 Runtime 立即退出有效 Buff 语义，`TryGetBuff`、`GetBuffs`、runtime lookup、事件索引、Tick 遍历、刷新/移除查找、`CountStacks`、`CollectRuntimeEntities` 和 `TryGetFirstRuntimeEntity` 都不会再把它当作有效 Buff；但 `OnRemove` Flush 时仍使用移除前的 `BuffRuntimeComponent` snapshot 构造上下文。`OnRemove` Flush 完成后才统一 `DestroyEntity`。

Flush 期间 Effect 内新产生的 `AddBuff` / `RemoveBuff` 不递归处理。它们进入 `_queuedCommands`，由下一次 `BuffSystemCore.Tick -> ConsumeQueuedCommands` 消费。

本阶段只修正刷新持续时间时的计时字段：

- `RefreshDuration`
- `AddStackAndRefreshDuration`
- `ResetDurationOnly`
- 并行 Buff 刷新某层持续时间时

这些路径都会重置：

```csharp
elapsedFrames = 0;
ticks = 0;
```

## 使用样例

```csharp
public sealed class PoisonTickEffect : BuffEffectExecutorBase
{
    public override void OnTick(in BuffEffectContext context)
    {
        if (!context.World.HasComponent<HealthComponent>(context.Runtime.target))
            return;

        ref HealthComponent health =
            ref context.World.GetComponent<HealthComponent>(context.Runtime.target);

        health.current -= context.Runtime.stack;
    }
}
```

## 回滚说明

Effect 修改的任何逻辑结果都必须写入 ECS Component。表现层事件应通过 WorldEvent 或 ViewBridge 处理，不应直接在 Effect 中播放。

## Phase 3F-8 - Compressed layer 生命周期 Effect 口径

compressed layer 生命周期仍进入 Phase 2A EffectRequest Pipeline。排序规则仍是：

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

`phaseOrder` 仍为：

```text
Apply = 0
Refresh = 1
StackChanged = 2
Tick = 3
Remove = 4
```

### Tick / Remove snapshot

compressed duration layer 的 Effect snapshot 口径为：

```text
Tick snapshot RemainingFrames = expireFrame - currentFrame + 1
Remove snapshot RemainingFrames = 0
forever snapshot remainingFrames = 0
```

新建 compressed layer 创建当帧不 Tick：

```text
duration=1：F1 Apply，F2 Tick + Remove
duration=2：F1 Apply，F2 Tick，F3 Tick + Remove
```

ViewData duration 使用 `expireFrame - currentFrame`，Tick snapshot 使用 `expireFrame - currentFrame + 1`，两者不能混用。forever ViewData 使用 `RemainingFrames = -1`，但 forever runtime / effect snapshot 中 `remainingFrames` 可以保持 0。

### PendingRemove / Destroy

最后一层 Remove / Expire / ClearAll 后，compressed runtime container 进入 pending remove。pending remove 使用 `compressedRuntimeHandle`，因为删除目标是 container entity。

container pending remove 不额外触发聚合 Remove Effect。layer Remove 使用 `layerRuntimeHandle`，生命周期回调仍来自单层 snapshot。pending remove 后 `TryGetBuff / GetBuffs` 不显示，Destroy 前会 defensive 清理 `_compressedRuntimeEntityByKey`。

### ReplaceEarliestWhenFull

`ReplaceEarliestWhenFull` 满层时，状态层面移除最早层并追加新层。新层生成新的 `layerId / layerRuntimeHandle`，未替换层 identity 保持。

同帧 Replace 不假设 Remove callback 一定早于 Apply callback。Effect Flush 顺序仍由 Phase 2A phaseOrder 决定，测试和业务逻辑都不应依赖“Remove 先于 Apply”的同帧回调顺序。

