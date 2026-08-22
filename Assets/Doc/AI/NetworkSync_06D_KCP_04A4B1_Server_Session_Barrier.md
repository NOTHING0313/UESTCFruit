# NETWORK-SYNC-06D-KCP-04A-4B-1

## 前置

`04A-4A Transport Reconnect Foundation` 已稳定通过：

- `FaultedClient_ReconnectsSameInstance_RebindsAndContinuesSequence` 连续 10/10 PASS。
- `KcpNetworkInputReconnectNUnitTests` 全 PASS。
- Run All EditMode PASS。

## 为什么不能立刻做 Scene 自动恢复

04A-1 曾验证过旧行为：

```text
P2 Disconnect
P1 继续发送
Server 保留 P1 pending backlog
P2 Rebind 后补齐 backlog
```

这个行为对“只验证 Transport Rebind”有价值，但对完整 Session Resume 有一致性风险。

原因：

```text
客户端冻结时必须回到一个共同权威边界
```

如果 Server 仍保留断线前/断线期间某个客户端的预测窗口输入，那么恢复后双方从最后 Authority 重新开始时，Server 可能把“旧 pending 输入”和“新恢复输入”混合，甚至出现同玩家同帧内容冲突。

因此 04A-4B 先建立 Server Session Barrier。

## 新规则

### Player Disconnect

任何已绑定 Player 断开：

```text
移除 Player Binding
+
ClearPendingFrames
```

只清：

```text
尚未完成的 pending frame
```

不清：

```text
已经生成的 Authority
LastAuthorityFrame
completed history
```

### Session 成员未齐

当：

```text
BoundPlayerCount < ExpectedPlayerCount
```

surviving client 的新输入：

```text
不进入 ServerInputFrameCollector
不算 Protocol Reject
只计 DroppedIncompleteSessionInputCount
```

因此掉线窗口不再制造新的 pending backlog。

## 关键边界

恢复时的唯一共同起点明确为：

```text
LastAuthorityFrame
```

Server 端保证：

```text
LastAuthorityFrame 之后没有旧 pending 输入残留
```

这为下一步客户端：

```text
Rollback/Align -> LastAuthorityFrame
Reconnect -> Rebind
Fresh Input -> Authority Resume
```

建立安全前提。

## 新诊断

`KcpNetworkInputServer`：

```text
LastAuthorityFrame
PendingFrameCount
DroppedPendingFrameCount
DroppedIncompleteSessionInputCount
```

这些都属于 Session Lifecycle 诊断，不改变 Protocol V1。

## 对历史 04A-1 的说明

04A-1 的“backlog 可保留并补齐”是当时的真实 baseline。

04A-4B-1 有意改变该策略：

```text
旧 baseline：保留 incomplete backlog
新 resume policy：断线即建立 barrier，丢弃 incomplete backlog
```

因此旧测试被更新为验证：

```text
Missing Player -> Authority Stall
Missing Window -> PendingFrameCount=0
```

这不是回归退化，而是 Session Resume 策略升级。

## PASS Gate

专项：

```text
KcpSessionBarrierNUnitTests
```

要求：

1. Disconnect 清掉既有 pending。
2. 缺员期间新输入不积累 pending。
3. Rebind 后必须用 fresh input 从 Authority 边界重新建立帧。
4. Completed Authority boundary 保留。
5. Rejected=0。

之后 Run All EditMode 必须 0 FAIL / 0 Error。

通过后进入：

```text
04A-4B-2
Client Align To LastAuthorityFrame
+ Bootstrap Auto Reconnect
+ Authority / Simulation Resume
```
