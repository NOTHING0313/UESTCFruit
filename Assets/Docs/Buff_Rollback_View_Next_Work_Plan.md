# BuffSystem / RollBackSystem / View 层后续工作总规划

本文用于总结当前项目中 BuffSystem、RollBackSystem 与 View 层的现状、问题、完善方案和推荐推进顺序。目标是把三套系统从“已有局部能力”推进到“可稳定接入 ECS 固定帧、回滚重模拟和 Unity 表现层”的完整链路。

> 当前建议：先统一数据边界，再补最小闭环，最后扩展表现和调试能力。不要一开始就做大规模重构，否则很容易同时破坏 ECS、Buff、回滚和 View 四条链路。

## 1. 总体目标

最终希望形成以下架构：

```text
外部输入 / 技能 / 网络命令
        |
        v
SimulationFrameCommandBuffer
        |
        v
SimulateRunner 固定逻辑帧
        |
        v
World.Tick
        |
        +--> BuffSystemCore / ECSBuffSystem
        |       |
        |       +--> BuffRuntimeComponent
        |       +--> AddBuffRequestComponent
        |       +--> RemoveBuffRequestComponent
        |       +--> BuffEffectECS
        |
        +--> Gameplay System
        |
        +--> WorldEventBuffer
                |
                v
        ViewBridge / ViewEventConsumer
                |
                v
        Unity GameObject / UI / VFX / SFX
```

回滚链路：

```text
预测输入
  -> 保存输入
  -> 模拟
  -> 保存快照
  -> 收到权威输入
  -> 比对差异
  -> 回滚到快照
  -> 重放输入和帧命令
  -> 校验 Checksum
  -> 刷新 View
```

核心原则：

- 逻辑状态只放在 ECS World 和可快照数据中。
- Buff 的运行状态必须是确定性的。
- View 不进入逻辑快照。
- View 只消费 ECS 状态和 WorldEvent，不反向决定逻辑结果。
- RollBackSystem 不直接依赖具体业务组件，而是通过接口、快照和 Checksum 适配。

## 2. 当前系统现状

### 2.1 BuffSystem 现状

当前 BuffSystem 同时存在两套形态：

1. 旧版 MonoBehaviour 驱动形态。
   - `BuffHandler`
   - `Buff`
   - `BuffRuntimeData`
   - `ParallelBuff`
   - `BuffEffect`
   - `EventRouter`
   - `BuffRuntimeDataFactory`

2. 新版 ECS 化接口和组件形态。
   - `IBuffSystem`
   - `BuffSystemCore`
   - `ECSBuffSystem`
   - `BuffRuntimeComponent`
   - `AddBuffRequestComponent`
   - `RemoveBuffRequestComponent`
   - `BuffDefinition`
   - `BuffEffectECS`
   - `BuffEventFrameCommand`
   - `BuffViewData`

已经具备的能力：

- 有 Buff 配置数据 `BuffConfigData`。
- 有普通 Buff 和并行叠层 Buff 的基础模型。
- 有 Buff 事件路由机制。
- 有 BuffEffect 抽象。
- 新版 ECS 接口已经开始使用 `ECSFrameWork.Entity`。
- 新版 Buff Runtime 已有 `BuffRuntimeComponent`，字段包括目标、来源、配置 ID、runtimeHandle、stack、durationFrames、remainingFrames、tickIntervalFrames、priority 等。
- 已经有 Buff 帧命令扩展，可以通过 `SimulationFrameCommandBuffer` 创建 Add/Remove Buff 请求实体。
- RollBackSystem 的 `WorldChecksumUtility` 已经把 `BuffRuntimeComponent` 纳入 Checksum。

### 2.2 RollBackSystem 现状

RollBackSystem 当前更像“接口 + Demo 工具 + ECS 适配雏形”，还不是完整回滚框架。

已有内容：

