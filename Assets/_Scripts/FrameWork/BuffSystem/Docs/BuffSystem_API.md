# BuffSystem API

## Phase 3G-3D-A - Production compressed whitelist entry

`BuffSystemCore` now has an internal production factory for the controlled compressed parallel pilot:

```csharp
internal static BuffSystemCore CreateForProduction(
    IBuffDefinitionProvider definitionProvider,
    BuffEffectRegistry effectRegistry)
```

This factory uses the existing private constructor with `enableCompressedParallelRuntime = true` and a production whitelist containing only `configId = 991001`. Public constructors are unchanged and still keep the compressed gate closed with an empty whitelist.

`SimulationInitializer` now uses `CreateForProduction(definitionProvider, effectRegistry)` after creating the `BuffConfigDataLoader` definition provider and registering production effects. This does not change `IBuffSystem`, public query APIs, public constructors, or the event Effect hot path.

The pilot `BuffConfigData asset` is not created in this phase. It must be created later through Unity Editor under:

`Assets/Resources/_Scripts/FrameWork/BuffSystem/BuffConfigDataCollection/Debug_CompressedParallel_TickSmoke.asset`

## Phase 3G-2F-B - Production initializer injection

`SimulationInitializer` now creates the production BuffSystem with explicit dependencies:

```csharp
BuffConfigDataLoader definitionProvider = BuffConfigDataLoader.Instance;
definitionProvider.SetTickLength(_fixedDeltaTime);
definitionProvider.Init();

BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
BuffEffectRegistryBootstrap.RegisterProductionEffects(effectRegistry);

_buffSystem = new BuffSystemCore(definitionProvider, effectRegistry);
```

This uses the existing `BuffSystemCore(IBuffDefinitionProvider, BuffEffectRegistry)` constructor. No public API, public constructor, or `IBuffSystem` signature changed.

Runtime definitions still come from `BuffConfigDataLoader` and its Resources path. Runtime Effect execution still depends on `BuffEffectRegistry.Register`. This phase does not create `BuffConfigData asset`, does not modify `BuffEffectCatalogData.asset`, does not add a production whitelist entry, and does not open the compressed gate.

## Phase 3G-2F-A - Debug NoOp Tick Effect API 说明

本阶段只新增生产可注册的空 Tick Effect 与注册入口骨架，不修改 `IBuffSystem`、`BuffSystemCore` public constructor 或现有 public 查询 API。

新增内部 Effect：

```csharp
internal sealed class DebugNoOpTickEffect : BuffEffectExecutorBase
{
    public override void OnTick(in BuffEffectContext context)
    {
    }
}
```

新增内部注册入口：

```csharp
internal static class BuffEffectRegistryBootstrap
{
    internal const int DebugNoOpTickEffectId = 990101;

    internal static void RegisterProductionEffects(BuffEffectRegistry registry)
}
```

运行时真正执行 Effect 仍依赖 `BuffEffectRegistry.Register(effectId, executor)`。`BuffEffectCatalogData.asset` 仍只是 metadata / Inspector 选择辅助，不会自动创建 runtime executor。

Phase 3G-2F-B 已将 `RegisterProductionEffects` 接入 `SimulationInitializer`，所以生产初始化会注册 `DebugNoOpTickEffect`。当前仍未创建 `BuffConfigData asset`，未加入 production whitelist，compressed gate 仍未对生产 Buff 启用。

## IBuffSystem

`IBuffSystem` 是 BuffSystem 的主要对外接口。

```csharp
void Tick(World world, SimulationContext context);
void AddBuff(AddBuffCommand command);
void RemoveBuff(RemoveBuffCommand command);
void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent)
    where TEvent : struct, IGameEvent;
bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data);
IReadOnlyList<BuffViewData> GetBuffs(Entity target);
```

## Phase 3B - ParallelBuffStorageMode

Phase 3B 新增并行 Buff 存储模式配置入口：

```csharp
public enum ParallelBuffStorageMode
{
    EntityPerStack = 0,
    CompressedExpiryFrameList = 1
}
```

`EntityPerStack` 仍是默认值和当前唯一运行时行为：每一个并行层对应一个 Runtime Entity。

`CompressedExpiryFrameList` 目前只是预留配置入口和数据结构骨架，用于后续 Phase 3C 设计一个 Runtime Entity 内部管理多个并行层。Phase 3B 不接入 `Add / Refresh / Remove / Tick / Expire / Query` 主流程，`BuffSystemCore` 当前不会读取该字段。

`BuffConfigData -> BuffDefinition` 会传递 `ParallelStorageMode` 字段，但运行时行为不变。压缩模式设计继续使用固定帧字段，不使用 `Time.time`、`Time.deltaTime` 或 `float expiry`。

### Tick

作用：推进一帧 Buff 逻辑。通常由 `ECSBuffSystem` 在 ECS 固定帧中调用。

参数：

- `world`：当前 ECS World。
- `context`：固定帧上下文，包含帧号和回滚标记。

运行时行为：消费排队的增删请求，推进运行中 Buff 的 `elapsedFrames`、`remainingFrames` 和 `ticks`，并触发对应 Effect。

### AddBuff

作用：添加或刷新 Buff。

参数：

- `AddBuffCommand.Target`：目标实体，必须有效且存活。
- `AddBuffCommand.Source`：来源实体，无来源时为 `Entity.Invalid`。
- `AddBuffCommand.ConfigId`：Buff 配置编号，必须大于 0。
- `AddBuffCommand.Stack`：本次添加层数，小于等于 0 时会被修正为 1。

