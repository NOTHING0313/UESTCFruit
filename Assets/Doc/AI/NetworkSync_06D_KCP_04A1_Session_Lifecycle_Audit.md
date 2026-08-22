# NETWORK-SYNC-06D-KCP-04A-1

## 阶段定位

`03D-VIEW-01F` 已完成双真实 Unity Client + Public KCP + Prediction + Rollback + Real Pool + View Reconcile 组合回归。

下一阶段进入 Session Lifecycle。04A 不直接实现“自动重连”，先审计当前代码真正具备哪些能力，避免把 Transport Reconnect、Simulation Resume 与 Session Resync 混成同一个问题。

## 本轮不修改生产代码

只新增 `KcpSessionLifecycleAuditNUnitTests`。

目的不是证明当前已经支持完整重连，而是得到以下基线证据。

## Audit 1：Player Disconnect / Rebind / Backlog

时间线：

```text
P1 + P2 Connected
F1 -> Authority
P2 Disconnect
P1 继续发送 F2~F6
```

预期当前 Server：

- `OnDisconnected` 删除 P2 的 Connection / Player Binding。
- 因缺少 P2，Authority 停在 F1。
- P1 的 F2~F6 仍被 `ServerInputFrameCollector` 保留为未完成帧。

随后创建一个新的 `KcpNetworkInputClient(PlayerID=2)`：

```text
New P2 Connected
New P2 发送 F2~F6
```

预期：

- 第一条有效 Input 才重新绑定 Player2。
- Server 利用已有 P1 backlog 补齐 F2~F6。
- Authority 恢复生成。
- 新客户端应用层 `Sequence` 从 1 重新开始；当前 Authority Server 并未使用 Client Packet Sequence 作为 Session Resume Gate。

该结果只能证明 **Server Transport Rebind + Input Backlog** 能工作，不等于完整游戏 Session Resume。

## Audit 2：Server Loss Detection

测试使用 250 ms KCP Timeout，模拟服务器 Socket 消失。

当前 `KcpNetworkInputClient` 预期：

```text
Ready
-> KCP Timeout
-> IsConnected=false
-> IsReady=false
-> LastKcpError=Timeout
-> SendInput throws
```

正式公网配置仍使用 kcp2k 默认约 10 s Timeout，本轮不会修改该生产参数。

## Audit 3：Reconnect API Gap

当前公共契约：

```text
INetworkInputClient
- Tick
- SendInput
- TryReceiveAuthority
- Dispose
```

没有：

```text
Connect
Reconnect
ConnectionState event
Disconnected event
```

`KcpNetworkInputClient` 构造时立即 Connect，但 wrapper 没有公开重新 Connect 的入口。

虽然上游 kcp2k `KcpClient.Connect` 本身支持 Reset 后再次连接，当前 UESTCFruit wrapper/runtime 尚未把这个能力建模出来。

## 当前架构上的 Session Resume 难点

即使下一步加入 Transport Reconnect，也不能立刻宣称 Session Resume 完成。

例如：

```text
P1 在 F100 断开并冻结
P2 继续预测到 F200
Server 保存 P2 的 F101~F200 pending input
P1 重连后从 F101 开始恢复
```

P1 与 P2 已产生逻辑帧差。

双方同速继续 Tick，P1 不会自动追上 P2，所以至少还需要定义一种策略：

- 全 Session Authority Stall 后所有客户端冻结；或
- Reconnect Client Catch-up；或
- Server Snapshot / Resync State；或
- 重新建立 Session。

当前 Protocol V1 只有 `ClientInput` 与 `ServerAuthorityFrame`，没有 Presence / Disconnect / Resume / Snapshot Resync 包。

## 04A 后续拆分建议

如果 04A-1 基线与预期一致：

1. `04A-2 Connection State Foundation`
   - 明确 Connecting / Connected / Disconnected / Faulted。
   - Transport Disconnect 不再直接与 Simulation 异常混在一起。
   - 上层能可靠收到断线状态。

2. `04A-3 Session Stall Policy`
   - 优先选择最小可验证策略：Authority 长时间不推进时冻结本地 Simulation。
   - 避免一个客户端断开后其他客户端无限 LastKnown Prediction。

3. `04A-4 Reconnect + Bounded Catch-up`
   - 在冻结策略成立后再做同 Session Rebind / 恢复。

本轮只做 04A-1，不预先改 Protocol。
