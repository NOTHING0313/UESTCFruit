# NETWORK-SYNC-06D-KCP-04A-3B

## 前置

- `04A-2 Connection State Foundation` PASS。
- `04A-3A NetworkSessionStallPolicy` 专项 + Run All PASS。

## 本轮目标

把纯 C# `NetworkSessionStallPolicy` 正式接入 Unity Scene Runtime。

目标行为：

```text
NetworkRollbackBootstrap.Update
-> 持续 Pump KCP
-> 评估 ConnectionState + Authority Heartbeat
-> TimeSimulator.SetSimulationRunning(...)
```

网络 Pump 与 Simulation 推进被明确分开：

```text
Simulation Frozen
!=
Network Pump Stopped
```

只有这样 Authority 恢复时才有机会解除 Stall。

## 执行顺序

`NetworkRollbackBootstrap` 增加：

```csharp
[DefaultExecutionOrder(-100)]
```

保证网络 Pump / Stall Gate 在默认执行顺序的 `TimeSimulator.Update` 之前发生。

因此达到：

```text
Bootstrap Update
1. Pump Network
2. Evaluate Session Stall
3. SetSimulationRunning

TimeSimulator Update
4. Sample Unity Input
5. 如果允许则 Runner.Update
```

避免超时已经成立后 `TimeSimulator` 仍先推进一批补偿帧。

## 两类冻结

### Local Transport Unavailable

```text
Connecting / Disconnected / Faulted
-> TransportUnavailable
-> Simulation Frozen
```

Transport 在 Pump 内进入 Faulted/Disconnected 时，不再立即销毁整个 Runtime。

Protocol Reject / Decode 等非连接生命周期错误仍保持 Fail Fast。

这为后续 `04A-4 Reconnect` 保留 Runtime / World / Snapshot 基础。

### Remote Player Missing

本客户端可能仍然：

```text
ConnectionState = Connected
```

但另一个玩家掉线后 Authority 不再增长。

达到 `_authorityStallTimeoutSeconds` 后：

```text
AuthorityTimeout
-> Simulation Frozen
```

默认：

```text
1.5 s
```

该值是 Session Stall Timeout，不是 KCP Transport Timeout。

## Recovery

如果 Simulation 因 AuthorityTimeout 冻结：

```text
Bootstrap Update 仍 Pump Network
```

后续 Authority 再次增长时：

```text
NetworkSessionStallPolicy
AuthorityTimeout -> None

Bootstrap
SetSimulationRunning(true)
```

本轮只验证 Freeze；完整 Reconnect / Resume 在 04A-4。

## 运行期证据

Stall：

```text
NetworkRollbackBootstrap EvaluateSessionStall Warning:
Stalled Reason=AuthorityTimeout,
Frame=...,
Authorities=...,
Connection=Connected
```

Summary 新增：

```text
Connection
SessionStalled
StallReason
SessionStalls
SessionResumes
LastStallFrame
LastStallAuthorities
LastStallReason
```

## 04A-3B 公网 PASS Gate

正常 2P 阶段：

- P1 / P2 正常连接、移动、Authority 推进。
- 无 Runtime / View / Pool Error。

主动关闭 P2 后：

- Server `OnDisconnected(P2)`。
- P1 自己与 Server 仍 Connected。
- Authority 停止增长。
- 约 1.5 s 后 P1 出现一次 `Stalled Reason=AuthorityTimeout`。
- 保持 P1 窗口继续运行至少 3~5 s。
- 最终 P1 Summary：
  - `SessionStalls >= 1`
  - `SessionResumes = 0`
  - `LastStallReason = AuthorityTimeout`
  - `Frame == LastStallFrame`
- 即冻结后正常逻辑帧不再继续增长。
- Server `Rejected=0 / KcpErrors=0`。

本阶段不要求重连。