- 输入缓存接口：`IInputBuffer<TInput>`。
- 回滚世界接口：`IRollbackableWorld<TInput>`。
- 回滚协调器接口：`IRollbackSimulation<TInput>`。
- 基础模拟接口：`ISimulation<TInput>`。
- Checksum 接口：`ISimulationChecksum`。
- 输入比较结果：`InputComparisonResult`。
- ECS Runner 适配：`RollbackRunnerAdapter`。
- 输入写入适配：`IWorldInputApplier<TInput>`。
- Demo 输入：`PlayerInput`。
- Demo 输入比较器：`PlayerInputComparer`。
- World 完整快照：`WorldSnapshot`。
- Entity 快照：`EntitySnapshotData`。
- Component 快照：`ComponentSnapshotData`。
- 反射恢复组件：`ReflectionComponentRestore`。
- 通用 Checksum：`WorldChecksumCalculator`。
- 业务定制 Checksum：`WorldChecksumUtility`。

明显缺失：

- `RollbackCoordinator` 实现。
- `InputBuffer<TInput>` 实现。
- 权威输入缓存。
- 快照环形缓冲。
- Checksum 历史缓存。
- 权威 Checksum 缓存。
- `IInputComparer<TInput>` 定义或统一位置。
- `ISnapshot` / `ISnapshotable<T>` 定义或统一位置。
- 可运行测试。
- 与真实 ECS World、BuffSystem、View 层的完整集成样例。

### 2.3 View 层现状

View 层已有基础生命周期闭环。

已有内容：

- `ViewManager`
  - 注册 prefab。
  - 生成 viewID。
  - 维护 `viewID -> GameObject / Transform`。
  - 销毁 View。
  - 清空 View。

- `IViewInstanceProvider`
  - 抽象 View 创建和释放。

- `DefaultViewInstanceProvider`
  - 使用 `Instantiate / Destroy`。

- `PoolSystemViewInstanceProvider`
  - 通过反射适配 `PoolSystem.GameObjectPoolCenter`。
  - 失败时 fallback 到 `Instantiate / Destroy`。

- `ViewSpawnSystem`
  - 消费 `PrefabViewRequestComponent`。
  - 根据 `PositionComponent` 生成 View。
  - 写回 `ViewComponent(viewID)`。

- `ViewSyncSystem`
  - 把 `PositionComponent` 同步到 Unity `Transform.position`。

- `ViewDestroySystem`
  - 消费 `ViewDestroyRequestComponent`。
  - 调用 `ViewManager.DestroyView`。
  - 移除 `ViewComponent` 和销毁请求组件。

- `WorldUnityExtensions`
  - `CreateEntityWithView`
  - `CreateMovingEntityWithView`
  - `RequestView`
  - `DestroyEntityWithView`
  - `DestroyViewOnly`

- Contracts 侧接口：
  - `IViewBridge`
  - `IEntityViewBinder`
  - `IObjectPoolFacade`
  - `ViewEffectCommand`
  - `IWorldViewReader`

当前缺口：

- `IEntityViewBinder` 还没有实现并接入 ViewManager。
- `IViewBridge` 还没有实现。
- `ViewEffectCommand` 还没有从 WorldEvent 转成实际表现播放。
- `IObjectPoolFacade` 没有被 ViewManager 使用。
- View 层没有 Buff UI 同步实现。
- View 层没有 WorldEvent 消费器。
- View 层没有回滚重模拟期间的表现抑制策略。

## 3. BuffSystem 问题清单与完善方案

### 3.1 新旧 Buff 架构并存，职责边界不清

问题：

- 旧版 `BuffHandler` 依赖 `MonoBehaviour Update / LateUpdate`。
- 新版 `BuffSystemCore` 走 ECS 固定帧。
- 两套路径同时存在，容易出现同一个 Buff 既被 Unity 帧驱动又被 ECS 帧驱动。
- 旧版 `BuffRuntimeData` 使用 `GameObject` 作为 Source / Target。
- 新版 `BuffRuntimeComponent` 使用 `Entity`，更适合回滚。

完善方案：

- 明确旧版 `BuffHandler` 只作为兼容层或测试层，不参与回滚逻辑。
- 正式逻辑统一走：

```text
AddBuffCommand / RemoveBuffCommand
  -> AddBuffRequestComponent / RemoveBuffRequestComponent
  -> BuffSystemCore.Tick
  -> BuffRuntimeComponent
```

- 新业务禁止直接调用 `BuffHandler.AddBuff`。
- 文档和示例统一使用 `IBuffSystem` 和帧命令。
- 旧版 `BuffEffect` 如果继续保留，应标记为非回滚路径。

### 3.2 Buff 时间基准需要彻底固定帧化

