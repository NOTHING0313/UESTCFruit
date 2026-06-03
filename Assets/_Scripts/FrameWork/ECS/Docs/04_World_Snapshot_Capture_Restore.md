# 04. World Snapshot Capture / Restore

## 1. 文档定位

本文档说明 ECS Core 当前提供的 World-level Snapshot Capture / Restore 接口，以及它与外部 RollBackSystem 或调度器之间的职责边界。

ECS Core 只负责捕获和恢复 `World` 的确定性运行时状态，不负责完整 Rollback 流程。

---

## 2. 对外调用入口

`World` 直接提供 Snapshot API：

```csharp
public EcsWorldSnapshot CaptureSnapshot(int frameNumber);
public bool TryCaptureSnapshot(int frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result);

public void RestoreSnapshot(EcsWorldSnapshot snapshot);
public bool TryRestoreSnapshot(EcsWorldSnapshot snapshot, out EcsWorldSnapshotRestoreResult result);
```

外部系统也可以通过受限接口依赖 Snapshot 能力：

```csharp
using Contracts;
using ECSFrameWork;

IEcsWorldSnapshotProvider snapshotProvider = world;

if (snapshotProvider.TryCaptureSnapshot(frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult captureResult))
{
    // 外部系统自行保存 snapshot。
}

if (!snapshotProvider.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult restoreResult))
{
    // 外部系统自行处理失败。
}
```

`IEcsWorldSnapshotProvider` 位于 `Contracts` 命名空间。`EcsWorldSnapshot`、`EcsWorldSnapshotCaptureResult` 和 `EcsWorldSnapshotRestoreResult` 仍位于 `ECSFrameWork` 命名空间。

`IEcsWorldSnapshotProvider` 只包含 `TryCaptureSnapshot` 和 `TryRestoreSnapshot`，不暴露 `EntityManager`、`ComponentManager`、`ArcheTypeManager`，也不包含抛异常版本。

---

## 3. Capture 稳定边界

Snapshot Capture 只允许在稳定帧边界调用：

```text
World.CurrentState == WorldStates.Idle
PendingCommandCount == 0
PendingSystemCommandCount == 0
```

推荐语义：

```text
frame N 的 snapshot 表示 frame N 已完成后的 ECS World 状态。
```

因此推荐调用顺序是：

```text
1. 写入 frame N 输入或命令。
2. World.Tick(frame N)。
3. World 回到 Idle。
4. TryCaptureSnapshot(frame N)。
5. 外部系统自行保存 snapshot。
```

---

## 4. Restore 稳定边界

Snapshot Restore 只允许在稳定帧边界调用：

```text
World.CurrentState == WorldStates.Idle
PendingCommandCount == 0
PendingSystemCommandCount == 0
```

pending command 不为空时 Restore 会失败，ECS Core 不会强制清空 pending 后恢复。

Restore 成功后，`World` 会恢复到 snapshot 记录的 ECS 状态。Restore 失败时返回 `EcsWorldSnapshotRestoreResult`，调用方应读取 `ErrorMessage` 并决定后续处理。

---

## 5. Snapshot 包含内容

`EcsWorldSnapshot` 当前包含：

```text
FrameNumber
Registered component type order
EntityManager slot / version / alive / dataCount / free id order
ComponentStore dense components
Singleton mappings
```

Restore 已覆盖的能力：

```text
Entity ID / Version / Alive
future Entity ID reuse order
ComponentTypeRegistry 注册顺序
ComponentStore dense 顺序和值
Entity mask 与 ArcheType / Query 重建
Singleton 映射
成功 Restore 后清空 WorldEventBuffer
invalid snapshot restore 不污染当前 World
```

---

## 6. Snapshot 不包含内容

Snapshot 不包含：

```text
Snapshot ring buffer
输入历史保存
回滚触发条件
Restore 后输入重放
TryRollbackToFrame
RollbackRuntimeController
WorldEventBuffer 历史事件
Unity View / GameObject / Transform / Prefab / Scene
网络同步 / checksum
System 私有字段
非纯值组件的深拷贝语义
```

这些内容属于 RollBackSystem 或上层模块。

---

## 7. 组件纯值约束

参与 Snapshot / Restore 的 `IComponentData` 应优先保持纯值数据。

不建议在可回滚组件中持有：

```text
GameObject
Transform
UnityEngine.Object
List<T>
Dictionary<TKey, TValue>
其他可变引用对象
```

当前 Snapshot 会保存 boxed component value。对于 struct 中的引用类型字段，ECS Core 不提供深拷贝保证。

---

## 8. WorldEventBuffer 策略

`WorldEventBuffer` 是 Logic -> View / UI / Audio 的一次性事件输出通道，不是持久 ECS 状态。

当前策略：

```text
Snapshot 不捕获 WorldEventBuffer 历史事件。
TryRestoreSnapshot 成功后会清空 WorldEventBuffer。
Restore 后的事件重放、过滤、表现层消费策略由外部 RollBackSystem 或 View 调度层决定。
```

ECS Core 不负责 WorldEvent 历史重放。

---

## 9. ECS 与外部回滚系统职责边界

ECS Core 负责：

```text
CaptureSnapshot / TryCaptureSnapshot
RestoreSnapshot / TryRestoreSnapshot
IEcsWorldSnapshotProvider 接口边界
恢复 Entity / Component / ArcheType / Query / Singleton
成功 Restore 后清空 transient event
拒绝非稳定边界 Capture / Restore
```

RollBackSystem 或上层模块负责：

```text
保存多少帧 snapshot
何时捕获 snapshot
何时触发回滚
输入历史保存
Restore 后重新模拟
View 重绑定或重同步
WorldEvent 重放或过滤策略
网络同步 / checksum
```

不要把 ECS Core 理解为完整 Rollback 实现。ECS Core 只提供可被 RollBackSystem 调用的 World-level Snapshot Capture / Restore 能力。

---

## 10. Phase 6 验收范围

Phase 6 bootstrap 已验证：

```text
Capture / Restore 边界条件
Entity ID / Version 恢复
future Entity ID reuse order
Component value restore
ComponentStore dense order
Query / ArcheType rebuild
Singleton mapping restore
WorldEventBuffer restore 后清空
invalid snapshot restore 不污染当前 World
Store 不在 snapshot 中时被移除
```

该能力已具备交付给外部 RollBackSystem 或调度器使用的基础接口条件。
