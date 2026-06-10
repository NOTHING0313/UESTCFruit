# RollBackSystem 使用文档

## 概述

RollBackSystem 是一个客户端预测 + 服务端权威校验的回滚框架，用于帧同步游戏的确定性模拟。

**核心流程：** 本地预测输入 → 缓存 ECS 完整快照 → 收到权威输入后回滚到历史帧 → 用正确输入逐帧重模拟所有 System。

---

## 分层架构

```
┌──────────────────────────────────────────────────────┐
│  RollbackBootstrap （入口，接 TimeSimulator/Runner）    │
│  RollbackVisualTest （可视化测试工具）                    │
├──────────────────────────────────────────────────────┤
│  RollbackCoordinator （核心调度器）                      │
│  WorldRollbackAdapter （ECS World → IRollbackableWorld）│
├──────────────────────────────────────────────────────┤
│  InputBuffer / SnapshotRingBuffer / ChecksumBuffer    │  ← 数据层
│  FrameCommandSourceAdapter                             │  ← 帧命令回放
│  PlayerSnapshotInputApplier                            │  ← 输入写入
│  WorldChecksumCalculator                               │  ← 状态校验
└──────────────────────────────────────────────────────┘
```

---

## 文件清单（24 个 .cs）

```
FrameWork/RollBackSystem/
├── RollbackBootstrap.cs                    ← 入口，挂场景即可
├── RollbackVisualTest.cs                   ← 可视化测试工具
├── WorldChecksumCalculator.cs              ← 确定性状态校验
├── IDeterministicHash.cs                   ← 组件自描述 Hash 接口
│
├── Interfaces/
│   ├── IRollbackableWorld.cs               ← 世界必须实现 Simulate + Tick
│   ├── IRollbackSimulation.cs              ← Coordinator 对外接口
│   ├── IInputBuffer.cs                     ← 输入缓存接口
│   ├── ISimulationChecksum.cs              ← 校验接口
│   └── IFrameCommandSource.cs              ← 帧命令回放源接口
│
├── Rollback/
│   ├── Core/
│   │   ├── RollbackCoordinator.cs          ← 核心调度器
│   │   └── WorldRollbackAdapter.cs         ← ECS 适配器
│   ├── Input/
│   │   ├── AuthoritativeInputBuffer.cs     ← 权威输入缓存
│   │   ├── InputBuffer.cs                  ← 预测输入缓存
│   │   ├── PlayerInputSnapshotComparer.cs  ← 输入相等比较
│   │   ├── PlayerSnapshotInputApplier.cs   ← 输入写入 ECS
│   │   └── IInputComparer.cs               ← 输入比较接口
│   ├── FrameCommand/
│   │   └── FrameCommandSourceAdapter.cs    ← 帧命令适配器
│   ├── Snapshot/
│   │   ├── ISnapshot.cs                    ← 快照接口
│   │   ├── ISnapshotable.cs                ← 可快照接口
│   │   └── SnapshotRingBuffer.cs           ← 环形快照缓存
│   ├── Checksum/
│   │   ├── AuthoritativeChecksumBuffer.cs  ← 权威校验缓存
│   │   ├── ChecksumBuffer.cs               ← 本地校验缓存
│   │   ├── ChecksumComparisonResult.cs     ← 校验结果
│   │   └── FrameChecksum.cs                ← 帧校验值
│   └── IWorldInputApplier.cs               ← 输入应用接口
```

---

## 机制详解：回滚如何确认位置和帧号

### 触发

```csharp
_rb.ReceiveRemoteInput(30, new PlayerInputSnapshot(30, 1) { moveX = -1f });
// 告诉系统：帧 30 的真实输入是左移，但本地预测的是右移
```

### 回滚流程

```
帧45: 正常运行，CurrentFrame=45

收到权威输入（帧30, moveX=-1）
  │
  ├─① 比较：预测输入 moveX=+1 ≠ 权威输入 moveX=-1 → 触发回滚
  │
  ├─② RollbackTo(29)：从 SnapshotRingBuffer 找到 ≤29 的最近快照
  │     World.Restore(snapshot) 完整恢复：
  │     · 所有 Entity（ID、Version、Alive 状态精确还原）
  │     · 所有 Component（Position、Velocity、Buff 等全部值字段）
  │     · ComponentStore dense 数组、ArcheType 分配、Singleton
  │     CurrentFrame = 20（快照帧号）
  │
  ├─③ 覆盖帧30的输入为权威输入
  │
  └─④ ResimulateTo(45)：从帧21逐帧重算到帧45
        每帧执行：
        · Simulate(input, context) → InputApplier.Apply → 写入 PlayerInputSnapshotComponent
          → FrameCommandSource 重放该帧的 BeforeTick 命令（Buff、技能等）
        · Tick(context) → InputMoveSystem → MovementSystem → BuffSystemBridge...
          → FrameCommandSource 执行该帧的 AfterTick 命令
        · Capture(nextFrame) → 保存新快照
        · SaveChecksum() → 保存状态 Hash
        CurrentFrame: 20 → 21 → ... → 45

帧45: 重模拟完成，CurrentFrame=45
      Runner.SetFrameCount(45) 对齐帧号
```