问题：

- 旧版 `BuffHandler.UpdateBuffState()` 使用 `Time.time` 和 `Time.deltaTime`。
- `ParallelBuffRunTimeData` 使用绝对时间保存每层到期时间。
- 回滚重放时 Unity 时间不可逆，无法保证确定性。

完善方案：

- Buff Runtime 统一使用帧计数：
  - `durationFrames`
  - `remainingFrames`
  - `tickIntervalFrames`
  - `elapsedFrames`
  - `ticks`
  - 并行 Buff 每层保存 `expireFrame`，不要保存 `expireTime`。

- 添加 Buff 时将秒转换成帧：

```csharp
int durationFrames = Mathf.CeilToInt(config.Duration / context.tickLength);
```

- Tick 判断改为：

```text
elapsedFrames += 1
remainingFrames -= 1
if tickIntervalFrames > 0 && elapsedFrames % tickIntervalFrames == 0 -> OnTick
if remainingFrames <= 0 -> 过期
```

### 3.3 BuffEffect 仍可能直接修改 MonoBehaviour

问题：

- 例如 `SpeedUpEffect` 当前会通过 `ctx.Handler.GetComponent<TestBuffChractor>()` 修改测试组件。
- 这类修改不在 ECS 快照内，不可回滚。
- 表现层和逻辑层耦合。

完善方案：

- 回滚逻辑 BuffEffect 必须迁移到 `BuffEffectECS`。
- 效果修改 ECS 组件：
  - `StatComponent`
  - `HealthComponent`
  - `MoveSpeedComponent`
  - 自定义状态组件

- 表现效果通过 WorldEvent 输出，不直接播放特效。

建议标准：

```text
逻辑影响 -> ECS Component
表现反馈 -> WorldEvent -> ViewBridge
临时 UI 状态 -> View 层自身维护
```

### 3.4 Buff RuntimeHandle 生成需要可回滚

问题：

- 旧版 `_nextRuntimeHandle` 是运行时递增值。
- 如果回滚恢复时没有恢复 `_nextRuntimeHandle`，重模拟会生成不同 handle。
- Checksum 已纳入 `runtimeHandle`，handle 不一致会导致校验失败。

完善方案：

- BuffSystem 快照必须包含：
  - `_nextRuntimeHandle`
  - 当前所有 Runtime 的 handle

或改为确定性 handle：

```text
runtimeHandle = Hash(target.ID, target.Version, source.ID, source.Version, configId, createFrame, sequence)
```

推荐短期：

- 保留递增 handle。
- 把 `_nextRuntimeHandle` 纳入 BuffSystem 快照。

推荐长期：

- 使用确定性 handle，降低快照依赖。

### 3.5 Buff 查询和 UI 数据需要稳定缓存策略

问题：

- `GetBuffs(Entity target)` 如果每次扫描 World，可能有分配和排序成本。
- UI 展示需要稳定顺序。

完善方案：

- `BuffSystemCore` 内部维护 ViewCache：
  - 按 target 缓存 `List<BuffViewData>`。
  - Buff 添加、移除、层数变化时更新缓存。
  - UI 查询只读缓存。

- 缓存排序规则：

```text
priority -> configId -> runtimeHandle
```

- ViewCache 不作为逻辑真相，逻辑真相仍是 `BuffRuntimeComponent`。

### 3.6 Buff 事件需要 Entity 化

问题：

- 旧版事件如 `AttackEvent` 使用 `GameObject Attacker / Target`。
- GameObject 不适合回滚和网络同步。

完善方案：

- Buff 逻辑事件统一使用 `Entity`：

```csharp
public readonly struct AttackEvent : IGameEvent
{
    public readonly Entity Attacker;
    public readonly Entity Target;
    public readonly int Damage;
    public readonly int Frame;
}
```

- 事件进入 BuffSystem 的方式：
  - 逻辑帧内直接 `IBuffSystem.Raise(world, context, in event)`。
  - Tick 外部事件通过 `BuffEventFrameCommand` 排入指定帧。

### 3.7 BuffSystem 需要接入 WorldEvent

问题：

- Buff 添加、移除、层数变化和 Tick 效果目前没有统一表现事件输出。
- View 层没有稳定的 Buff 表现输入。

