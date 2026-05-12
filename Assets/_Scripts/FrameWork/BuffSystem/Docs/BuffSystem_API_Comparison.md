# BuffSystem API 对比

## Buff 增删

旧版 `BuffHandler.AddBuff<TBuff>` 改为：

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));
```

Tick 外需要确定性调度时改为：

```csharp
commandBuffer.AddBuffAtFrame(frameNumber, new AddBuffCommand(target, configId, source, stack));
```

旧版 `RemoveBuffStack`、`ClearBuff` 改为：

```csharp
buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, stackCount));
buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, clearAllStacks: true));
```

## Buff 查询

旧版 `ContainBuff/GetBuff/GetBuffs` 改为：

```csharp
bool hasBuff = buffSystem.TryGetBuff(target, configId, source, out BuffViewData data);
IReadOnlyList<BuffViewData> buffs = buffSystem.GetBuffs(target);
```

`BuffRuntimeData` 和 `ParallelBuffRunTimeData` 已由 `BuffRuntimeComponent` 统一承载。

## Effect

旧版 `BuffEffect : ScriptableObject` 改为纯 C# 执行器：

```csharp
public sealed class MyEffect : BuffEffectExecutorBase
{
    public override void OnApply(in BuffEffectContext context) { }
    public override void OnRemove(in BuffEffectContext context) { }
}
```

旧版测试 `SpeedUpEffect` 已移出运行时链路。运行时通过 `BuffEffectRegistry.Register(effectId, effect)` 注册执行器，配置中只保存 `EffectId`。

## 事件响应

旧版链路：

```csharp
BuffHandler.Raise<TEvent>(in e)
EventRouter / EventListener<TEvent>
BuffEffect.OnEvent(in BuffContext ctx)
```

新版链路：

```csharp
public readonly struct AttackHitEvent : IGameEvent
{
    public int FrameNumber { get; }
    public int EventId { get; }
    public Entity Attacker { get; }
    public Entity Target { get; }
}

public sealed class ThornEffect :
    BuffEffectExecutorBase,
    IBuffEventEffectExecutor<AttackHitEvent>
{
    public bool ShouldTrigger(in BuffEffectContext context, in AttackHitEvent e) => context.Runtime.target == e.Target;
    public void OnEvent(in BuffEffectContext context, in AttackHitEvent e) { }
}

buffSystem.Raise(world, context, in attackHitEvent);
```

迁移要求：

- 事件必须是 `struct`，并实现 `IGameEvent`。
- 攻击者、目标、来源等身份必须使用 `Entity`。
- `BuffConfigData.TriggerType` 必须设为 `EventTrigger`。
- `BuffConfigData.EventIds` 必须包含事件的 `EventId`。
- `ShouldTrigger` 只做过滤，不修改 ECS 状态。

Tick 外或回滚命令流使用：

```csharp
commandBuffer.RaiseBuffEventAtFrame(buffSystem, in attackHitEvent);
```

该命令显式接收 `IBuffSystem`，不访问单例。
