# NETWORK-SYNC-06D-KCP-03C-2A Scene Wiring

## 目标

把已通过 03C-1 的 `NetworkRollbackSimulationRuntime` 正式接入现有 Unity Scene 生命周期。

本阶段先做 **1 Player Scene + Public KCP Authority**，只验证 Scene / UnityInputAdapter / TimeSimulator / Runner / View 与正式网络 Runtime 能共存。

2 Player 双客户端 Scene 放到 03C-2B，避免一次引入两个 Unity 进程与远端控制问题。

## 本轮正式改动

### TimeSimulator

新增 `SetSimulationRunning(bool)`：

- 网络握手期间：`false`
- KCP Ready + Runtime Mount 后：`true`
- 暂停期间仍采样 Unity Input

因此 Runtime 一定在 Runner Frame 0 挂载，不存在握手期间 Runner 偷跑。

### SimulationInitializer

1. `TimeSimulator.SetInputAdapters(_inputAdapter)` 成为输入采样唯一入口。
2. 不再在 `SimulationInitializer.Update` 再采样一次。
3. 新增 `SetDirectInputWriteEnabled(bool)`，真正订阅/退订：
   `Runner.BeforeTick +=/-= UnityInputAdapter.WriteInputToWorld`
4. 网络模式挂载时关闭旧直接输入路径，不再使用 Reflection 把 `_world` 置空。
5. 本地 `PlayerInputSnapshotComponent.playerID` 显式使用 `UnityInputAdapter.PlayerID`。
6. 新增 NetworkRollbackBootstrap 自动解析入口。

### NetworkRollbackBootstrap

Scene MonoBehaviour：

- 冻结 TimeSimulator 正常推进
- 校验单机 `RollbackBootstrap` 必须关闭
- 关闭旧直接输入写入
- 建立 PlayerID → Entity
- 创建 `NetworkRollbackSimulationRuntime`
- KCP 握手
- Ready 后清理握手期一次性输入
- `Runtime.Mount`
- 恢复正常 Simulation
- Unity Update 持续 Pump Authority

## 03C-2A Scene Gate

建议复制现有 `ZP_Test.unity` 为新的网络 smoke Scene，不覆盖已通过的 View smoke 场景。

Scene 配置：

- `SimulationInitializer`：保持原有 View/Input/Buff 引用
- 同 GameObject 添加 `NetworkRollbackBootstrap`
- `RollbackBootstrap`：不存在或禁用
- `UnityInputAdapter.PlayerID = 1`

NetworkRollbackBootstrap：

- Enable = true
- Transport Mode = Kcp
- Server Address = 8.137.83.229
- Server Port = 28015
- Session Id = 0x11223344
- Player Count = 1
- Connect Timeout = 5
- Snapshot Capacity = 256
- Snapshot Interval = 10

Ubuntu：

```bash
~/NetworkSyncAuthorityHostKcp kcp-server \
  --port 28015 \
  --players 1 \
  --session 0x11223344
```

PlayMode 期望：

- NetworkRollbackBootstrap MountWhenReady Log: Mounted
- Server `BIND Player=1`
- Server Authority 持续增长
- WASD 可以驱动玩家 View
- Unity Console 0 Error
- 停止 PlayMode 时 KCP 正常 Disconnect

## 为什么先 1P

03C-1 已经在真实 SimulateRunner 上证明 2P Prediction / Out-of-order Authority / Rollback / Checksum 收敛。

03C-2A 的新风险不是多人算法，而是：

- Unity Scene 生命周期
- TimeSimulator 输入所有权
- Runner Frame 0 挂载
- View/Buff/FrameCommand 与 Network Runtime 共存

所以先用 1P Authority 把 Scene 接线变量隔离。

## 本阶段仍不宣称

- 2 个真实 Unity 客户端联机 Scene 已通过
- View rollback lifecycle 完整正确
- Ghost / Prediction View 已实现
- Player Build / IL2CPP 已验证

这些进入后续 03C-2B / View rollback 验证。