完善方案：

新增 WorldEvent：

- `BuffAppliedWorldEvent`
- `BuffRemovedWorldEvent`
- `BuffStackChangedWorldEvent`
- `BuffTickWorldEvent`
- `BuffEffectTriggeredWorldEvent`

字段建议：

```csharp
int frameNumber
Entity target
Entity source
int configId
int runtimeHandle
int stack
int stackDelta
```

使用要求：

- 事件只描述逻辑结果。
- 不直接引用 GameObject、Sprite、AudioClip。
- View 层根据 configId 映射表现资源。

## 4. RollBackSystem 问题清单与完善方案

### 4.1 回滚协调器缺失

问题：

- `IRollbackSimulation<TInput>` 已定义，但没有实现。
- `RollbackTest` 中引用的 `RollbackCoordinator` 不存在或未接入。

完善方案：

实现 `RollbackCoordinator<TInput, TSnapshot>`：

职责：

- 当前帧推进。
- 保存本地预测输入。
- 保存快照。
- 接收权威输入。
- 比对输入差异。
- 回滚到最近快照。
- 重模拟到当前帧。
- 记录和比对 Checksum。

核心字段：

```text
IInputBuffer<TInput> predictedInputs
IInputBuffer<TInput> authoritativeInputs
IInputComparer<TInput> inputComparer
SnapshotRingBuffer<TSnapshot> snapshots
IRollbackableWorld<TInput> world
ChecksumBuffer localChecksums
ChecksumBuffer authoritativeChecksums
int currentFrame
```

### 4.2 缺少输入比较接口定义

问题：

- `PlayerInputComparer` 实现了 `Interfaces.IInputComparer<PlayerInput>`。
- 但在当前 RollBackSystem 直接文件中没有看到该接口定义。

完善方案：

新增：

```csharp
namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IInputComparer<TInput>
    {
        bool IsEqual(TInput a, TInput b);
    }
}
```

要求：

- 输入比较必须只比较会影响逻辑的字段。
- 不比较时间戳、调试字段、表现字段。

### 4.3 缺少 Snapshot 接口定义

问题：

- `WorldSnapshot : ISnapshot`。
- `IRollbackableWorld<TInput> : ISnapshotable<ISnapshot>`。
- 但接口定义位置不明确。

完善方案：

统一放在 `RollBackSystem/Interfaces`：

```csharp
public interface ISnapshot
{
    int Frame { get; }
    void Release();
}

public interface ISnapshotable<TSnapshot>
{
    TSnapshot CaptureSnapshot(int frame);
    void RestoreSnapshot(TSnapshot snapshot);
}
```

注意：

- 如果已有同名接口，应统一命名空间并删除重复定义。

### 4.4 WorldSnapshot 恢复 Entity ID / Version 存在风险

问题：

- 当前 `EntitySnapshotData.Restore` 使用 `world.CreateEntity()` 重建 Entity。
- 这不保证恢复出原来的 Entity ID / Version。
- 如果组件中保存了 Entity 引用，例如 BuffRuntimeComponent.target/source，恢复后可能引用旧 Entity。

完善方案有两种：

方案 A：支持按原 ID / Version 恢复 Entity。

- 给 ECS World / EntityManager 增加内部恢复接口。
- Snapshot 恢复时重建原 Entity。
- 适合长期方案。

方案 B：建立 Entity 映射表。

- Restore 时记录：

```text
oldEntity -> newEntity
```

- 所有组件恢复后，扫描组件中 Entity 字段并 remap。
- 适合短期，但实现复杂，需要组件级 remap 支持。

推荐：

- 如果要做真正回滚，优先采用方案 A。
- 如果短期只是 Demo，可继续使用当前完整重建方案，但不要把它当最终回滚方案。

### 4.5 Checksum 需要稳定排序

问题：

- Checksum 遍历 Entity 和组件类型时，如果顺序不稳定，可能同状态不同 hash。
- `componentType.GetHashCode()` 不一定适合作为跨运行稳定值。

完善方案：

- Entity 按 `ID, Version` 排序。
- Component 类型按稳定名称排序：

```text
componentType.FullName
```

- 避免使用引用对象默认 `GetHashCode()`。
- 对每个参与回滚的组件写显式 Hash 逻辑。

建议新增：

