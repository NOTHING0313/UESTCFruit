# RollbackSystem 最小逻辑闭环交接文档

## 1. 当前结论

当前状态可以记录为：

`RBS-A：Rollback local logic-only minimum closure completed.`

准确中文口径：

`Rollback 本地单玩家最小逻辑闭环已完成。`

该结论的范围限定为：本地单玩家、真实 `SimulationFrameCommandApplier` 接线、Snapshot Restore、Rollback Resimulate、Checksum 保存帧修复、ConfirmFrame 历史清理、`UnityInputAdapter` 场景挂载可用。

当前不能宣称 Rollback 已达到完整生产级联机闭环。

当前不包含：真实网络权威协议、多玩家 `FrameInputSet`、View restore/resync、服务端 authoritative checksum drift handling、长期压力测试。

## 2. 已完成阶段

### 2.1 RBS-Fix-A1-A6-LogicOnly

A1-A6 完成 RollbackSystem 内部 logic-only 闭环修复，关键点如下：

- `TryStep(frame, input)` 已变为 frame-aware。
- 正常帧 `TryStep` 只写输入，不执行 `World.Tick`。
- `TryStep` 失败时在 `RollbackBootstrap.OnBeforeTick` fail-fast，阻止 Runner 继续错误 Tick。
- `WorldRollbackAdapter.Simulate` 保持 input-only，不再执行 BeforeTick / AfterTick FrameCommand。
- Restore 结果化，`WorldRollbackAdapter.TryRestore` 返回明确诊断结果。
- Restore 失败不更新 `CurrentFrame`，也不继续重模拟。
- `TryResimulateTo` 是 Coordinator 内唯一显式 Tick 路径。
- 重模拟时 `SaveChecksum(frame)` 保存到显式帧号，避免 checksum 写入旧 `CurrentFrame`。
- authoritative input 提前到达时，`TryStep` 会在 Tick 前改用 authoritative input。
- 单个 `PlayerInputSnapshot` 被应用到多个 player entity 时会被 guard 阻断。
- `TickMultiple` 不再作为生产 catch-up 假追帧路径推进 Coordinator。
- 新增 restore/resimulated listener 入口，但 View resync 尚未实现。

### 2.2 RBS-Fix-A7 FrameCommand Real-Applier Wiring

A7 完成真实 FrameCommand 管线接线，关键点如下：

- `SimulationInitializer` 创建唯一真实 `SimulationFrameCommandBuffer`。
- `SimulationInitializer` 创建唯一真实 `SimulationFrameCommandApplier`。
- `SimulationInitializer` 通过 `TimeSimulator.SetFrameCommandApplier` 注入真实 Applier。
- `TimeSimulator` 正常 Tick 使用同一套 Applier。
- Rollback resimulation replay 使用同一套 Applier。
- `RollbackBootstrap` 不再自建孤立 `SimulationFrameCommandBuffer / SimulationFrameCommandApplier`。
- `RollbackBootstrap` 通过 `TimeSimulator.DebugFrameCommandBuffer` 与 `TimeSimulator.DebugFrameCommandApplier` 建立 replay binding。
- `WorldRollbackAdapter` 接收 `RollbackFrameCommandReplayBinding`，并通过真实 `SimulationFrameCommandApplier` replay BeforeTick / AfterTick FrameCommand。
- `ConfirmFrame` 清理 `FrameCommandBuffer.RemoveBefore(frame)` 与 `FrameCommandApplier.RemoveAppliedBefore(frame)`。
- FrameCommand replay 不可用或 replay 失败时，resimulation fail-fast，不再静默 skipped。

### 2.3 RBS-Fix-A7 MountHardening

MountHardening 完成生命周期接线加固，关键点如下：

- `SimulationInitializer` 优先使用 Inspector 显式绑定的 `_rollbackBootstrap`。
- 未显式绑定时，仅 fallback 到同 GameObject 的 `GetComponent<RollbackBootstrap>()`。
- `SimulationInitializer` 不使用全局 `FindObjectOfType` / `FindObjectsByType` 来选择 `RollbackBootstrap`。
- `RollbackBootstrap.TryMount` 幂等；已挂载时返回成功，不重建 coordinator，不重置帧状态，不重复订阅事件。
- `TryMount` 拒绝已经推进后的 late mount：`runner.IsTicking || runner.FrameCount > 0`。
- `TryMount` 会校验 `TimeSimulator` 上的真实 FrameCommand buffer/applier 是否存在且实例一致。
- `OnDisable` / `OnDestroy` 幂等 `Unmount`，解除 runner 事件订阅并清空 binding。
- 不放宽 `FrameMismatch` 校验。
- 不支持 Runner 已推进后随意重新 late mount。

