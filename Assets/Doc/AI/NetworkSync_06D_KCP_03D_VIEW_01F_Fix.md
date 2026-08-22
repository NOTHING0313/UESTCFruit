# NETWORK-SYNC-06D-KCP-03D-VIEW-01F-FIX

01F 首轮网络/回滚主体正常，但不能直接判 PASS。

已确认：
- P1 Rollback 1127，P2 Rollback 1504。
- 双方最终视觉基本收敛。
- MaxViewCount / MaxBindingCount / MaxPoolInUse 均为 2。

仍有两类问题：

1. P2 Frame1 Logic/View Position Mismatch。
根因：真实对象池先按 worldPosition Spawn，随后 Provider 使用 `SetParent(worldViewRoot, false)`，导致非零父节点下 worldPosition 被解释成 localPosition。ViewSync 下一帧才修正，因此首帧出现跳变。

2. P2 退出出现 `PoolID 0 Not Found`。
根因：Unity 不保证 `GameObjectPoolCenter.OnDestroy` 与 `SimulationInitializer.OnDestroy` 顺序。PoolCenter 先 ClearAllPools 后，ViewManager 再 Release 就会报伪错误。

修复：
- Provider 直接把 worldViewRoot 作为 `GetInstance(..., parent)` 参数。
- GameObjectPoolCenter 增加 IsShuttingDown。
- teardown 时 Provider 不再向已结束生命周期的 PoolCenter Release。
- View Audit 改到 `SimulateRunner.AfterTick` 每个正常固定帧采一次。
- Runtime Summary 使用最后稳定 Audit Sample，而不是 Scene teardown 时的即时 Pool 状态。

重新验证 01F 时要求：
- ViewAuditFailures=0
- ViewCount=2
- BindingCount=2
- PoolInUse=2
- MaxViewCount=MaxBindingCount=MaxPoolInUse=2
- FirstViewAuditFailure=None
- 0 PoolID Not Found
- RollbackRestore/RollbackResimulate > 0
- Server Rejected=0 / KcpErrors=0