```csharp
public interface IDeterministicHash
{
    void AppendHash(ref uint hash);
}
```

或集中在 `WorldChecksumUtility` 里手写每个组件字段。

### 4.6 帧命令也需要回滚重放

问题：

- ECS 已有 `SimulationFrameCommandBuffer`。
- RollBackSystem 当前重点是输入和 WorldSnapshot，还没有明确帧命令如何保存和重放。
- Buff 添加/移除依赖帧命令，如果回滚不重放帧命令，Buff 状态会丢失或漂移。

完善方案：

- RollBackSystem 重模拟时必须重放：
  - 输入。
  - 同帧外部命令。
  - BuffFrameCommand。
  - 其他 GameplayFrameCommand。

- `SimulationFrameCommandApplier` 已有 `ReplayCommandsToWorld`，回滚时应使用它而不是普通 `ApplyCommandsToWorld`。

### 4.7 RollBack 与 View 需要隔离

问题：

- 回滚重模拟可能连续执行多个历史帧。
- 如果每次都播放 View 特效，会重复播放音效、动画、飘字。

完善方案：

- `SimulationContext.isRollback = true` 时：
  - 不播放一次性表现。
  - 不发出不可逆外部副作用。
  - 只更新最终 Transform 状态。

- ViewEventConsumer 需要判断：

```text
if context.isRollback -> 不播放或缓存
if final confirmed frame -> 播放
```

## 5. View 层问题清单与完善方案

### 5.1 Entity 与 View 双向绑定未完成

问题：

- `ViewManager` 只知道 viewID。
- `IEntityViewBinder` 已定义，但没有实现。
- Buff 特效、点击选中、UI 血条需要从 Entity 找 GameObject。

完善方案：

实现 `EntityViewBinder`：

```text
Dictionary<Entity, int> entityToViewId
Dictionary<int, Entity> viewIdToEntity
Dictionary<Entity, GameObject> entityToView
```

接入点：

- `ViewSpawnSystem` 成功生成 viewID 后调用 Bind。
- `ViewDestroySystem` 销毁 View 后调用 Unbind。
- `EntityDestroySystem` 销毁 Entity 前释放绑定。

### 5.2 ViewBridge 未实现

问题：

- `IViewBridge` 只有接口，没有实现。
- Buff UI、特效、音效没有统一入口。

完善方案：

实现 `ViewBridge`：

- 依赖：
  - `IEntityViewBinder`
  - `IObjectPoolFacade`
  - `IBuffSystem`

- 能力：
  - `PlayEffect(in ViewEffectCommand command)`
  - `SyncBuffUI(Entity target, IBuffSystem buffSystem)`

注意：

- `ViewBridge` 不修改 ECS 逻辑组件。
- `ViewBridge` 可以访问 Unity 对象、UI、特效 prefab。

### 5.3 WorldEvent 到 View 的消费链缺失

问题：

- ECS 和 BuffSystem 可以产生 WorldEvent。
- 但 View 层没有统一消费器。

完善方案：

实现 `WorldViewEventConsumer`：

职责：

- 每帧读取 WorldEvent。
- 将逻辑事件映射为 `ViewEffectCommand` 或 UI 更新。
- 调用 `IViewBridge`。
- 按帧清理已消费事件。

消费事件示例：

- `DamageWorldEvent` -> 飘字 / 受击特效。
- `EntityDeadWorldEvent` -> 死亡特效。
- `BuffAppliedWorldEvent` -> Buff 图标刷新 / 特效。
- `BuffRemovedWorldEvent` -> Buff 图标刷新。
- `BuffStackChangedWorldEvent` -> Buff 层数刷新。

### 5.4 View 同步维度不足

问题：

- 当前 `ViewSyncSystem` 只同步位置。
- 实际表现还需要朝向、缩放、动画状态、显隐状态。

完善方案：

按组件逐步扩展：

- `RotationComponent`
- `ScaleComponent`
- `ViewVisibleComponent`
- `AnimationStateComponent`
- `FacingComponent`

对应系统：

- `ViewTransformSyncSystem`
- `ViewAnimationSystem`
- `ViewVisibilitySystem`

注意：

- 不建议一个系统一次性同步所有表现。
- 先扩展最常用的 rotation。

