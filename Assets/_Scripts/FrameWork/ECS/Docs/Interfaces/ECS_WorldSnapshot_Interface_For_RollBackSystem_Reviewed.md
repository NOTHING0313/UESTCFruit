# ECS World Snapshot 接口说明（提供给 RollBackSystem 负责人）

## 1. 文档目的

本文档说明 ECS Core 已提供的 **World-level Snapshot Capture / Restore** 能力，以及 RollBackSystem 或外部调度器应如何通过接口捕获与恢复 ECS World 状态。

这份文档只定义 **ECS 侧交付接口与使用边界**。  
RollBackSystem 的历史缓冲、输入历史、回滚触发、重模拟流程和表现层同步策略，不属于 ECS Core 的实现范围。

---

## 2. 当前交付状态

ECS Core 已完成并通过测试的能力如下：

| 能力 | 状态 |
|---|---|
| World 快照捕获 | 已完成 |
| World 快照恢复 | 已完成 |
| Entity ID / Version / Alive 恢复 | 已通过测试 |
| Entity free ID reuse order 恢复 | 已通过测试 |
| ComponentTypeRegistry 注册顺序恢复 | 已通过测试 |
| ComponentStore dense 顺序与组件值恢复 | 已通过测试 |
| Entity Mask / ArcheType / Query 重建 | 已通过测试 |
| Singleton 映射恢复 | 已通过测试 |
| Restore 成功后清空 WorldEventBuffer | 已通过测试 |
| invalid snapshot restore 不污染当前 World | 已通过测试 |
| 对外接口 `IEcsWorldSnapshotProvider` | 已完成 |
| RollBackSystem 具体回滚流程 | 不属于 ECS 交付范围 |

---

## 3. 命名空间与文件位置

### 3.1 对外接口

```text
Assets/_Scripts/FrameWork/Contracts/ECSInterface/IEcsWorldSnapshotProvider.cs
```

```csharp
namespace Contracts
```

### 3.2 Snapshot DTO

Snapshot DTO 仍位于 ECS 框架命名空间：

```csharp
namespace ECSFrameWork
```

主要类型包括：

```csharp
EcsWorldSnapshot
EcsEntityManagerSnapshot
EcsEntitySlotSnapshot
EcsComponentStoreSnapshot
EcsComponentSnapshot
EcsSingletonSnapshot
EcsWorldSnapshotCaptureResult
EcsWorldSnapshotRestoreResult
```

因此调用方通常需要：

```csharp
using Contracts;
using ECSFrameWork;
```

---

## 4. 推荐对接接口

RollBackSystem 或外部调度器推荐依赖 `IEcsWorldSnapshotProvider`，而不是直接依赖完整 `World` 类型。

```csharp
public interface IEcsWorldSnapshotProvider
{
    bool TryCaptureSnapshot(int frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result);

    bool TryRestoreSnapshot(EcsWorldSnapshot snapshot, out EcsWorldSnapshotRestoreResult result);
}
```

`World` 已实现该接口：

```csharp
public class World : IEcsWorldSnapshotProvider
{
    // 复用 World 已有的 TryCaptureSnapshot / TryRestoreSnapshot 实现。
}
```

### 为什么推荐使用接口

`World` 是 ECS 的完整入口，包含实体、组件、系统、事件、结构变更等大量能力。  
RollBackSystem 通常只需要：

```text
捕获 ECS World Snapshot
恢复 ECS World Snapshot
```

通过 `IEcsWorldSnapshotProvider` 对接，可以把依赖面限制在 Snapshot 能力上，避免外部系统误用 ECS 内部运行时能力。

---

## 5. World 直接调用 API

如果调用方已经持有 `World`，也可以直接调用：

```csharp
public EcsWorldSnapshot CaptureSnapshot(int frameNumber);
public bool TryCaptureSnapshot(int frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result);

public void RestoreSnapshot(EcsWorldSnapshot snapshot);
public bool TryRestoreSnapshot(EcsWorldSnapshot snapshot, out EcsWorldSnapshotRestoreResult result);
```

建议 RollBackSystem 优先使用 `Try` 版本。  
`Try` 版本会通过 result 返回失败原因，更适合回滚流程中的可预期失败处理。

---

## 6. 捕获快照

### 6.1 方法签名

```csharp
bool TryCaptureSnapshot(int frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result);
```

### 6.2 调用条件

