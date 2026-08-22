# NETWORK-SYNC-06D-KCP-03C-2B

## 目标

两个真实 Unity Player 同时连接 Ubuntu KCP Authority：

```text
Player 1 Unity Client ─┐
                      ├─ Ubuntu KCP Authority --players 2
Player 2 Unity Client ─┘
```

双方 Scene 都创建相同的 Player1 / Player2 ECS Entity，分别只采集自己的 Local Player 输入。

## 本轮解决的关键问题

### 1. 不允许“Local Entity 先创建”的客户端差异

旧 03C-2A 对 1P 没问题，但直接扩成 2P 会出现：

- P1 客户端：Entity1 = Player1，Entity2 = Player2
- P2 客户端：Entity1 = Player2，Entity2 = Player1

这会导致跨客户端逻辑身份不一致。

03C-2B 改为所有客户端都按：

```text
PlayerID 1
PlayerID 2
...
```

升序创建 Entity。

因此 2P 时双方都是：

```text
Entity1 = Player1
Entity2 = Player2
```

### 2. 初始位置与 Local Player 无关

`NetworkPlayerLayout`：

- P1 = X -1.5
- P2 = X +1.5

当 spacing=3。

所以两个客户端看到完全相同的初始布局。

### 3. Debug TestBuff 不能只加到 Local Player

TestBuff 属于逻辑状态。

网络模式现在对所有 Player 按 PlayerID 升序执行同样的 AddBuff，避免：

- P1 世界给 Player1 Buff
- P2 世界给 Player2 Buff

这种初始状态分叉。

### 4. 同一 Build 启动两个 Player

新增命令行：

```text
--network-player-id=1
--network-player-count=2
--network-server=8.137.83.229
--network-port=28015
--network-session=0x11223344
```

`NetworkRollbackBootstrap.Awake` 解析后，在 `SimulationInitializer` 创建玩家之前调用 `UnityInputAdapter.SetPlayerID`。

因此不用制作 P1/P2 两套 Scene 或两份 Build。

### 5. 失焦继续运行

网络模式默认：

```csharp
Application.runInBackground = true;
```

切换 P1/P2 窗口输入时，另一客户端仍继续逻辑 Tick 和 KCP。

### 6. 退出时输出网络 Runtime 摘要

包含：

- Frame
- Authority Received / Applied
- Out-of-order Authority
- Predicted Frames / Inputs
- Rollback Restore / Resimulate

远端玩家改变输入时，至少应出现 Prediction；如果 Authority 与预测不一致，应出现 Rollback Restore / Resimulate。

## 不宣称

03C-2B 通过后可以宣称：

- 两个真实 Unity 客户端公网 KCP 联机 Scene 闭环
- 双方 Local/Remote Player 输入互相可见
- Scene Runtime 中真实 Prediction/Authority/Rollback 链工作

仍不能宣称：

- View rollback 生命周期生产级正确
- Ghost / 插值 / 渲染延迟隐藏完成
- 跨 Windows/Linux/IL2CPP 浮点严格确定性
- 长时间生产级 Session/重连/掉线恢复