### 5.5 对象池门面与现有 Provider 未统一

问题：

- `IObjectPoolFacade` 已定义。
- `PoolSystemViewInstanceProvider` 当前通过反射找 PoolSystem。
- 两套抽象并存。

完善方案：

短期：

- 保留 `PoolSystemViewInstanceProvider`。
- 增加启动校验日志，明确当前是否接入对象池成功。

长期：

- 用 `IObjectPoolFacade` 实现新的 `ViewInstanceProvider`。
- ViewManager 只依赖 `IViewInstanceProvider`。
- PoolSystem 细节完全放在 Facade 中。

### 5.6 View 生成失败缺少恢复策略

问题：

- `ViewSpawnSystem` 生成失败后移除请求组件。
- 这会导致该 Entity 永远没有 View。
- 只打印 Warning，后续难以追踪。

完善方案：

- 生成失败时输出 WorldEvent 或 Debug 记录。
- 可选保留请求组件并限制重试次数。
- 增加 `ViewSpawnFailedWorldEvent`。

建议策略：

```text
Prefab 未注册 -> 失败并上报，不重试
Provider 临时失败 -> 可重试
Prefab 为空 -> 失败并上报
```

## 6. 三套系统集成方案

### 6.1 Buff 与 RollBack 集成

目标：

- Buff 状态进入 ECS World。
- Buff 添加/移除走帧命令。
- Buff Runtime 纳入快照和 Checksum。

需要完成：

1. 确认 `BuffRuntimeComponent` 是唯一正式运行时状态。
2. `BuffSystemCore.Tick` 使用 `SimulationContext`。
3. 所有 Buff 命令使用 `SimulationFrameCommandBuffer`。
4. Rollback 重模拟时重放 Buff 帧命令。
5. Checksum 覆盖 BuffRuntimeComponent 的所有关键字段。
6. Snapshot 覆盖 BuffRuntimeComponent。

### 6.2 Buff 与 View 集成

目标：

- Buff 逻辑不直接碰 Unity View。
- Buff 表现通过 WorldEvent 和 ViewBridge。

需要完成：

1. BuffSystem 产生 Buff WorldEvent。
2. ViewEventConsumer 消费 Buff WorldEvent。
3. ViewBridge 调用 `IBuffSystem.GetBuffs(target)` 刷新 UI。
4. ViewBridge 根据 configId 播放对应特效。

### 6.3 RollBack 与 View 集成

目标：

- 回滚时不重复播放表现。
- 重模拟后 View 与最终 World 状态一致。

需要完成：

1. ViewEventConsumer 感知 `context.isRollback`。
2. 回滚重模拟期间只同步 Transform，不播放一次性事件。
3. 重模拟结束后刷新所有 Entity View。
4. 如果快照恢复导致 ViewComponent 或 ViewManager 映射失效，需要重建绑定。

关键问题：

- 如果 WorldSnapshot 恢复时重建 Entity，旧 View 绑定会失效。
- 因此必须先解决 Entity ID / Version 恢复，或提供 View 绑定 remap。

## 7. 推荐推进顺序

### 阶段 1：收敛 BuffSystem 到 ECS 路径

目标：

- 明确正式 Buff 逻辑只走 ECS。

任务：

1. 梳理 `BuffSystemCore` 当前实现。
2. 确认 `ECSBuffSystem` 是否已经注册到 World。
3. 禁止新逻辑直接调用 `BuffHandler`。
4. 为 Add/Remove Buff 帧命令补测试。
5. 确认 Buff Tick 不依赖 Unity `Time`。
6. 确认 `BuffRuntimeComponent` 字段足够表达所有运行时状态。

验收：

- 固定推进 N 帧后，Buff remainingFrames、stack、ticks 与预期一致。
- 同样输入重复模拟，Buff 结果一致。

### 阶段 2：补齐 RollBackSystem 最小闭环

目标：

- 可以预测、保存、回滚、重模拟。

任务：

1. 补 `ISnapshot` / `ISnapshotable<T>`。
2. 补 `IInputComparer<T>`。
3. 实现 `InputBuffer<TInput>`。
4. 实现 `SnapshotRingBuffer<TSnapshot>`。
5. 实现 `RollbackCoordinator<TInput, TSnapshot>`。
6. 恢复并更新 `RollbackTest`。

