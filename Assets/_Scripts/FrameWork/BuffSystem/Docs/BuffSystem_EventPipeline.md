# BuffSystem Event Pipeline

## 作用

Event Pipeline 让 Buff 响应 ECS 逻辑事件。事件必须是 `struct`，并实现 `IGameEvent`，避免在回滚关键路径中使用表现层对象。

## IGameEvent

事件应至少提供：

- `FrameNumber`：事件发生的逻辑帧。
- `EventId`：稳定整数事件编号。

示例：

```csharp
public readonly struct AttackHitEvent : IGameEvent
{
    public int FrameNumber { get; }
    public int EventId { get; }
    public Entity Attacker { get; }
    public Entity Target { get; }

    public AttackHitEvent(int frameNumber, int eventId, Entity attacker, Entity target)
    {
        FrameNumber = frameNumber;
        EventId = eventId;
        Attacker = attacker;
        Target = target;
    }
}
```

## IBuffEventEffectExecutor

```csharp
public interface IBuffEventEffectExecutor<TEvent> : IBuffEventEffectExecutor
    where TEvent : struct, IGameEvent
{
    bool ShouldTrigger(in BuffEffectContext context, in TEvent gameEvent);
    void OnEvent(in BuffEffectContext context, in TEvent gameEvent);
}
```

运行时行为：

- `ShouldTrigger` 只做过滤，不修改 ECS 状态。
- `OnEvent` 执行真正逻辑。
- Buff 配置必须是 `TriggerType == EventTrigger`。
- `BuffDefinition.EventIds` 必须包含事件 `EventId`。

## 使用样例

```csharp
public sealed class ThornEffect :
    BuffEffectExecutorBase,
    IBuffEventEffectExecutor<AttackHitEvent>
{
    public bool ShouldTrigger(in BuffEffectContext context, in AttackHitEvent e)
    {
        return context.Runtime.target == e.Target;
    }

    public void OnEvent(in BuffEffectContext context, in AttackHitEvent e)
    {
        if (!context.World.HasComponent<HealthComponent>(e.Attacker))
            return;

        ref HealthComponent health =
            ref context.World.GetComponent<HealthComponent>(e.Attacker);

        health.current -= context.Runtime.stack;
    }
}
```

## 回滚说明

事件结果必须由以下数据决定：

- `IGameEvent` 字段
- `BuffRuntimeComponent`
- `BuffDefinition`
- 当前 ECS World 组件状态
- 当前注册的纯 C# Effect

不要在事件 Effect 中读取真实时间、Unity 输入、GameObject 或全局随机状态。

