# BuffSystem Examples

## 初始化

```csharp
BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
effectRegistry.Register(1001, new ShieldEffect());
effectRegistry.Register(2001, new PoisonTickEffect());

IBuffDefinitionProvider definitionProvider = BuffConfigDataLoader.Instance;
ECSBuffSystem buffSystem = new ECSBuffSystem(definitionProvider, effectRegistry);

world.AddSystem(buffSystem);
```

## 添加 ResetDurationOnly Buff

`ResetDurationOnly` 适合“重复施加只刷新持续时间，不改变层数”的 Buff，例如护盾维持、状态续期。

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, 1001, source, 1));
```

配置侧：

```csharp
normalStackPolicy: NormalBuffStackPolicy.ResetDurationOnly
```

运行时结果：

- 当前 `stack` 不变。
- `remainingFrames` 回到配置持续帧。
- `elapsedFrames` 清零。
- `ticks` 清零。

## 生命周期 Effect Flush 示例

Phase 2A 后，生命周期 Effect 不在状态变化处立即执行，而是在本帧 BuffSystemCore 处理末尾统一 Flush。

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, 1001, source, 1));
```

同一帧内，Runtime 状态会先写入 ECS；随后生命周期 Effect 按以下顺序 Flush：

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

其中 `phaseOrder` 为：

```text
Apply = 0
Refresh = 1
StackChanged = 2
Tick = 3
Remove = 4
```

如果 Effect 在 Flush 期间再次调用：

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, 1002, source, 1));
buffSystem.RemoveBuff(new RemoveBuffCommand(target, 1001, source, 1));
```

这些命令不会在当前 Flush 中递归执行，而是进入 `_queuedCommands`，由下一次 `BuffSystemCore.Tick -> ConsumeQueuedCommands` 消费。

完整移除 Buff 时，Runtime 会立即退出 `TryGetBuff` / `GetBuffs` 的有效结果；`OnRemove` 仍使用移除前 Runtime snapshot，Flush 后才物理销毁 Runtime Entity。

## 添加并行 Buff

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, 2001, source, 3));
```

如果 `BuffInstanceType.parallel` 且 `ParallelBuffStackUpPolicy.RefreshEarliest`，系统会先刷新最早到期层，再追加剩余层。

## 固定帧命令方式

Tick 外需要可回放调度时，写入帧命令：

```csharp
commandBuffer.AddBuffAtFrame(
    frameNumber,
    new AddBuffCommand(target, 1001, source, 1));
```

移除：

```csharp
commandBuffer.RemoveBuffAtFrame(
    frameNumber,
    new RemoveBuffCommand(target, 1001, source, 1));
```

## 查询 Buff

```csharp
if (buffSystem.TryGetBuff(target, 1001, source, out BuffViewData data))
{
    int stack = data.Stack;
    int remainingFrames = data.RemainingFrames;
}
```

读取全部：

```csharp
IReadOnlyList<BuffViewData> buffs = buffSystem.GetBuffs(target);
```

## 编写 Tick Effect

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

注意：Effect 不要读取 Unity 真实时间，不要持有 GameObject。

