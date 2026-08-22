# NETWORK-SYNC-06D-KCP-03D-VIEW-01D

## 01C Audit-Fix 最终证据

修正 StructuralChangeBuffer 测试时序后，四项均真正抵达目标生命周期边界，并全部 FAIL：

### Created Entity -> Rollback Before Creation

```text
Spawn=1
Release=0
Live=1
ViewCount=1
```

结论：View 已 Spawn、但 Binder 尚未建立时存在 orphan rollback 窗口。

### Destroyed Entity -> Rollback Before Destroy

```text
Spawn=1
Release=1
Live=0
ViewCount=0
Restored Entity Missing View
```

同时出现：

```text
ViewRollbackRestoreListener ... Missing View Transform
```

结论：Snapshot 能复活逻辑 Entity，但当前没有稳定数据回答“该 Entity 应重新 Spawn 哪个 Prefab”。

### Pooled View Destroy -> Rollback

```text
Spawn=1
Release=1
Reuse=0
Live=0
ViewCount=0
```

结论：对象池已正确 Release，但 Rollback 后没有 Respawn Request，因此池化对象无法复用恢复。

### Consumed View Event -> Rollback/Resimulate

```text
EffectAfterResim=1
BufferedAfterResim=1
FinalEffect=2
```

结论：Rollback frame 虽未播放历史事件，但 `WorldViewEventConsumer` 直接 return 导致事件残留，下一正常帧重复播放。

## 01D 修复原则

不把 Unity GameObject / Transform / viewID 放进 Snapshot。

新增两种状态分类：

```text
IRollbackTransientComponent
    Snapshot: NO
    Logic Checksum: NO

ILogicChecksumIgnoredComponent
    Snapshot: YES
    Logic Checksum: NO
```

`IRollbackTransientComponent` 继承 `ILogicChecksumIgnoredComponent`。

### ViewPrefabComponent

新增稳定表现描述：

```text
ViewPrefabComponent.prefabID
```

它：

- 只保存整数 prefabID；
- 不保存 Unity Object；
- 进入 Snapshot，用于 Rollback 后重建表现；
- 不进入 gameplay logic checksum。

因此 Snapshot 的边界从“只保存 gameplay 数据”细化为：

> Snapshot 不保存 Unity 实例状态；允许保存恢复表现所需的稳定、确定、无 Unity 引用的数据描述。

### 关闭 Spawn -> Binder orphan 窗口

`ViewSpawnSystem` 新增可选 `IEntityViewBinder`。

真实 GameObject Spawn 成功后，同帧立即：

```text
SpawnView
-> Bind(Entity, ViewID)
-> Structural Set ViewComponent
```

`EntityViewBindingSystem` 继续保留，作为下一帧扫描/清理路径。

### Rollback Restore Reconcile

`ViewRollbackRestoreListener`：

1. Snapshot 后创建、Restore 后不存在的 Entity：
   - Unbind
   - DestroyView / Pool Release

2. Entity 与原 View 都存在：
   - 重建瞬时 ViewComponent
   - 继续复用原 GameObject

3. Entity 被 Restore 复活，但旧 View 已 Release：
   - 清 stale binding
   - 从 ViewPrefabComponent 生成 PrefabViewRequestComponent
   - Resimulate 时重新 Spawn / Pool Reuse

4. 扫描所有拥有 ViewPrefabComponent 但缺 View 的 Entity：
   - 补 Spawn Request
   - 防止 Binder 历史已经被清理后无法恢复

### Rollback Event

`WorldViewEventConsumer` 在 `context.isRollback` 时：

```text
不播放
+
ClearWorldEvents
```

避免历史事件泄漏到下一正常帧重复播放。

## 01D Gate

第一层：

- ViewRollbackLifecycleNUnitTests 4/4 PASS
- ViewRollbackBoundaryNUnitTests 4/4 PASS
- ViewRollbackDescriptorNUnitTests 2/2 PASS

第二层：

Run All EditMode，0 regression。

01D PASS 后仍不直接宣称生产级 View rollback lifecycle：

后续仍需 Scene / Pool / Buff UI / Effect 的真实 PlayMode smoke 与双客户端回归。
