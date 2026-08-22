# NETWORK-SYNC-06D-KCP-03D-VIEW-01F

## 前置

- 03C-2B：双真实 Unity Client + Public KCP PASS。
- 03D-VIEW-01D：Boundary / Descriptor / Lifecycle + Run All EditMode PASS。
- 03D-VIEW-01E：真实 PlayMode + GameObjectPoolCenter 3/3 PASS。

## 目标

重新把所有变量组合起来：

```text
Unity Standalone P1
Unity Standalone P2
Public KCP Authority
LastKnown Prediction
Rollback / Resimulate
真实 GameObjectPool
ViewRollbackRestoreListener
ViewPrefabComponent
View Event 去重
```

01F 是本轮 View Rollback 专项的真实公网组合回归。

## 新增运行时 Audit

`NetworkViewRollbackRuntimeAudit` 只读检查：

- 网络 Player Entity 全部存活。
- 每个 Player 有稳定 `ViewPrefabComponent`。
- 每个 Player 有有效瞬时 `ViewComponent`。
- `ViewManager` 能解析 viewID。
- `EntityViewBinder` 能解析同一个 GameObject。
- 所有 Player 的 viewID 唯一。
- Bound GameObject 激活。
- `PoolItem.IsInPool == false`。
- PoolItem 对应当前 Player Prefab。
- Logic Position 与 Transform Position 一致。
- `ViewManager.ViewCount == PlayerCount`。
- `Binder BindingCount == PlayerCount`。
- 当前 Player Prefab 的 InUse PoolItem == PlayerCount。

Audit 不参与逻辑模拟。

## Scene

`ZP_NetworkTest` 的 `NetworkRollbackBootstrap`：

```text
Enable View Rollback Audit = true
```

其余继续沿用 03C-2B：

```text
Transport = Kcp
Server = 8.137.83.229
Port = 28015
Session = 0x11223344
Player Count = 2
Run In Background = true
```

## 测试动作

1. Ubuntu 以 `--players 2` 启动。
2. 同一新 Build 启动 Player1 / Player2。
3. 两端都确认 2 个 Player。
4. P1、P2 交替快速：
   `D -> 松 -> W -> 松 -> A -> 松 -> S -> 松`
5. 持续 30~60 秒，主动制造 Prediction mismatch。
6. 停止输入 2~3 秒，确认最终位置收敛。
7. 正常 Alt+F4 关闭两个客户端。
8. Ctrl+C 停 Authority。
9. 提取两个 Runtime Summary。

## PASS Gate

两个 Client：

```text
Mounted
Authorities / Applied 持续增长
PredictedFrames > 0
PredictedInputs > 0
RollbackRestore > 0
RollbackResimulate > 0

ViewAuditSamples > 0
ViewAuditFailures = 0
ViewCount = 2
BindingCount = 2
PoolInUse = 2
MaxViewCount = 2
MaxBindingCount = 2
MaxPoolInUse = 2
FirstViewAuditFailure = None

0 NetworkViewRollbackRuntimeAudit Error
0 Pool Release Error
0 Missing View Warning
0 Runtime Exception
```

人工：

```text
P1 在 P2 客户端可见
P2 在 P1 客户端可见
远端 Prediction 可有启动/停止过冲
Authority 修正后最终位置收敛
无重复 Player GameObject
无玩家突然永久消失
```

Server：

```text
Rejected = 0
KcpErrors = 0
```

通过后可以宣称：

> 双真实 Unity Client 公网 KCP 下，Prediction / Rollback 与真实对象池 View Reconcile 的组合回归通过。

仍不等于生产级网络表现完成；插值/误差平滑、断线重连、长 Session、跨平台确定性仍是后续项。
