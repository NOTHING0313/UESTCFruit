# BuffSystem Overview

## 基础使用流程示例

以下示例展示 ECS BuffSystem 的最小接入流程。`BuffConfigData` 可以作为 Unity Authoring 数据来源，运行时应通过 `BuffConfigDataLoader` 或其他 `IBuffDefinitionProvider` 转换/提供确定性的 `BuffDefinition`，Buff 逻辑本身只依赖 ECS `World`、`Entity` 和固定帧上下文。

```csharp
BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
effectRegistry.Register(1001, new ShieldEffect());

IBuffDefinitionProvider definitionProvider = BuffConfigDataLoader.Instance;
ECSBuffSystem buffSystem = new ECSBuffSystem(definitionProvider, effectRegistry);
world.AddSystem(buffSystem);
```

添加 Buff 时使用 `Entity` 表达目标和来源，不传入 `GameObject`：

```csharp
Entity target = playerEntity;
Entity source = casterEntity;

buffSystem.AddBuff(new AddBuffCommand(target, configId: 1001, source, stack: 1));
```

表现层、UI 或调试面板读取 Buff 时使用只读查询：

```csharp
if (buffSystem.TryGetBuff(target, configId: 1001, source, out BuffViewData data))
{
    int stack = data.Stack;
    int remainingFrames = data.RemainingFrames;
}

IReadOnlyList<BuffViewData> allBuffs = buffSystem.GetBuffs(target);
```

BuffSystem 的生命周期由固定帧推进。系统读取 `SimulationContext.frameNumber` 和运行时组件中的固定帧字段，例如 `remainingFrames`、`elapsedFrames`、`ticks`；不要使用 `Time.time` 或 `Time.deltaTime` 作为运行时 Buff 计时来源。

## 目标

BuffSystem 是第一套 FrameWork 中围绕 ECS、固定帧和确定性模拟演进的 Buff 运行时系统。运行时 Buff 目标、来源和 Buff 实例都使用 ECS `Entity` 表达，不依赖 `GameObject`、`MonoBehaviour`、`Time.time` 或 `Time.deltaTime`。

## 核心职责

- 接收 `AddBuffCommand` 和 `RemoveBuffCommand`。
- 在固定帧 `ECSBuffSystem.Tick` 中消费 Buff 请求。
- 使用 `BuffRuntimeComponent` 保存运行时状态。
- 使用 `BuffDefinition` 保存确定性运行时配置。
- 使用 `BuffEffectRegistry` 查找纯 C# Effect 执行器。
- 使用 `IGameEvent` 和 `IBuffEventEffectExecutor<TEvent>` 支持确定性事件触发。
- 通过 `BuffViewData` 向 View、UI 和调试面板提供只读状态。

## 运行时数据流

```text
SimulateRunner
-> World.Tick
-> ECSBuffSystem.Tick
-> BuffSystemCore.Tick
-> Consume Add/Remove Request
-> Tick Runtime Buff
-> Run Buff Effect
-> Update BuffViewData cache when queried
```

## 回滚友好约束

- 时间使用固定帧字段，例如 `SimulationContext.frameNumber`、`remainingFrames`、`elapsedFrames` 和 `ticks`。
- 运行时 Effect 必须是纯 C# 执行器。
- 需要回滚的状态必须写入 ECS Component 或可被 ECS Snapshot 捕获的数据。
- 表现层播放、GameObject 绑定、对象池、UI 图标不属于 Buff 逻辑快照。

## Phase 1 变化

Phase 1 新增普通 Buff 叠层策略 `NormalBuffStackPolicy.ResetDurationOnly = 5`。它用于等价迁移 FrameWork2 的 `ResetRuntimeBuffStackUpStrategy`：重复添加同一 Buff 时，不改变当前层数，只重置持续帧、剩余帧、已运行帧和 Tick 次数。

同时，刷新持续时间的策略现在会统一重置 `elapsedFrames` 和 `ticks`，避免刷新后沿用旧 Tick 计数导致周期效果提前触发。
