# NETWORK-SYNC-06D-KCP-04A-3A

## 前置

`04A-2 Connection State Foundation` 已完成专项稳定性验证与 Run All 回归。

已建立：

```text
Disconnected
Connecting
Connected
Faulted
```

并沿 `INetworkInputClient -> Pump -> ClientRuntime -> SimulationRuntime` 向上传播。

## 为什么 04A-3 先拆 A / B

直接在 Scene 中“断线就暂停 TimeSimulator”还不够。

一个重要场景是：

```text
P1 与 Server 仍然 Connected
P2 已断线
```

此时 P1 自己的 KCP ConnectionState 仍是 `Connected`，但 Authority Server 因缺少 P2 不再产生完整帧。

因此 Session 是否应该继续推进，不能只看本机 Transport：

```text
Transport Connected != Session Healthy
```

还必须看 Authority 是否持续前进。

## 04A-3A 目标

新增纯 C#：

```text
NetworkSessionStallPolicy
```

只负责计算：

```text
ConnectionState
+
Authority Progress Heartbeat
=
ShouldRunSimulation
```

不直接依赖：

- Unity
- MonoBehaviour
- TimeSimulator
- RollbackCoordinator
- View / Pool

## Stall Reason

```text
None
TransportUnavailable
AuthorityTimeout
```

### TransportUnavailable

以下任意状态立即暂停：

```text
Connecting
Disconnected
Faulted
```

### AuthorityTimeout

Transport 仍是 Connected，但一段时间没有新的 Authority：

```text
Connected
AuthorityCount 不再增长
超过 AuthorityTimeout
-> Stall
```

这正好覆盖“远端玩家掉线，但本客户端到 Server 的 KCP 仍健康”的情况。

### 自动解除 AuthorityTimeout

如果之后：

```text
AuthorityCount 再次增长
```

Policy 自动恢复：

```text
AuthorityTimeout
-> None
```

为下一阶段 Rebind / Reconnect 后重新推进 Simulation 提供基础。

## 本轮为什么还不碰 TimeSimulator

当前 SimulateRunner 一旦进入 `TickFrame`：

```text
BeforeTick
-> World.Tick
-> AfterTick
```

没有中途 Cancel Tick 能力。

同时 Network Runtime 目前也会在 `BeforeTick` Pump KCP。

如果不先定义明确 Policy，就直接改 Scene Update 顺序、TimeSimulator 或 Runner，容易把 Transport、Session 与固定帧所有权同时改乱。

所以：

```text
04A-3A = Pure Session Stall Policy
04A-3B = Scene / TimeSimulator Integration
```

04A-3B 再决定最小安全接线点。

## 当前建议 Timeout

本轮测试只验证算法，不固定生产 Inspector 数值。

后续 Scene 初始建议从：

```text
Authority Stall Timeout = 1~2 秒
```

开始验证。

它不是 KCP Transport Timeout。

区别：

```text
Authority Stall Timeout
-> 先冻结 Simulation，防止无限 Prediction

KCP Timeout
-> 判断底层连接本身已经失效
```

Authority Stall 应明显早于生产 KCP Timeout。

## PASS Gate

`NetworkSessionStallPolicyNUnitTests`：

- Authority 持续推进 -> Running。
- Authority 停止到阈值 -> AuthorityTimeout。
- 新 Authority 到达 -> 自动恢复。
- Connecting / Disconnected / Faulted -> TransportUnavailable。
- Connection 恢复且仍在 Authority heartbeat window -> Running。
- AuthorityCount 倒退 -> 明确拒绝。

之后 Run All EditMode 必须 0 FAIL / 0 Error。
