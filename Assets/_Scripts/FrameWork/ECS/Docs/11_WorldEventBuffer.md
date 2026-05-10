# WorldEventBuffer 使用说明

## 1. 定位

`WorldEventBuffer` 是 ECS 逻辑层向表现层、UI、音效层输出一次性结果的通道。

它用于记录某一逻辑帧中发生过的事件，例如：

```text
受到伤害
Entity 死亡
技能命中
生产完成
播放一次性特效
刷新一次性 UI 提示
```

它不替代 Component，也不替代 SimulationFrameCommand。

```text
Component：长期世界状态，例如 Position、Health、Velocity。
SimulationFrameCommand：外部输入到 ECS 的命令，例如创建实体、设置组件、销毁实体。
WorldEvent：ECS 逻辑执行后的结果，例如受击、死亡、命中。
```

## 2. 核心接口

`World` 暴露以下 API：

```csharp
public void AddWorldEvent<T>(T worldEvent) where T : struct, IWorldEvent;
public IReadOnlyList<T> GetWorldEvents<T>() where T : struct, IWorldEvent;
public void ClearWorldEvents();
public void ClearWorldEventsBeforeFrame(int frameNumber);
public int WorldEventCount { get; }
```

事件类型需要实现：

```csharp
public interface IWorldEvent
{
    int frameNumber { get; }
}
```

## 3. 内置事件

当前提供两个基础事件：

```csharp
public readonly struct DamageWorldEvent : IWorldEvent
```

用于记录一次伤害结果，包含：

```text
frameNumber
source
target
amount
remainingHealth
```

```csharp
public readonly struct EntityDeadWorldEvent : IWorldEvent
```

用于记录 Entity 首次进入死亡状态。

## 4. DamageResolveSystem 接入

`DamageResolveSystem` 现在会在伤害成功应用后写入：

```csharp
World.AddWorldEvent(new DamageWorldEvent(frameNumber, source, target, damage, remainingHealth));
```

如果目标生命值归零，并且此前没有 `DeadTagComponent`，则会额外写入：

```csharp
World.AddWorldEvent(new EntityDeadWorldEvent(frameNumber, target));
```

这意味着表现层可以在逻辑 Tick 后读取事件，并播放飘字、受击音效、死亡动画等反馈。

## 5. 清理时机

当前 `WorldEventBuffer` 不会在 `World.Tick` 内自动清理。

推荐流程是：

```text
World.Tick(context)
    ↓
表现层 / UI / 音效层读取 WorldEvent
    ↓
world.ClearWorldEvents()
```

这样可以避免逻辑帧末尾自动清理导致表现层还没来得及读取事件。

如果需要保留多帧事件，可以使用：

```csharp
world.ClearWorldEventsBeforeFrame(frameNumber);
```

它会清理 `frameNumber` 之前的事件，保留当前帧和之后的事件。

## 6. 注意事项

1. `WorldEvent` 是一次性结果，不应该作为长期状态使用。
2. 事件可以在 System.Tick 中写入。
3. 事件写入不会改变 Entity、Component、ArcheType 或 QueryCache。
4. 表现层读取事件后应主动清理。
5. 未来接入回滚时，逻辑事件可以在重放中重新生成，但播放音效、粒子等表现行为需要避免重复播放。
