# BuffSystem Effect Pipeline

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

