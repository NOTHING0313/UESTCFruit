# NETWORK-SYNC-06D-KCP-03C-1

## 目标

把已经验证的：

`INetworkInputClient -> NetworkRollbackClientRuntime -> NetworkAuthorityRollbackDriver`

接到真实 `SimulateRunner.BeforeTick / AfterTick` 正常帧边界。

本阶段仍为纯 C# Runtime Core，不修改 Scene / Prefab，不创建 MonoBehaviour 网络入口。

## 基于当前项目源码确认的正式时序

`SimulateRunner.TickFrame`：

1. `BeforeTick`
2. `World.Tick`
3. `AfterTick`

因此 `NetworkRollbackSimulationRuntime` 只在 `BeforeTick`：

1. Pump 已到达 Authority
2. 收集本地 `PlayerInputSnapshot`
3. SendInput
4. `FrameInputAccumulator`
5. `FrameInputAssembler`
6. `RollbackCoordinator.TryStep`

它绝不主动执行正常 `World.Tick`。

`World.Tick` 仍只有 `SimulateRunner` 一个正常帧 owner。

`AfterTick` 只负责稳定边界 Snapshot。

## 03C-1 新增

- `NetworkPlayerBinding`
- `NetworkRollbackSimulationRuntime`
- `NetworkRollbackSimulationRuntimeValidationTestBootstrap`
- `NetworkRollbackSimulationRuntimeNUnitTests`

## 测试特点

- KCP Server：本地真实 `KcpNetworkInputServer`
- Local Player：走 `NetworkRollbackSimulationRuntime`
- Remote Player：独立 KCP Client，0~3 帧随机延迟
- Actual World：只通过 `SimulateRunner.StepNextFrame` 推进正常帧
- Reference World：使用完整真实 `FrameInputSet`
- 强制出现 Prediction、Out-of-order Authority、Rollback Restore、Resimulate
- 最终 Checksum 必须一致
- Actual World 正常 Tick 数必须严格等于 100，证明没有双 Tick

## 本轮不修改

- `TimeSimulator`
- `SimulateRunner`
- `RollbackBootstrap`
- `SimulationInitializer`
- `UnityInputAdapter`
- `RollbackCoordinator`
- Protocol / KCP

## 已发现但暂不在 03C-1 修改的问题

当前 `SimulationInitializer` 直接订阅：

`_runner.BeforeTick += _inputAdapter.WriteInputToWorld`

而 `RollbackBootstrap.DetachSimulationInitializerInput()` 只是通过 Reflection 把 Adapter 的 `_world/_playerEntity` 清空。

但 `UnityInputAdapter.WriteInputToWorld()` 会先执行 `CollectSnapshot()`，再检查 `_world`，因此 pressed/released/mouseDelta/scroll 等一次性输入仍可能被旧订阅提前消费。

03C-2 正式 Scene 网络接线时不能复制这种做法，应真正解除旧 BeforeTick 输入写入，或给 TimeSimulator/SimulationInitializer 增加明确的输入所有权切换 API。

此外当前 `SimulationInitializer.CreatePlayerEntity()` 创建的 `PlayerInputSnapshotComponent(0f,0f)` 的 playerID 初始为 0，并且只创建一个玩家。03C-2 需要显式创建/绑定完整网络玩家集合。
