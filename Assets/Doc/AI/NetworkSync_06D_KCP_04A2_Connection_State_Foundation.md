# NETWORK-SYNC-06D-KCP-04A-2

## 前置

`04A-1 Disconnect / Rebind / Session Lifecycle Baseline Audit` 单项与 Run All 均 PASS。

当前已确认：Player Disconnect 会解除 Server Binding；缺失玩家时 Authority 停止；另一玩家 pending input 可保留；新连接以相同 PlayerID 输入后可 Rebind 并补齐 backlog；KCP 能感知 Server Loss；公共契约当前没有 Reconnect。

## 本轮

建立显式 Transport Connection State：

```text
Disconnected
Connecting
Connected
Faulted
```

传播链：

```text
INetworkInputClient
-> NetworkInputClientPump
-> NetworkRollbackClientRuntime
-> NetworkRollbackSimulationRuntime
```

本轮不实现自动重连，也不改变 Simulation 断线策略。

## KCP

```text
constructor -> Connecting
OnConnected -> Connected
OnError -> Faulted
OnDisconnected -> Disconnected
Dispose -> Disconnected
```

若 `OnError` 后 kcp2k 再触发 `OnDisconnected`，保留 `Faulted`，避免错误语义丢失。

## Raw UDP

Raw UDP 无真实连接握手/断线检测，仅把对象可工作生命周期映射为：

```text
created -> Connected
disposed -> Disconnected
```

不代表 Raw UDP 能检测远端在线。

## 有意保留

`NetworkInputClientPump` 遇到 `HasTransportError` 仍抛异常。

阶段拆分：

```text
04A-2 = Connection State Foundation
04A-3 = Session Stall / Simulation Freeze Policy
04A-4 = Reconnect / Resume
```

## PASS Gate

- Raw UDP Connected -> Disconnected。
- KCP Connecting -> Connected -> Disconnected。
- KCP Server Loss -> Faulted + Timeout。
- Pump 正确转发 ConnectionStateChanged。
- 既有 Transport Error Gate 不回退。
- Run All EditMode 0 FAIL / 0 Error。