Capture 只能在稳定边界调用：

```text
World.CurrentState == WorldStates.Idle
World.PendingCommandCount == 0
World.PendingSystemCommandCount == 0
```

如果不满足条件，方法会返回 `false`，并通过 `result.ErrorMessage` 给出失败原因。

### 6.3 推荐帧语义

建议统一约定：

```text
frame N 的 snapshot 表示 frame N 完整结束后的 ECS World 状态。
```

也就是说，应在以下流程完成后再捕获：

```text
World.Tick(frame N)
结构变更回放完成
System 变更回放完成
AfterTick 命令应用完成
World 回到 Idle
```

不要在 `IFixedStepSystem.Tick()` 内部或结构变更尚未回放时捕获快照。

### 6.4 示例

```csharp
using Contracts;
using ECSFrameWork;

IEcsWorldSnapshotProvider snapshotProvider = world;

if (!snapshotProvider.TryCaptureSnapshot(frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result))
{
    // RollBackSystem 自行记录错误并决定是否中断流程。
    string error = result.ErrorMessage;
    return;
}

// RollBackSystem 自行保存 snapshot。
```

---

## 7. 恢复快照

### 7.1 方法签名

```csharp
bool TryRestoreSnapshot(EcsWorldSnapshot snapshot, out EcsWorldSnapshotRestoreResult result);
```

### 7.2 调用条件

Restore 只能在稳定边界调用：

```text
World.CurrentState == WorldStates.Idle
World.PendingCommandCount == 0
World.PendingSystemCommandCount == 0
```

如果不满足条件，方法会返回 `false`，并通过 `result.ErrorMessage` 给出失败原因。

### 7.3 Restore 成功后的 ECS 保证

Restore 成功后，ECS 保证恢复以下逻辑状态：

```text
Entity ID / Version / Alive
EntityManager dataCount
EntityManager free ID reuse order
ComponentTypeRegistry 注册顺序
ComponentStore dense 顺序与组件值
Entity Mask
ArcheType / Query 可用状态
Singleton 映射
```

Restore 成功后，`WorldEventBuffer` 会被清空。

### 7.4 Restore 失败处理

如果 `TryRestoreSnapshot` 返回 `false`：

```text
不要继续执行输入重放
不要假设 World 已恢复到目标状态
应记录 result.ErrorMessage
由 RollBackSystem / 上层决定降级策略
```

### 7.5 示例

```csharp
using Contracts;
using ECSFrameWork;

IEcsWorldSnapshotProvider snapshotProvider = world;

if (!snapshotProvider.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result))
{
    string error = result.ErrorMessage;
    return;
}

// Restore 成功后，RollBackSystem 可自行决定是否进行输入历史重放。
```

---

## 8. Snapshot 包含的内容

`EcsWorldSnapshot` 包含 ECS 逻辑世界的核心状态：

| 内容 | 说明 |
|---|---|
| `FrameNumber` | 快照对应逻辑帧号 |
| Registered component type order | 组件类型注册顺序，用于保持 `ComponentMask256` bit 语义稳定 |
| EntityManager snapshot | Entity slot、Version、Alive、dataCount、free ID 顺序 |
| ComponentStore snapshots | 各组件 Store 的 dense 顺序、Entity、组件值 |
| Singleton snapshots | Singleton component type 到 Entity 的显式映射 |

注意：Snapshot **不直接保存 QueryCache 或 ArcheTypeGroup 的运行时缓存**。  
Restore 时会根据 Entity 和 ComponentStore 重新构建 Entity Mask、ArcheType 分组与 Query 可用状态。

---

## 9. Snapshot 不包含的内容

以下内容不属于 ECS Snapshot：

| 内容 | 归属 |
|---|---|
| Snapshot ring buffer | RollBackSystem / 上层 |
| 输入历史 | RollBackSystem / 输入同步层 |
| 回滚触发条件 | RollBackSystem / 上层 |
| Restore 后输入重放 | RollBackSystem / 上层 |
| 网络校验 / checksum | RollBackSystem / 网络层 |
| System 私有字段 | 不进入 Snapshot；需要恢复的状态应放入 Component |
| WorldEventBuffer 历史事件 | 不进入 Snapshot；Restore 成功后清空 |
| Unity View / GameObject / Transform | View 层自行同步 |
| Prefab / Scene / ScriptableObject | 不进入 ECS Snapshot |
| 非纯值组件引用对象深拷贝 | 第一版不支持 |

