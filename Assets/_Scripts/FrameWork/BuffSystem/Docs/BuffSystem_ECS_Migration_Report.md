# BuffSystem ECS 迁移报告

## 当前结果

BuffSystem 运行时已迁移为纯 ECS 固定帧模型。运行时核心不依赖 `MonoBehaviour`、`GameObject`、`Time.time`、`Time.deltaTime` 或 Unity 表现层对象。

本次补充恢复了旧版事件响应语义：

```text
IGameEvent -> IBuffSystem.Raise -> EventTrigger Buff -> IBuffEventEffectExecutor<TEvent>.OnEvent
```

`EventTrigger` 不再是死字段，只有配置了匹配 `EventIds` 且 Effect 实现了对应事件接口的 Buff 才会响应事件。

## 运行时数据流

单机固定帧：

```text
SimulateRunner
-> World.Tick
-> ECSBuffSystem.Tick
-> BuffSystemCore.Tick
```

事件触发：

```text
固定帧系统或帧命令
-> IBuffSystem.Raise(world, context, in gameEvent)
-> 收集 BuffRuntimeComponent
-> 过滤 TriggerType/EventId/Effect 能力/ShouldTrigger
-> 稳定排序
-> OnEvent
```

回滚重放：

```text
SimulationFrameCommandBuffer
-> RaiseBuffEventFrameCommand<TEvent>
-> IBuffSystem.Raise
```

事件命令只保存 `TEvent` 和显式传入的 `IBuffSystem`，不会保存 `GameObject` 或访问单例。

## 事件排序

同一事件命中的 Buff 会按以下顺序执行：

1. `BuffDefinition.Priority`
2. `BuffRuntimeComponent.runtimeHandle`
3. `BuffEntity.ID`
4. `BuffEntity.Version`

该顺序不依赖 `Dictionary` 遍历，也不依赖查询返回的原始顺序。

## 性能优化记录

`BuffSystemCore.Tick` 已将运行中 Buff 查询集中到 Tick 开头，后续 Runtime Lookup、生命周期 Tick 和视图缓存复用同一份本帧快照。本帧新增的 Runtime Entity 会记录到单独列表，保持本帧可被 ViewCache 读取，同时不改变新增 Buff 的时长推进语义。

Runtime Lookup 和目标 Buff 视图列表会复用内部 `List`，避免每帧为相同 key 或 target 重复分配。`GetBuffs(target)` 改为按目标懒构建，`TryGetBuff` 保持通过 key 字典读取。

并行 Buff 仍保持“每层一个 Runtime Entity”的设计。这种结构利于回滚快照和独立到期，但在极高层数场景下会增加 Entity 数量、排序和查询成本；后续如需进一步优化，可以评估在保持快照确定性的前提下，将同配置并行层压缩到单个 Runtime Entity 的内部固定数组中。

## 配置边界

`BuffConfigData` 仍是 Unity Authoring 入口，但运行时使用 `BuffDefinition`。

新增配置：

- `EventIds`：仅 `TriggerType == EventTrigger` 时有效。
- `EventIds` 为空时默认不响应事件。
- 编辑器校验会提示 EventTrigger Buff 缺少 EventIds。

## Effect 边界

生命周期 Effect 使用 `IBuffEffectExecutor`。

事件 Effect 使用 `IBuffEventEffectExecutor<TEvent>`。

一个 Effect 可以同时实现多个事件接口，例如：

```csharp
public sealed class ComboEffect :
    BuffEffectExecutorBase,
    IBuffEventEffectExecutor<AttackHitEvent>,
    IBuffEventEffectExecutor<KillEvent>
{
}
```

事件能力在 `BuffEffectRegistry.Register` 阶段缓存。`Raise<TEvent>` 热路径不做反射，不把事件参数装箱成 `object`。
