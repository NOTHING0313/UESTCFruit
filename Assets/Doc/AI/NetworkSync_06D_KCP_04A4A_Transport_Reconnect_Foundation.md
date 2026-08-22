# NETWORK-SYNC-06D-KCP-04A-4A

## 前置

`04A-3B Scene / TimeSimulator Freeze Integration` 公网证据已通过。

实际测试中先断开的是 Player1，因此留下运行的是 Player2。Player2 在 Authority 停止后进入：

```text
Stalled Reason=AuthorityTimeout
Connection=Connected
Frame=2932
LastStallFrame=2932
SessionStalls=1
SessionResumes=0
```

且 View/Pool/Runtime 0 Error，Server `Rejected=0 / KcpErrors=0`。

这已经证明 Freeze Policy 与 PlayerID 无关，作用于“仍存活的客户端”。

## 04A-4 为什么继续拆 A / B

完整 Session Resume 至少包括：

```text
Transport Reconnect
-> Server Player Rebind
-> Authority Resume
-> Simulation Resume
-> Rollback / View Convergence
```

本轮只先完成第一层：

```text
04A-4A Transport Reconnect Foundation
```

不立即让 Bootstrap 自动重连。

## 新契约

新增：

```text
IReconnectableNetworkInputClient
```

而不是直接把 `Reconnect()` 塞进 `INetworkInputClient`。

原因：

```text
KCP
= Connection-oriented reliable transport
= 有明确 Connect / Disconnect / Reconnect

Raw UDP
= Connectionless
= 不应该伪装成具有相同 Reconnect 语义
```

因此：

```text
KcpNetworkInputClient
-> INetworkInputClient
-> IReconnectableNetworkInputClient

LocalNetworkInputClient
-> INetworkInputClient only
```

## KCP Reconnect 语义

只允许：

```text
Disconnected
Faulted
```

调用 Reconnect。

Reconnect 时：

- 保留 `SessionId`
- 保留 `PlayerID`
- 保留应用层 `_nextSequence`
- 清旧 Authority Queue
- 清 Decode / Reject / KCP Error
- `State -> Connecting`
- 复用 kcp2k `KcpClient.Connect`

上游 kcp2k 的 `Connect()` 自身会 `Reset(config)`，因此每次是新的 KCP peer/cookie/socket 生命周期，但 UESTCFruit 的逻辑 Client 身份保持不变。

## 为什么保留 Sequence

这是“同进程、同 Session 的 Transport 恢复”，不是创建全新游戏 Session。

因此：

```text
F1 Send Sequence=1
Fault
Reconnect
F2 Send Sequence=2
```

比重新从 1 开始更适合作为诊断和未来 Protocol Resume Gate 的基础。

当前 Server 仍未用 Client Sequence 作为 Session Resume Gate，本轮不改 Protocol。

## Runtime 暴露

能力沿：

```text
NetworkInputClientPump
-> NetworkRollbackClientRuntime
-> NetworkRollbackSimulationRuntime
```

暴露：

```text
CanReconnect
Reconnect()
```

Bootstrap 暂不自动调用。

## PASS Gate

`KcpNetworkInputReconnectNUnitTests`：

1. 同一个 KCP Client：
   - Connected
   - Server blackhole
   - Faulted
   - Server 清旧连接
   - Reconnect
   - Connected
   - 同 PlayerID Rebind
   - Authority 再生成
   - Sequence 1 -> 2
   - Reject=0

2. Connected 状态直接 Reconnect 必须拒绝。

3. Reconnect Capability 只属于 KCP，不强加给 Raw UDP。

之后 Run All EditMode 0 FAIL / 0 Error。

通过后进入：

```text
04A-4B
Bootstrap Auto Reconnect
+ Authority Resume
+ Simulation Resume
```