运行时行为：Tick 外调用会进入内部请求队列，在下一次 Buff Tick 消费。

示例：

```csharp
buffSystem.AddBuff(new AddBuffCommand(target, 1001, source, 1));
```

### RemoveBuff

作用：移除 Buff 层数或清空 Buff。

参数：

- `RemoveBuffCommand.Target`：目标实体。
- `RemoveBuffCommand.Source`：来源实体。
- `RemoveBuffCommand.ConfigId`：Buff 配置编号。
- `RemoveBuffCommand.StackCount`：移除层数。
- `RemoveBuffCommand.MatchAnySource`：是否忽略来源匹配。
- `RemoveBuffCommand.ClearAllStacks`：是否清空全部层数。

运行时行为：第一阶段不改变现有部分减层语义。当前普通 Buff 部分减层后会刷新 `remainingFrames` 为当前 `durationFrames`，该行为保留。

Phase 2A 后，完整移除 Runtime Buff 时会立即退出有效 Buff 查询语义，但物理销毁延迟到生命周期 Effect Flush 之后。`OnRemove` 使用移除前的 Runtime snapshot。

### Raise

作用：触发 ECS 逻辑事件，只响应 `TriggerType == EventTrigger` 且事件编号匹配的 Buff。

参数：

- `world`：当前 ECS World。
- `context`：固定帧上下文。
- `gameEvent`：实现 `IGameEvent` 的 struct 事件。

示例：

```csharp
AttackHitEvent e = new AttackHitEvent(context.frameNumber, 2001, attacker, target);
buffSystem.Raise(world, context, in e);
```

### TryGetBuff / GetBuffs

作用：为 View、UI 和调试面板读取 Buff 只读视图。

返回：

- `BuffViewData.RemainingFrames == -1` 表示永久 Buff。
- 并行 Buff 的视图会合并同目标、同来源、同配置的层数，并取最早到期层的剩余帧。

## API 变化

Phase 1 新增：

```csharp
NormalBuffStackPolicy.ResetDurationOnly = 5
```

旧枚举值顺序和整数值保持不变。

Phase 2A 无 public API 变更。`IBuffEffectExecutor`、`BuffEffectContext`、`IBuffEventEffectExecutor<TEvent>` 签名保持不变；变化仅发生在 `BuffSystemCore` 内部生命周期 Effect 调度时机。

## Phase 3F-8 - Compressed parallel API 可见面

### 配置字段

`ParallelBuffStorageMode` 已包含：

```csharp
public enum ParallelBuffStorageMode
{
    EntityPerStack = 0,
    CompressedExpiryFrameList = 1
}
```

`BuffConfigData.ParallelStorageMode` 和 `BuffDefinition.ParallelStorageMode` 用于声明并行 Buff 的存储模式。默认值仍为 `ParallelBuffStorageMode.EntityPerStack`。

`CompressedExpiryFrameList` 的 eligibility 条件为：

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
compressed gate == enabled
```

以下情况会 fallback 到 `EntityPerStack`：

```text
gate=false
EventTrigger parallel buff
Unlimited == true
MaxStack > CompressedParallelBuffLayerBuffer.Capacity
任何不满足 eligibility 的配置
```

### gate 与构造函数

正式 public constructor 默认 gate=false：

```csharp
new BuffSystemCore()
new BuffSystemCore(definitionProvider, effectRegistry)
```

因此当前正式运行时仍默认 `EntityPerStack`。`BuffSystemCore.CreateForCompressedParallelValidation(...)` 是 internal test-only factory，只用于 `BuffSystemCompressedParallelValidationRunner` 创建 gate=true 验证实例。业务代码不应使用 validation factory，也不应在 Phase 3G 前绕过 gate。

### public 查询 API

`TryGetBuff / GetBuffs` public API 不变。compressed 模式接入 ViewCache 后，对外仍返回 `BuffViewData`：

```text
compressed aggregate 只对外暴露一个 BuffViewData
Stack = active layerCount
duration RemainingFrames = min(expireFrame - currentFrame)
forever RemainingFrames = -1
RuntimeHandle = min(active layerRuntimeHandle)
不暴露每层详情
```

mixed duration / forever 不是常规配置路径；如果异常出现，按旧 `MergeViewData` 兼容语义倾向 `RemainingFrames = -1`。

注意：ViewData 口径不能和 Tick snapshot 口径混用。ViewData duration 使用 `expireFrame - currentFrame`；Tick snapshot 使用 `expireFrame - currentFrame + 1`。

## Phase 3G-1 - whitelist gate API 说明

Phase 3G-1 不修改 public API、`IBuffSystem`、public constructor 或 `BuffDefinition / BuffConfigData` 字段。

正式 public constructor 仍保持：

```csharp
new BuffSystemCore()
new BuffSystemCore(definitionProvider, effectRegistry)
```

生产路径下：

```text
global gate = false
config whitelist = empty
```

因此任何生产 Buff 默认仍走 `EntityPerStack`。即使某个 Buff 满足 `CompressedExpiryFrameList` eligibility，只要不在白名单中或 gate 未开启，也不会使用 compressed runtime。

internal `CreateForCompressedParallelValidation(...)` 会创建 gate=true 的测试实例，并使用 validation whitelist 保持 `BuffSystemCompressedParallelValidationRunner` 的 compressed path 可验证。该 factory 仍是 test-only，不应被业务代码调用。