### 位置修正原理

```
正常推进（帧1-45 全用预测输入 moveX=+1）：
  帧20: x=10.0
  帧30: x=15.0  ← +1 × 5 × 1/60 = +0.083/帧
  帧45: x=22.5

回滚重算（帧21-29 用 +1，帧30-45 用权威输入 -1）：
  帧20: x=10.0  ← 快照恢复（重新计算起点）
  帧29: x=14.5  ← 累积 10 帧的 +1
  帧30: x=14.0  ← 权威输入 -1，x 开始减少
  帧45: x=6.5   ← 最终修正位置
```

### 帧号对齐

| 阶段 | `Coordinator.CurrentFrame` | `Runner._frameCount` |
|------|:---:|:---:|
| 回滚前 | 45 | 45 |
| RollbackTo 后 | 20 | 45（不变） |
| ResimulateTo 完成 | 45 | 45（不变） |
| SetFrameCount(45) | 45 | 45（对齐） |
| 下次 Update | 46 | 46 |

---

## 正常运行的数据流

```
TimeSimulator.Update（每 Unity 帧）
  │
  ├─ SampleInputAdapters()        ← UnityInputAdapter 采样键盘/鼠标
  │
  └─ Runner.Update(Time.deltaTime)
       │
       ├─ BeforeTick 事件
       │    ├─ RollbackBootstrap.OnBeforeTick
       │    │    ├─ adapter.CollectSnapshot(frame)   ← 采集帧输入
       │    │    └─ coordinator.Step(input)          ← 写入预测输入缓存
       │    │         └─ adapter.Simulate(input, ctx)
       │    │              ├─ FrameCommandSource.BeforeTick 命令
       │    │              └─ InputApplier.Apply → 写入 ECS 组件
       │    │
       │    └─ （SimulationInitializer 的 WriteInputToWorld 已被禁用）
       │
       ├─ World.Tick(context)      ← 所有 System 执行
       │    ├─ InputMoveSystem     ← 读 PlayerInputSnapshotComponent → 写 Velocity
       │    ├─ MovementSystem      ← Velocity × tickLength → 更新 Position
       │    ├─ BuffSystemBridge    ← Buff 系统 Tick
       │    └─ ... 其他 System
       │
       └─ AfterTick 事件
            ├─ RollbackBootstrap.OnAfterTick
            │    └─ coordinator.SaveSnapshot() (每 N 帧)
            │
            └─ TimeSimulator.AfterTick 帧命令
```

---

## 场景接入

### 方式 1：自动接入（推荐）

1. 在 Unity Editor 中打开场景（如 `ZP_Test.unity`）
2. 选中 `BuffConfigLoader`（或任意已挂 `TimeSimulator` 的 GameObject）
3. Add Component → **RollbackBootstrap**
   - `_enable` = true
   - `_snapshotRingCapacity` = 120（缓存最近 120 帧快照）
   - `_snapshotIntervalFrames` = 10（每 10 帧保存一次）
4. Play 即可

系统自动完成：
- 发现 `UnityInputAdapter` 并从中采集输入
- 发现 World 中带 `PlayerTagComponent` 的 Entity 并注册
- 禁用 `SimulationInitializer` 的直接输入写入
- Hook `Runner.BeforeTick` 接管输入管线

### 方式 2：自定义接入

如果场景结构不同，在代码中手动挂载：

```csharp
var rb = gameObject.AddComponent<RollbackBootstrap>();
rb.Coordinator;           // 获取 Coordinator
rb.ReceiveRemoteInput();  // 触发回滚
```

---

## 测试工具

### RollbackVisualTest

挂到场景中任意 GameObject（与 `RollbackBootstrap` 同场景）。