### 2.4 InputAdapter-Fix-A / A2

InputAdapter 修复完成 Unity Inspector enum 报错收束，关键点如下：

- `UnityInputAdapter` 文件存在，并可通过场景挂载接入。
- `UnityInputAdapter not found` 已通过场景挂载解决。
- `KeyboardBinding.button` / `MouseBinding.button` 不再直接序列化 `InputButtonFlags`。
- `UnityInputAdapter` 的 MonoBehaviour 字段层不再持有 `InputButtonFlags`。
- `_heldButtons` / `_pressedBuffer` / `_releasedBuffer` 已收束为 raw integral cache。
- 运行时 `PlayerInputSnapshot` 仍使用 `InputButtonFlags`，对外输入快照契约不变。
- Inspector unsupported enum 报错已清除。

## 3. 当前最小逻辑闭环范围

当前闭环覆盖：

- 本地单玩家输入采样与写入。
- 正常 Tick 前输入进入 rollback coordinator。
- 正常 Tick 后保存 snapshot 与 checksum。
- authoritative input 到达后的 mismatch 检测。
- rollback restore 到目标帧前一帧。
- rollback resimulate 到当前帧。
- resimulation 中重放真实 FrameCommand BeforeTick / AfterTick。
- resimulation 后重新 capture snapshot 并保存显式帧 checksum。
- confirmed frame 前的 predicted input、authoritative input、snapshot、checksum、FrameCommand history 清理。

当前闭环不覆盖：

- 真实网络权威输入协议。
- 多玩家 `FrameInputSet`。
- View restore/resync。
- 服务端 authoritative checksum drift handling。
- 长期压力测试与 soak test。

## 4. 当前运行链路

### 4.1 正常 Tick 链路

```text
SimulationInitializer
-> 创建 World / SimulateRunner
-> 创建真实 SimulationFrameCommandBuffer
-> 创建真实 SimulationFrameCommandApplier
-> 注入 TimeSimulator.SetFrameCommandApplier
-> TimeSimulator.InitSimulator
-> 第一帧前 TryMount RollbackBootstrap

TimeSimulator.Update
-> SimulateRunner.TickFrame
-> BeforeTick
   -> RollbackBootstrap.OnBeforeTick
   -> RollbackCoordinator.TryStep(frame, input)
   -> WorldRollbackAdapter.Simulate input-only
-> World.Tick
-> AfterTick
   -> RollbackCoordinator.SaveSnapshot / SaveChecksum
```

### 4.2 Rollback / Resimulate 链路

```text
Receive authoritative input
-> 检测 predicted / authoritative mismatch
-> TryRollbackTo(frame - 1)
-> World.TryRestoreSnapshot
-> TryResimulateTo(currentFrame)
   -> Simulate input-only
   -> Replay BeforeTick FrameCommand
   -> World.Tick(isRollback: true)
   -> Replay AfterTick FrameCommand
   -> CaptureSnapshot(frame)
   -> SaveChecksum(frame)
```

### 4.3 ConfirmFrame 清理链路

```text
ConfirmFrame(frame)
-> 清理 predicted input history
-> 清理 authoritative input history
-> 清理 snapshot ring
-> 清理 checksum history
-> 清理 FrameCommandBuffer.RemoveBefore(frame)
-> 清理 FrameCommandApplier.RemoveAppliedBefore(frame)
```

## 5. 当前验证结果

当前已有验证记录：

- A1-A6 手动 Unity logic-only 测试通过。
- A7 场景输出显示第一帧前 mount 成功。
- 用户已确认当前无相关报错。

当前 Console 不再出现以下 rollback / input 相关问题：

- `FrameMismatch expected frame 1`
- `FrameCommand replay binding failed`
- `FrameCommand replay skipped`
- `UnityInputAdapter not found`
- `Unsupported enum type InputButtonFlags`

以上验证不等价于生产级联机 rollback 验收。当前未执行长期压力测试、网络漂移测试或多玩家输入一致性测试。

## 6. 明确未完成项

以下事项仍未完成，后续阶段不得误认为已经闭环：

