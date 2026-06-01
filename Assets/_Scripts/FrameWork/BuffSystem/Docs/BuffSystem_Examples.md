# BuffSystem Examples

## Phase 3G-3D-A - Debug_CompressedParallel_TickSmoke preparation

Production initialization now uses the internal production factory, which enables compressed parallel runtime only for the production whitelist. The current whitelist contains only the reserved pilot `configId = 991001`.

The pilot asset is still not created by code. Create it later through Unity Editor at:

`Assets/Resources/_Scripts/FrameWork/BuffSystem/BuffConfigDataCollection/Debug_CompressedParallel_TickSmoke.asset`

Recommended pilot fields:

- `ID = 991001`
- `Name = Debug_CompressedParallel_TickSmoke`
- `BuffType = parallel`
- `ParallelStorageMode = CompressedExpiryFrameList`
- `BuffTriggerType = Tick`
- `Unlimited = false`
- `MaxStack = 3`
- `Duration = 2.0`
- `TickTime = 1.0`
- `ParallelStackUpPolicy = Append`
- `ParallelStackDownPolicy = RemoveEarliest`
- `EffectId = 990101`
- `EventIds = empty`

Non-whitelisted Buffs still use `EntityPerStack`. If the pilot needs to be rolled back, remove `991001` from the production whitelist or return `SimulationInitializer` to the public constructor path, then rebuild the World / restart the scene.

## Phase 3G-2F-C - BuffConfigDataLoader 场景挂载

`SimulationInitializer` 会在创建 `World` 前检查 `BuffConfigDataLoader.Instance`。如果场景没有挂载 loader，初始化会输出明确错误、禁用 `SimulationInitializer` 并停止，避免空引用和半初始化状态。

推荐在 Unity Editor 中手动操作：

```text
1. 打开 Assets/_Scenes/Scene.unity
2. 选中 Bootstrap GameObject
3. Add Component -> BuffConfigDataLoader
4. 保存场景
5. 确认场景中只有一个 BuffConfigDataLoader
```

本阶段没有创建试点 `BuffConfigData asset`，没有加入 production whitelist，也没有打开 compressed gate。

## Phase 3G-2F-B - Production initializer 注入示例

生产初始化入口现在通过显式依赖创建 BuffSystem：

```csharp
BuffConfigDataLoader definitionProvider = BuffConfigDataLoader.Instance;
definitionProvider.SetTickLength(_fixedDeltaTime);
definitionProvider.Init();

BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
BuffEffectRegistryBootstrap.RegisterProductionEffects(effectRegistry);

_buffSystem = new BuffSystemCore(definitionProvider, effectRegistry);
```

这只让生产路径具备加载 Resources BuffConfigData 和注册生产 Effect 的能力。本阶段没有创建试点 `BuffConfigData asset`，没有加入 production whitelist，也没有打开 compressed gate。

## Phase 3G-2F-A - Debug NoOp Tick Effect 示例

本阶段新增的 `DebugNoOpTickEffect` 是生产 smoke test 用空 Effect。它只覆盖 `OnTick`，不写 gameplay state，不调用 Add / Remove，不依赖 GameObject、MonoBehaviour 或 Unity Time API。

```csharp
BuffEffectRegistry registry = new BuffEffectRegistry();
BuffEffectRegistryBootstrap.RegisterProductionEffects(registry);
```

注册后，`EffectId = 990101` 可供后续 `Debug_CompressedParallel_TickSmoke` 试点 Buff 引用。Phase 3G-2F-B 已将 bootstrap 接入 `SimulationInitializer`，但当前仍没有创建试点 `BuffConfigData asset`，没有加入 production whitelist，compressed gate 仍未对生产 Buff 启用。

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

## Phase 3F-8 - Compressed parallel 配置与查询示例

以下示例只说明 `CompressedExpiryFrameList` 的配置与验证语义，不代表当前正式运行时已经全局启用 compressed。正式 public constructor 路径 gate 默认关闭，当前正式运行时仍默认 `EntityPerStack`。业务代码不应使用 validation factory。

### eligible Tick parallel buff 配置

```csharp
BuffDefinition compressedPoison = new BuffDefinition(
    configId: 3001,
    name: "Compressed Poison",
    priority: 0,
    maxStack: 8,
    unlimited: false,
    isForever: false,
    durationFrames: 120,
    tickIntervalFrames: 30,
    durationExtendFramesPerStack: 0,
    triggerType: BuffTriggerType.Tick,
    buffType: BuffInstanceType.parallel,
    normalStackPolicy: NormalBuffStackPolicy.AddStackOnly,
    parallelStackUpPolicy: ParallelBuffStackUpPolicy.Append,
    parallelStackDownPolicy: ParallelBuffStackDownPolicy.RemoveEarliest,
    effectId: 3001,
    eventIds: null,
    parallelStorageMode: ParallelBuffStorageMode.CompressedExpiryFrameList);
```

该配置只有在 compressed gate 被开启时才可能走 compressed runtime。正式运行时 gate 默认关闭，因此会继续走 `EntityPerStack`。

### fallback 示例

以下配置即使声明 `CompressedExpiryFrameList`，也会 fallback 到 `EntityPerStack`：

```csharp
// EventTrigger parallel buff fallback
triggerType: BuffTriggerType.EventTrigger

// Unlimited fallback
unlimited: true

// MaxStack > Capacity fallback
maxStack: CompressedParallelBuffLayerBuffer.Capacity + 1
```

完整 fallback 条件：

```text
gate=false
EventTrigger parallel buff
Unlimited == true
MaxStack > CompressedParallelBuffLayerBuffer.Capacity
任何不满足 eligibility 的配置
```

### aggregate ViewData 读取

```csharp
if (buffSystem.TryGetBuff(target, 3001, source, out BuffViewData view))
{
    int stack = view.Stack;
    int remainingFrames = view.RemainingFrames;
    int runtimeHandle = view.RuntimeHandle;
}
```

compressed 模式对外只返回一个 aggregate `BuffViewData`：

```text
Stack = active layerCount
duration RemainingFrames = min(expireFrame - currentFrame)
forever RemainingFrames = -1
RuntimeHandle = min(active layerRuntimeHandle)
```

不会通过 public `TryGetBuff / GetBuffs` 暴露每层详情。ViewData 口径不要和 Tick snapshot 口径混用；Tick snapshot duration 使用 `expireFrame - currentFrame + 1`。