---

## 10. 组件设计约束

参与 Snapshot / Restore 的 `IComponentData` 应优先保持纯值数据。

不建议在可回滚组件中保存：

```text
GameObject
Transform
UnityEngine.Object
List<T>
Dictionary<TKey, TValue>
其他可变引用对象
```

当前 Snapshot 的组件值以 boxed component value 保存。  
对于纯 struct component，这是值副本；如果 struct 内部持有引用类型字段，Snapshot 不负责深拷贝引用对象图。

推荐规则：

```text
需要随回滚恢复的逻辑状态 -> 放入纯值 Component 或 Singleton Component
表现层引用、临时缓存、Unity 对象引用 -> 不进入 Component Snapshot
```

---

## 11. WorldEventBuffer 策略

`WorldEventBuffer` 是瞬时逻辑输出，不属于 ECS 持久状态。

Restore 成功后：

```text
WorldEventBuffer 会被清空
历史事件不会恢复
后续事件应由后续模拟重新产生
```

如果 RollBackSystem 或 View 层需要处理表现事件，应在它们自身的调度策略中处理。  
ECS Snapshot 不负责事件重放或表现层事件过滤。

---

## 12. System 私有状态策略

ECS Snapshot 不保存 System 私有字段。

如果某个 System 中存在需要参与回滚的逻辑状态，应迁移到：

```text
普通 Component
Singleton Component
```

System 私有字段只应保存：

```text
配置引用
缓存
临时计算结果
可重建状态
```

---

## 13. ECS 与 RollBackSystem 的职责边界

### 13.1 ECS 负责

```text
IEcsWorldSnapshotProvider
World.TryCaptureSnapshot
World.TryRestoreSnapshot
World.CaptureSnapshot
World.RestoreSnapshot
EcsWorldSnapshot DTO
Capture / Restore result
恢复 Entity / Component / ArcheType / Query / Singleton
成功 Restore 后清空 WorldEventBuffer
拒绝非稳定边界 Capture / Restore
```

### 13.2 RollBackSystem 或上层负责

```text
每帧何时保存 Snapshot
保存多少帧 Snapshot
Snapshot ring buffer
输入历史
迟到输入 / 权威输入判断
回滚触发条件
选择 rollback frame
Restore 后输入重放
重放后的 Snapshot 刷新策略
View 重绑定 / 重同步
失败时的降级策略
```

---

## 14. 最小接入建议

RollBackSystem 负责人接入前建议确认：

```text
是否能拿到 IEcsWorldSnapshotProvider 或 World 实例
每帧捕获时机是否位于 World Idle 后
Snapshot 保存窗口由谁维护
输入历史是否完整
Restore 后是否需要输入重放
View 是否需要重同步
错误日志是否记录 result.ErrorMessage
```

ECS 侧不强制规定 ring buffer、输入历史或回滚触发方案。

---

## 15. 已通过的关键测试范围

当前 ECS Snapshot 已通过测试，覆盖范围包括：

```text
Entity ID / Version 恢复
Entity Alive 状态恢复
Entity 删除和恢复
Component 值恢复
Component 删除和恢复
future Entity ID reuse order
ComponentStore dense order
多个 ComponentType
ComponentTypeRegistry order
Entity mask
Query / ArcheType rebuild
Singleton mapping
invalid snapshot restore 不污染 World
Capture / Restore 非稳定边界拒绝
WorldEventBuffer restore 后清空
Store 不在 snapshot 中时被移除
```

---

## 16. 对接注意事项

1. 不要在 `IFixedStepSystem.Tick()` 内直接调用 Restore，因为此时 World 通常不是 `Idle`。
2. 不要修改 `EcsWorldSnapshot` 内容，应将其视为只读数据。
3. 不要假设 Snapshot 包含 Unity 表现对象。
4. 不要假设 Restore 会恢复 System 私有字段。
5. 不要在 pending command 未处理完时调用 Capture / Restore。
6. Restore 失败后不要继续执行输入重放。
7. 如果需要 View 同步，应由 View 层基于恢复后的 ECS 当前状态刷新表现。

---

## 17. 一句话边界总结

```text
ECS 提供稳定的 World Snapshot Capture / Restore；
RollBackSystem 负责历史管理、回滚决策、输入重放和表现层重同步。
```