- 真实网络权威输入协议未完成。
- 多玩家 `FrameInputSet` 未完成。
- View restore/resync 未完成。
- `PrefabID = 1` View spawn 警告如仍存在，需要单独处理。
- checksum 白名单规范未完成。
- authoritative checksum drift handling 未完成。
- 长期 rollback stress / soak 测试未完成。
- `UnityInputAdapter` 正式输入所有权策略仍需后续审计。
- Buff Event FrameCommand rollback 上下文未审计。

## 7. 当前已知非阻断警告 / 风险

- 当前闭环是本地单玩家逻辑闭环，不是生产级联机 rollback 闭环。
- 如果 `RollbackBootstrap` 与 `SimulationInitializer` 不在同 GameObject，需要手动在 Inspector 绑定 `_rollbackBootstrap`。
- 如果运行时禁用 `RollbackBootstrap`，会触发 `Unmount`；重新启用后不保证能安全 late mount 到已推进 Runner。
- `UnityInputAdapter` 已可通过场景挂载解决 not found，但正式输入所有权和多玩家输入仍未设计完成。
- View 生成警告属于表现层问题，不阻断 rollback 逻辑闭环。
- 当前 `ConfirmFrame` 的 FrameCommand history cleanup failure 仍是 diagnostic-only，失败时记录 warning，不改变 confirmed-frame 缓冲清理主流程。

## 8. 后续阶段建议

建议后续按以下顺序推进，避免重新搅动已稳定的 rollback 内核：

1. 明确正式输入所有权：决定 `UnityInputAdapter`、`RollbackBootstrap`、`InputSnapshotBuffer` 在生产路径中的职责边界。
2. 设计多玩家 `FrameInputSet` 契约：不要把单玩家 `PlayerInputSnapshot` 直接扩展成隐式多玩家协议。
3. 设计真实网络权威输入协议：包括 frame window、late input、confirm frame、retransmit、client prediction 与 server authority 边界。
4. 设计 checksum 白名单与 drift handling：先明确哪些组件参与权威 checksum，再处理 drift 诊断和恢复策略。
5. 设计 View restore/resync：表现层同步应监听 restore/resimulated 事件，不应反向影响 authority state。
6. 审计 Buff Event FrameCommand rollback 上下文：确认 Buff 事件在正常 Tick 与 rollback resimulation 下的重放、去重和清理规则。
7. 增加长期 rollback stress / soak 测试：覆盖频繁 mismatch、连续 rollback、FrameCommand cleanup、checksum drift 诊断。

## 9. 禁止回退的关键设计决策

以下设计已经成为当前最小闭环基线，后续不得轻易回退：

- `TryStep` 不允许执行 `World.Tick`。
- `WorldRollbackAdapter.Simulate` 必须保持 input-only。
- `RollbackBootstrap` 不允许自建孤立 `FrameCommandApplier`。
- FrameCommand replay 必须使用 `TimeSimulator` 同一套真实 Applier。
- `FrameMismatch` 校验不得放宽。
- Restore 失败不得吞掉。
- `SaveChecksum` 必须保存到显式 frame。
- `TickMultiple` 不得作为生产假追帧路径。
- `UnityInputAdapter` 不得在 MonoBehaviour 字段层暴露 `InputButtonFlags`。
- `KeyboardBinding` / `MouseBinding` 不得重新序列化 `InputButtonFlags`。

## 10. 给下一轮 Codex 的上下文摘要

当前 RollbackSystem 已完成本地单玩家最小逻辑闭环。A1-A6 完成 frame-aware TryStep、input-only Simulate、Restore 结果化、checksum frame 修复、authoritative pre-arrival 处理、TickMultiple 假追帧禁用；A7 完成真实 FrameCommandBuffer/Applier 接线，SimulationInitializer 创建唯一真实管线，TimeSimulator 正常 Tick 与 rollback resimulation 共用同一套 Applier；MountHardening 完成显式 RollbackBootstrap 引用、TryMount 幂等、OnDisable/OnDestroy Unmount；UnityInputAdapter 已修 Inspector InputButtonFlags 报错，场景挂载后不再 not found。

当前只能宣称 local single-player logic-only minimum closure completed，不能宣称 production network rollback completed。后续优先处理正式输入所有权 / 多玩家 FrameInputSet / View restore-resync / checksum 白名单 / 网络权威协议。禁止回退 TryStep input-only、禁止 RollbackBootstrap 自建孤立 applier、禁止放宽 FrameMismatch。