| 功能 | 操作 | 显示 |
|------|------|------|
| 实时监控 | Play 即显示 | 左上 HUD：帧号、位置、Checksum |
| 触发回滚 | 先按 WASD 移动，再按 Space | 注入翻转方向的权威输入 |
| 验证结果 | 观察 Console 和 HUD | 位置变化、Checksum 变化、帧号对齐 |

**预期输出：**

```
[RollbackVisualTest] ═══ Triggering rollback at frame 30 ═══
[RollbackVisualTest] Predicted moveX=1.0, Authoritative moveX=-1.0
[RollbackVisualTest] Pre-rollback: frame=45, checksum=2837465129
[RollbackVisualTest] pre-rollback player pos=(3.50, 0.00, 0.00)
[RollbackVisualTest] Post-rollback: frame=45, checksum=3120498512
[RollbackVisualTest] post-rollback player pos=(2.10, 0.00, 0.00)
[RollbackVisualTest] Checksum changed: True
```

---

## 核心 API

### RollbackCoordinator

| 方法/属性 | 说明 |
|-----------|------|
| `Step(TInput input)` | 推进一帧，写输入和帧命令，不 Tick |
| `SaveSnapshot()` | 保存当前帧的 ECS 完整快照 + Checksum |
| `ReceiveAuthoritativeInput(int frame, TInput input)` | 比较输入 → 触发回滚 + 重模拟 |
| `RollbackTo(int frame)` | 回滚到 frame 之前的状态 |
| `ResimulateTo(int targetFrame)` | 重模拟到目标帧 |
| `CalculateChecksum()` | 计算当前 World 的确定性 Hash |
| `VerifyChecksum(int frame)` | 比较本地 vs 权威 Checksum |
| `CurrentFrame` | 当前逻辑帧号 |
| `TickLength` | 逻辑帧时长（默认 1/60s） |

### RollbackBootstrap

| 成员 | 说明 |
|------|------|
| `Coordinator` | 获取 Coordinator 实例 |
| `InputApplier` | 获取 InputApplier（用于注册玩家） |
| `World` | 获取当前 ECS World |
| `ReceiveRemoteInput(int frame, PlayerInputSnapshot input)` | 触发回滚的快捷入口 |

---

## Checksum 覆盖范围

`WorldChecksumCalculator` 对以下所有组件的全部值类型字段做确定性 Hash：

| 组件 | Hash 字段 |
|------|----------|
| `PositionComponent` | x, y, z |
| `VelocityComponent` | x, y, z |
| `MoveSpeedComponent` | value |
| `ViewComponent` | viewID |
| `PrefabViewRequestComponent` | prefabID |
| `HealthComponent` | current, max |
| `StatComponent` | attack, defense, moveSpeed |
| `DamageRequestComponent` | source.ID/Version, target.ID/Version, amount |
| `PlayerInputSnapshotComponent` | inputFrame, playerID, moveX/Y, mouse 全部字段, 全部按钮 flags |
| `BuffRuntimeComponent` | target/source ID/Version, configId, runtimeHandle, stack, duration/remaining/tickInterval/elapsedFrames, ticks, maxStack, priority, unlimited, isForever, buffType |
| `CompressedParallelBuffRuntimeComponent` | target/source, configId, compressedRuntimeHandle, priority, layerCount, nextLayerId, 所有 active layer 的 layerId/expireFrame/elapsedFrames/ticks/runtimeHandle |
| `AddBuffRequestComponent` | 所有 AddBuffCommand 字段 |
| `RemoveBuffRequestComponent` | 所有 RemoveBuffCommand 字段 |
| 空组件 | `ViewDestroyRequest`、`EntityDestroyRequest`、`PlayerTag`、`DeadTag`（无字段，仅类型名参与 Hash） |

遇到未识别的组件类型时，输出 `[Checksum] Unhashed component` 警告。

---

## 设计约束

1. **Simulate() 只写输入和 BeforeTick 命令，不 Tick** — 与 `TimeSimulator.OnBeforeTick` 对齐
2. **Tick() 执行 World.Tick 后执行 AfterTick 命令** — 与 `TimeSimulator.OnAfterTick` 对齐
3. **回滚到 frame-1** — `ReceiveAuthoritativeInput` 回滚到错误帧之前的快照
4. **World 必须 Idle 才能 CaptureSnapshot** — 快照在 `AfterTick` 中保存
5. **帧命令按 BeforeTick/AfterTick 分阶段执行** — 正常推进和回滚重放时序一致
6. **Checksum 必须稳定确定** — 相同状态 → 相同 Hash，用于检测客户端与服务端状态漂移
