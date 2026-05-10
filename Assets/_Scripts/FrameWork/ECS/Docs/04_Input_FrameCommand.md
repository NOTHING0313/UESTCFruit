# 04. 输入快照与帧指令

## 1. 输入处理目标

输入不直接驱动逻辑，而是先转换为可缓存、可传输、可回放的 `PlayerInputSnapshot`：

```text
UnityInputAdapter.SampleInput()
    ↓
UnityInputAdapter.CollectSnapshot(frameNumber)
    ↓
InputSnapshotBuffer.SetInput(snapshot)
    ↓
WorldInputApplier.ApplyInputToWorld(frameNumber)
    ↓
PlayerInputComponent
    ↓
System.Tick
```

## 2. InputSnapshotBuffer

`InputSnapshotBuffer` 的存储结构是：

```text
frameNumber
    playerID
        PlayerInputSnapshot
```

它用于保存输入历史。未来接入帧同步、回放或回滚时，可以根据 frameNumber 重新读取输入。

## 3. PlayerInputComponent

`PlayerInputComponent` 是输入快照在 ECS World 中的组件投影。System 不应该直接读取 Unity 输入设备，而应该读取该组件。

## 4. SimulationFrameCommandBuffer

Tick 外部的修改请求应该记录为帧指令：

```csharp
commandBuffer.SetComponentAtFrame(120, entity, new MoveSpeedComponent(5f));
commandBuffer.DestroyEntityAtFrame(180, entity);
```

指令支持两个执行时机：

| Timing | 含义 |
|---|---|
| `BeforeTick` | World.Tick 前执行，本帧 System 可以看到结果 |
| `AfterTick` | World.Tick 后执行，本帧 System 看不到，下帧可见 |

## 5. SimulationFrameCommandScheduler

普通外部调用者通常不知道当前准确帧号，所以应使用 Scheduler：

```csharp
scheduler.SetComponentNextFrameEnd(entity, new MoveSpeedComponent(5f));
scheduler.DestroyEntityNextFrameEnd(entity);
```

`CurrentFrameEndOrNextFrameEnd` 系列 API 表示：

- 如果当前正在 Tick，则调度到当前帧 AfterTick。
- 如果当前不在 Tick，则调度到下一帧 AfterTick。

## 6. 使用规则

- System Tick 内部产生的结构变化：直接调用 `World.SetComponent / RemoveComponent / DestroyEntity`，交给 `StructuralChangeBuffer`。
- Tick 外部的输入、UI、网络、剧情指令：通过 `InputSnapshotBuffer` 或 `SimulationFrameCommandBuffer` 按帧记录。
- 帧指令不要立刻消费删除，未来回放 / 回滚需要保留最近一段历史。
