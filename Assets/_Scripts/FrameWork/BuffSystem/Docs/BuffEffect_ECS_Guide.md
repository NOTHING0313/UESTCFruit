# ECS Buff Effect 使用指南

## 基本规则

新版 Effect 是纯 C# 执行器，不继承 `ScriptableObject`，不访问 `MonoBehaviour`、`GameObject`、`Transform` 或 `Time`。

- Effect 不保存会影响逻辑结果的运行时私有状态。
- 需要回滚的状态必须写入 ECS Component。
- 所有客户端必须用相同 `effect id` 注册相同执行器。
- 表现反馈不要在 Effect 中直接播放，应写入 WorldEvent 或项目现有表现层事件。

## 生命周期 Effect

推荐继承 `BuffEffectExecutorBase`，只重写需要的生命周期。

```csharp
using BuffSystem;
using ECSFrameWork;

public sealed class MoveSpeedBuffEffect : BuffEffectExecutorBase
{
    public override void OnStackChanged(in BuffEffectContext context, int delta)
    {
        if (!context.World.HasComponent<StatComponent>(context.Runtime.target))
            return;

        ref StatComponent stat = ref context.World.GetComponent<StatComponent>(context.Runtime.target);
        stat.moveSpeed += delta;
    }
}
```

生命周期回调含义：

- `OnApply`：runtime Buff 创建时调用。
- `OnRefresh`：同目标、同来源、同配置 Buff 被刷新时调用。
- `OnStackChanged`：层数变化时调用，`delta` 可正可负。
- `OnTick`：到达固定帧 Tick 间隔时调用。
- `OnRemove`：runtime Buff 被移除或过期时调用。

## 事件 Effect

事件响应使用 `IBuffEventEffectExecutor<TEvent>`，不要把事件参数转成 `object`。

```csharp
using BuffSystem;
using ECSFrameWork;

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

public sealed class ThornEffect :
    BuffEffectExecutorBase,
    IBuffEventEffectExecutor<AttackHitEvent>
{
    public bool ShouldTrigger(in BuffEffectContext context, in AttackHitEvent gameEvent)
    {
        return context.Runtime.target == gameEvent.Target;
    }

    public void OnEvent(in BuffEffectContext context, in AttackHitEvent gameEvent)
    {
        if (!context.World.HasComponent<HealthComponent>(gameEvent.Attacker))
            return;

        ref HealthComponent health = ref context.World.GetComponent<HealthComponent>(gameEvent.Attacker);
        health.current -= context.Runtime.stack;
    }
}
```

`ShouldTrigger` 必须无副作用，只做目标、来源、伤害类型等条件判断。真正的 ECS 状态修改放在 `OnEvent`。

## 注册与配置

```csharp
BuffEffectRegistry registry = new BuffEffectRegistry();
registry.Register(1001, new ThornEffect());

ECSBuffSystem buffSystem = new ECSBuffSystem(definitionProvider, registry);
world.AddSystem(buffSystem);
```

`BuffConfigData` 中需要配置：

- `Trigger Type = EventTrigger`
- `Effect ID = 1001`
- `Event IDs` 包含事件的 `EventId`

如果 `EventTrigger` Buff 没有配置 `EventIds`，运行时不会响应任何事件，并会在编辑器校验阶段给出警告。

## 策划目录

可以创建 `BuffEffectCatalogData` 维护 Effect 目录，策划在 `BuffConfigData` 中通过显示名选择 `EffectId`。该目录只服务 Inspector 展示和校验，运行时不会被 `BuffDefinition` 或 `BuffEffectRegistry` 读取。

可以创建 `BuffEventCatalogData` 维护事件目录，策划在 `EventTrigger` Buff 中通过显示名选择 `EventIds`。运行时仍只保存整数数组 `int[] EventIds`，不会依赖 `EventKey`、显示名或事件说明。

## 触发事件

固定帧系统内优先直接调用：

```csharp
AttackHitEvent e = new AttackHitEvent(context.frameNumber, 2001, attacker, target);
buffSystem.Raise(world, context, in e);
```

Tick 外或回放队列可以写入帧命令：

```csharp
AttackHitEvent e = new AttackHitEvent(targetFrame, 2001, attacker, target);
commandBuffer.RaiseBuffEventAtFrame(buffSystem, in e);
```

事件命令不会查找单例，调用方必须显式传入当前 `IBuffSystem`。

## 回滚要求

事件结果必须只由以下数据决定：

- `IGameEvent.FrameNumber`
- `IGameEvent.EventId`
- 事件结构体中的 ECS 数据
- `BuffRuntimeComponent`
- `BuffDefinition`
- 当前 ECS World 组件状态
- 当前注册的 Effect 实现

不要在事件 Effect 中读取真实时间、Unity 输入、随机全局状态或表现层对象。