验收：

- frame 1 输入被权威输入修正后，可以回滚并重模拟到当前帧。
- 重模拟后 World Checksum 一致。

### 阶段 3：修正 Snapshot 的 Entity 恢复问题

目标：

- 回滚后 Entity 引用不漂移。

任务：

1. 决定使用原 ID / Version 恢复，还是 Entity remap。
2. 如果使用原 ID / Version 恢复，扩展 EntityManager 内部恢复接口。
3. 如果使用 remap，定义组件 remap 机制。
4. 对 `BuffRuntimeComponent.target/source` 做恢复验证。

验收：

- 回滚后 BuffRuntimeComponent 的 target/source 指向仍有效。
- View、Buff、Damage 等引用 Entity 的组件不失效。

### 阶段 4：补 View 绑定和桥接

目标：

- Entity 能稳定找到 View。
- View 能消费逻辑事件。

任务：

1. 实现 `EntityViewBinder`。
2. 接入 `ViewSpawnSystem` 和 `ViewDestroySystem`。
3. 实现 `ViewBridge`。
4. 实现 `WorldViewEventConsumer`。
5. 支持 Buff UI 同步。

验收：

- Entity Spawn 后能通过 Binder 找到 GameObject。
- Entity Destroy 后绑定被清理。
- Buff 添加/移除后 UI 能刷新。
- DamageWorldEvent 能触发表现效果。

### 阶段 5：处理回滚下的表现策略

目标：

- 回滚不重复播放表现。

任务：

1. ViewEventConsumer 增加 rollback 模式判断。
2. 重模拟期间不播放一次性 ViewEffect。
3. 重模拟结束后刷新最终 Transform 和 Buff UI。
4. 为回滚后 View 映射修复做测试。

验收：

- 触发回滚时不会重复播放 Buff 特效或音效。
- 回滚完成后角色位置、血量 UI、Buff UI 与逻辑一致。

### 阶段 6：补调试与文档

目标：

- 方便定位状态漂移。

任务：

1. Checksum 输出每类组件 hash。
2. 增加 Buff Runtime Debug View。
3. 增加 View 绑定 Debug View。
4. 增加 Rollback 日志：
   - 差异帧
   - 回滚帧
   - 重模拟范围
   - 本地/权威 Checksum

验收：

- 出现状态漂移时能定位是输入、Buff、组件恢复还是 View 绑定问题。

## 8. 关键风险清单

### 8.1 Entity 恢复风险

风险：

- 快照恢复后 Entity ID 改变，导致组件引用失效。

影响：

- Buff target/source 失效。
- View 绑定失效。
- 伤害请求 target/source 失效。

优先级：

- 最高。

### 8.2 Buff 旧逻辑继续使用 Unity 时间

风险：

- 回滚重放结果不一致。

影响：

- Buff 到期时间不同。
- Tick 次数不同。
- Checksum 漂移。

优先级：

- 最高。

### 8.3 View 进入逻辑状态

风险：

- GameObject 或 Transform 被误放进 ECS 组件或快照。

影响：

- 无法序列化。
- 回滚后引用悬空。
- 逻辑和表现互相污染。

优先级：

- 高。

### 8.4 Checksum 不稳定

风险：

- 使用默认 `GetHashCode()` 或无序遍历。

影响：

- 相同状态也可能判定漂移。

优先级：

- 高。

### 8.5 帧命令未重放

风险：

- 回滚只重放输入，不重放 Buff 添加/移除等外部命令。

影响：

- Buff 状态丢失。
- 技能结果不一致。

优先级：

- 高。

### 8.6 表现事件重复播放

风险：

- 回滚重模拟多次触发 WorldEvent。

影响：

- 重复飘字。
- 重复音效。
- 重复特效。

优先级：

- 中高。

## 9. 建议新增或完善的文件

### BuffSystem

建议新增：

- `BuffAppliedWorldEvent.cs`
- `BuffRemovedWorldEvent.cs`
- `BuffStackChangedWorldEvent.cs`
- `BuffTickWorldEvent.cs`
- `BuffRollbackSnapshot.cs`，如果 BuffSystem 有非 ECS 缓存状态。

建议完善：

