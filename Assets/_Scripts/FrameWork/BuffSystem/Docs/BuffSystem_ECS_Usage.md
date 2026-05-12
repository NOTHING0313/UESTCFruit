# BuffSystem ECS 使用指南

## 初始化

本地固定帧模式推荐使用 `World + ECSBuffSystem`：

```csharp
BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
effectRegistry.Register(1001, new PoisonTickEffect());
effectRegistry.Register(2001, new ThornEventEffect());

IBuffDefinitionProvider definitionProvider = BuffConfigDataLoader.Instance;
ECSBuffSystem buffSystem = new ECSBuffSystem(definitionProvider, effectRegistry);

World world = new World();
world.AddSystem(buffSystem);
```

`BuffConfigData` 只负责编辑器配置，进入运行时后会转换成 `BuffDefinition`。纯运行时结构不依赖 Odin、Unity 资产或表现层对象。

## AddBuff / RemoveBuff

直接调用适合同一逻辑流程内排队，由下一次 `ECSBuffSystem.Tick` 消费：

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, configId, source, stack));
buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, stackCount));
```

Tick 外需要回滚重放时写入帧命令：

```csharp
commandBuffer.AddBuffAtFrame(frameNumber, new AddBuffCommand(target, configId, source, 1));
commandBuffer.RemoveBuffAtFrame(frameNumber, new RemoveBuffCommand(target, configId, source, 1));
```

清除全部层数：

```csharp
buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, source, clearAllStacks: true));
```

任意来源移除：

```csharp
buffSystem.RemoveBuff(new RemoveBuffCommand(target, configId, matchAnySource: true));
```

## Raise IGameEvent

事件必须是 `struct`，实现 `IGameEvent`，并使用 `Entity` 表达攻击者、目标、来源等身份。

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

AttackHitEvent e = new AttackHitEvent(context.frameNumber, 2001, attacker, target);
buffSystem.Raise(world, context, in e);
```

Tick 外写入帧命令：

```csharp
commandBuffer.RaiseBuffEventAtFrame(buffSystem, in e);
```

只有 `TriggerType = EventTrigger`、`EventIds` 包含事件编号、且 Effect 实现了对应 `IBuffEventEffectExecutor<TEvent>` 的 Buff 会响应。

## Tick Effect

```csharp
public sealed class PoisonTickEffect : BuffEffectExecutorBase
{
    public override void OnTick(in BuffEffectContext context)
    {
        if (!context.World.HasComponent<HealthComponent>(context.Runtime.target))
            return;

        ref HealthComponent health = ref context.World.GetComponent<HealthComponent>(context.Runtime.target);
        health.current -= context.Runtime.stack;
    }
}
```

Tick Effect 的触发间隔来自 `BuffConfigData.TickTime` 转换后的固定帧数。

## Event Effect

```csharp
public sealed class ThornEventEffect :
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

        ref HealthComponent health = ref context.World.GetComponent<HealthComponent>(e.Attacker);
        health.current -= context.Runtime.stack;
    }
}
```

`ShouldTrigger` 只做过滤，不修改 ECS 状态。需要回滚的结果必须写入 ECS Component。

## 表现层读取

表现层不要直接修改 Runtime Buff。推荐读取：

- `buffSystem.TryGetBuff(target, configId, source, out BuffViewData data)`
- `buffSystem.GetBuffs(target)`
- 项目现有 `WorldEvent` 或表现层事件

`BuffViewData.RemainingFrames` 为 `-1` 表示永久 Buff。并行 Buff 的 View 会合并同目标、同来源、同配置的层数，并取最早到期层的剩余帧数。
