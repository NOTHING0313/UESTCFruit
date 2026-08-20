# NETWORK-SYNC-06D-KCP-03B Runtime Handoff

## 目标

在已通过 `INetworkInputClient` Raw UDP / KCP 双后端抽象后，新增纯 C# Runtime 编排层，使上层不再直接处理具体传输客户端的 `Tick / TryReceiveAuthority / NetworkAuthorityRollbackDriver.Apply` 接线。

## 新增主链

```text
Local Player Input
        ↓
NetworkRollbackClientRuntime.SendInput
        ↓
INetworkInputClient
   ├─ RawUdp
   └─ Kcp
        ↓
NetworkInputClientPump.Tick
        ↓
ServerAuthorityFramePacket
        ↓
NetworkAuthorityRollbackDriver.Apply
        ↓
FrameInputAssembler ObserveAuthoritativeInput
        ↓
RollbackCoordinator
```

## 新增类型

- `NetworkInputClientPump`
  - 统一 `Tick`
  - Drain 全部已到达 Authority
  - 统一 Reject / Transport Error 门禁
- `NetworkRollbackClientRuntime`
  - Runtime 主入口
  - 上层只调用 `SendInput` / `Tick`
  - 暴露 Authority/乱序统计
- `NetworkRollbackClientRuntimeFactory`
  - `NetworkInputClientOptions` → Transport Client
  - 自动接 `NetworkAuthorityRollbackDriver`

## 边界

本阶段不修改：

- Protocol V1
- Prediction Policy
- FrameInputAssembler
- RollbackCoordinator
- ECS / Snapshot / Checksum
- Authority Host
- Scene / Prefab / asmdef

当前上传源码中，具体 `LocalNetworkInputClient` 的使用主要仍在 UDP/Public Validation Test；未发现已经存在的正式 Scene Runtime 网络入口，因此 03B 不伪造 Scene MonoBehaviour，而先建立可被 Simulation/Controller 持有的纯 C# Runtime Core。

## Verification Gate

Unity EditMode：

- `NetworkInputClientPumpNUnitTests`
  - Drain Authority
  - SendInput Delegate
  - Transport Error Gate
- `NetworkRollbackClientRuntimeNUnitTests`
  - Raw UDP 20 Frames
  - KCP 20 Frames

回归：

- `NetworkInputClientFactoryNUnitTests`
- `KcpNetworkInputLoopbackNUnitTests`
- 原 Raw UDP Local Input Exchange

期望：

- Unity 0 Error
- Runtime Raw UDP PASS
- Runtime KCP PASS
- Authority Applied Count 与 Server Authority Count 一致
- Coordinator CurrentFrame 不被相同 Authority 错误改变