- `BuffSystemCore.cs`
- `ECSBuffSystem.cs`
- `BuffEffectECS.cs`
- `BuffEventFrameCommand.cs`
- `BuffECSComponents.cs`

### RollBackSystem

建议新增：

- `Interfaces/IInputComparer.cs`
- `Interfaces/ISnapshot.cs`
- `Interfaces/ISnapshotable.cs`
- `InputBuffer.cs`
- `AuthoritativeInputBuffer.cs`
- `SnapshotRingBuffer.cs`
- `ChecksumBuffer.cs`
- `RollbackCoordinator.cs`
- `WorldRollbackAdapter.cs`

建议完善：

- `WorldSnapshot.cs`
- `EntitySnapshotData.cs`
- `ReflectionComponentRestore.cs`
- `WorldChecksumUtility.cs`
- `RollbackTest.cs`

### View 层

建议新增：

- `EntityViewBinder.cs`
- `ViewBridge.cs`
- `WorldViewEventConsumer.cs`
- `BuffUIViewAdapter.cs`
- `ViewEffectRegistry.cs`

建议完善：

- `ViewManager.cs`
- `ViewSpawnSystem.cs`
- `ViewDestroySystem.cs`
- `ViewSyncSystem.cs`
- `PoolSystemViewInstanceProvider.cs`

## 10. 验证路线

### 10.1 BuffSystem 验证

手动测试：

1. 创建目标 Entity。
2. 添加 `HealthComponent` / `StatComponent`。
3. 通过 `SimulationFrameCommandBuffer` 添加 Buff。
4. 推进固定帧。
5. 验证：
   - BuffRuntimeComponent 创建。
   - stack 正确。
   - remainingFrames 正确。
   - OnTick 次数正确。
   - OnRemove 正确清理。

### 10.2 RollBackSystem 验证

手动测试：

1. frame 1 使用预测输入 A。
2. frame 2 使用预测输入 A。
3. 保存快照。
4. 收到 frame 1 权威输入 B。
5. 回滚到 frame 1 前。
6. 使用 B 重模拟到当前帧。
7. 验证最终位置、Buff 状态、Checksum。

### 10.3 View 层验证

手动测试：

1. 注册 prefab。
2. 创建带 View 请求的 Entity。
3. 推进一帧。
4. 验证 View 生成。
5. 验证 Binder 能从 Entity 找 View。
6. 修改 Position，验证 Transform 同步。
7. 请求销毁 Entity，验证 View 释放和 Binder 清理。

### 10.4 三系统集成验证

集成测试：

1. 玩家释放技能。
2. 技能通过帧命令添加 Buff。
3. Buff 修改目标属性。
4. Buff 产生 WorldEvent。
5. ViewBridge 播放特效并刷新 Buff UI。
6. 收到权威输入修正。
7. Rollback 重模拟。
8. 验证：
   - Buff 结果一致。
   - Checksum 一致。
   - View 不重复播放旧特效。
   - 最终 UI 与逻辑一致。

## 11. 推荐短期落地顺序

如果只按最小可用目标推进，建议顺序如下：

1. 确认 `BuffSystemCore` 已能完全替代旧 `BuffHandler` 的逻辑路径。
2. 补齐 RollBackSystem 的基础接口缺口。
3. 实现最小 `RollbackCoordinator`。
4. 修复或规避 `WorldSnapshot` 的 Entity ID 恢复问题。
5. 实现 `EntityViewBinder`。
6. 实现 `ViewBridge` 和 `WorldViewEventConsumer`。
7. 把 Buff WorldEvent 接入 ViewBridge。
8. 做 Buff + Rollback + View 的最小集成测试。

## 12. 最终完成标准

满足以下条件后，可以认为三套系统基本完成第一阶段集成：

- Buff 添加、移除、Tick 全部在固定逻辑帧中执行。
- Buff Runtime 状态全部存在于 ECS 可快照数据中，或有明确快照结构。
- RollBackSystem 可以保存快照、接收权威输入、回滚、重模拟。
- Checksum 能覆盖核心 Gameplay 状态和 Buff 状态。
- View 层可以根据 Entity 生成、绑定、同步、销毁 GameObject。
- View 层可以消费 WorldEvent 播放表现。
- 回滚重模拟期间不会重复播放表现副作用。
- 回滚完成后 View 与最终 ECS World 状态一致。

