# RollbackSystem LogicOnly Closure RBS-Fix-A1-A6

## Scope

本阶段只完成 RollbackSystem 内部 logic-only 闭环修复，不宣称生产级 rollback 已闭环。

已确认未修改：

- ECSCore / World / Snapshot 实现
- TimeSimulator
- SimulateRunner
- SimulationInitializer
- Prefab / Scene / ScriptableObject / ProjectSettings / Packages / .meta

## Completed

- 新增 `RollbackStepResult` / `RollbackRestoreResult` / `RollbackResimulateResult` 诊断结果。
- 新增 `TryStep(frame, input)`，正常帧只写输入，不执行 `World.Tick`。
- `RollbackBootstrap.OnBeforeTick` 使用 `TryStep(ctx.frameNumber, input)`，失败直接抛异常，阻止 Runner 继续错误 Tick。
- `WorldRollbackAdapter.Simulate` 改为 input-only，不再执行 BeforeTick / AfterTick FrameCommand。
- `WorldRollbackAdapter.TryRestore` 返回显式结果，覆盖 null、类型错误、World restore 失败和异常。
- `RollbackCoordinator.TryRollbackTo` 在 Restore 成功后才更新 `CurrentFrame`。
- `TryResimulateTo` 是 Coordinator 内唯一显式 Tick 路径，并用 `SaveChecksum(frame)` 保存正确帧号。
- authoritative input 提前到达时，`TryStep` 会在 Tick 前改用 authoritative input，不再静默漏检。
- 单个 `PlayerInputSnapshot` 被应用到多个 player entity 时会被阻断。
- `TickMultiple` 不再作为生产 catch-up 路径推进 Coordinator。
- 新增 restore/resimulated listener 入口，View resync 暂不实现。

## Why TryStep Does Not Tick

正常帧由 `TimeSimulator.Update -> SimulateRunner.Update -> TickFrame` 驱动。`TickFrame` 的生命周期是：

```text
BeforeTick -> World.Tick -> AfterTick
```

因此 `RollbackCoordinator.TryStep` 只能在 `BeforeTick` 中准备输入。如果它内部也 Tick，会和 Runner 形成双 Tick。

## Failure Policy

`BeforeTick` 没有取消返回值。`TryStep` 不可修正失败时，`RollbackBootstrap.OnBeforeTick` 必须 fail-fast 抛异常。禁止出现：

```text
TryStep failure -> log -> return -> Runner 继续 World.Tick
```

## Restore Contract

`IRollbackableWorld<TInput>.TryRestore(ISnapshot snapshot)` 是新的诊断入口。

失败时 Coordinator 不更新 `CurrentFrame`，不继续重模拟。

## Checksum Frame Fix

重模拟流程按显式帧保存 checksum：

```text
Restore to 5
Resimulate 6 -> SaveChecksum(6)
Resimulate 7 -> SaveChecksum(7)
Resimulate 8 -> SaveChecksum(8)
```

不会再把 frame 6 的 checksum 保存到旧 `CurrentFrame = 5`。

## FrameCommand Status

A1-A6 不接真实生产 `SimulationFrameCommandApplier`。

当前状态：

- `TimeSimulator` 有 `SetFrameCommandApplier` 和 `DebugFrameCommandApplier`。
- `SimulationInitializer` 当前源码未注入真实 applier。
- `RollbackBootstrap` 已停止创建孤立 `SimulationFrameCommandBuffer / SimulationFrameCommandApplier`。
- 重模拟如果没有真实 `IFrameCommandSource`，只记录 skipped / blocked 诊断。

A7 blocked：需要用户确认是否允许最小修改 `SimulationInitializer` 或 `TimeSimulator` 完成真实 FrameCommandApplier 接线。

## Listener Status

新增 `IRollbackRestoreListener`。

当前只提供入口：

- `OnRollbackWorldRestored(World world, int restoredFrame)`
- `OnRollbackResimulated(World world, int currentFrame)`

`BuffRollbackRestoreListener` 已存在，但本阶段不自动反射或改 Initializer 接线。

View restore / resimulated 后重同步属于 RBS-Fix-B。

## Manual Unity Verification

1. 打开 Unity Editor，让脚本完成编译。
2. 新建临时 GameObject。
3. 添加 `RollbackCoordinatorLogicOnlyTestBootstrap`。
4. 在组件右键菜单执行 `Run Logic-Only Rollback Tests`。
5. Console 预期出现：

```text
[RollbackCoordinatorLogicOnlyTestBootstrap] All logic-only rollback checks passed.
```

6. 手动运行当前场景，观察 Console：
   - 不应出现 RollbackBootstrap 创建孤立 FrameCommandApplier 的行为。
   - 若 `_expectedFrame` 超前，应只出现 catch-up blocked 警告，不应推进 Coordinator 假进度。
   - 若 TryStep 失败，应抛出异常阻止错误 Tick。
