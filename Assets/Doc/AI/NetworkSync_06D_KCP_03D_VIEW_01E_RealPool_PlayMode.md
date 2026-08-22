# NETWORK-SYNC-06D-KCP-03D-VIEW-01E

## 前置结论

`03D-VIEW-01D` 已完成：

- Boundary 4/4 PASS
- Descriptor 2/2 PASS
- Lifecycle 4/4 PASS
- Run All EditMode：123 PASS / 0 FAIL / 0 Error

01D 已证明纯逻辑测试环境中的 View Rollback 生命周期闭环成立。

## 01E 目标

把 01D 修复放进 **真实 Unity PlayMode + 真实项目 GameObjectPoolCenter** 中验证。

本阶段不访问公网、不启动 KCP，不增加网络变量，只验证表现层运行时：

```text
SimulateRunner
→ RollbackCoordinator
→ Snapshot Restore
→ ViewRollbackRestoreListener
→ ViewSpawn / ViewDestroy / EntityDestroy
→ GameObjectPoolViewInstanceProvider
→ GameObjectPoolCenter
→ PoolItem
```

## 为什么必须有 01E

01D Lifecycle 测试使用了可控 Tracking Provider。

真实项目 Scene 使用的是：

```text
GameObjectPoolViewInstanceProvider
→ GameObjectPoolCenter
→ GameObjectPool
```

真实池额外包含：

- PoolItem.PoolID
- IsInPool 防重复 Release
- 初始池扩容
- SetActive
- Transform reparent
- OnGetFromPool / OnReleaseToPool
- 全局 Singleton 生命周期

因此 01D PASS 不能自动等价为真实对象池 PASS。

## 三个 PlayMode Gate

### 1. Created Entity -> Rollback -> Real Pool Release / Reuse

```text
F0 Snapshot
F1 创建 Entity
F2 Spawn 真实池 View
Authority 修正 F1
Rollback -> F0
```

要求：

- Entity 消失
- ViewManager ViewCount = 0
- 原 GameObject inactive
- PoolItem.IsInPool = true
- Pool 中 InUse = 0

随后同 Prefab 再 Spawn：

- 必须重新得到同一个刚 Release 的 GameObject
- PoolItem.IsInPool = false
- 只有一个 InUse View

### 2. Destroyed Entity -> Rollback -> Respawn Same Pool Instance

```text
F1 Spawn
F2 Binder + Snapshot
F3 预测 DestroyRequest
F4 Release + Destroy Entity
Authority 修正 F3
Rollback -> F2
```

要求：

- Snapshot 复活 Entity
- ViewPrefabComponent 恢复
- ViewRollbackRestoreListener 产生 Spawn Request
- GameObjectPool 复用被 Release 的原实例
- Binder / ViewComponent / ViewManager 一致

### 3. ViewEvent PlayMode 去重

正常 F1 已消费一次 DamageWorldEvent。

Rollback / Resimulate 再产生同一历史事件：

- rollback frame 不播放
- WorldEventBuffer 被清空
- 下一正常帧不重复播放

最终 EffectCount 仍为 1。

## 运行环境

Editor-driven PlayMode Test 使用 Empty Scene：

- 不污染 ZP_Test / ZP_NetworkTest
- 不依赖 Ubuntu
- 不依赖保存场景
- 使用真实 MonoBehaviour GameObjectPoolCenter

## 01E PASS Gate

```text
ViewRollbackRealPoolPlayModeTests
3 / 3 PASS
Console / LogAssert 0 Unexpected Error
```

通过后再进入 01F：

> 双真实 Unity Client + Public KCP 的 View Rollback Regression。

01F 才重新把 Network / Public Authority / Scene / Pool / View 全部组合起来。
