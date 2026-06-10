# RollBackSystem

客户端预测 + 服务端校验的帧同步回滚框架。

## 架构

```
RollbackBootstrap        ← 场景入口，挂载即用
RollbackCoordinator      ← 核心调度：Step → SaveSnapshot → ReceiveAuthoritativeInput
WorldRollbackAdapter     ← ECS World → IRollbackableWorld 适配
WorldChecksumCalculator  ← 确定性状态 Hash
```

## 文件（24 个 .cs）

```
RollBackSystem/
├── RollbackBootstrap.cs           入口
├── RollbackVisualTest.cs          可视化测试
├── WorldChecksumCalculator.cs     状态校验
├── IDeterministicHash.cs
├── Interfaces/                    IRollbackableWorld, IRollbackSimulation, IInputBuffer, ...
└── Rollback/
    ├── Core/                      RollbackCoordinator, WorldRollbackAdapter
    ├── Input/                     InputBuffer, AuthoritativeInputBuffer, PlayerSnapshotInputApplier, ...
    ├── Snapshot/                  ISnapshot, SnapshotRingBuffer
    ├── Checksum/                  ChecksumBuffer, FrameChecksum, ...
    └── FrameCommand/              FrameCommandSourceAdapter
```

## 功能

| 功能 | 说明 |
|------|------|
| 预测输入管线 | 本地输入 → InputBuffer → InputApplier.Apply → 写入 ECS |
| 快照保存 | 每 N 帧在 AfterTick 保存完整 ECS World 快照（RingBuffer） |
| 输入比较 | `PlayerInputSnapshotComparer` 浮点精度比较全部字段 |
| 回滚触发 | 权威输入与预测不同 → 恢复最近快照 → 逐帧重模拟 |
| 重模拟 | 每帧 Simulate(写输入+BeforeTick命令) → Tick(跑System+AfterTick命令) |
| Checksum | 全部 15 种组件逐字段确定性 Hash，未知组件报 Warning |

## 场景接入

1. 选中 `BuffConfigLoader` GameObject
2. Add Component → **RollbackBootstrap**（勾选 `_enable`，其他默认）
3. Add Component → **RollbackVisualTest**（可选）
4. Play

系统自动完成：发现 `UnityInputAdapter` → 发现玩家 Entity → 禁用旧输入路径 → Hook `BeforeTick`/`AfterTick`。

## 测试

挂 `RollbackVisualTest` 后：

| 操作 | 效果 |
|------|------|
| WASD 移动 | HUD 显示帧号、位置、Checksum |
| 按 Space | 注入翻转方向的权威输入，触发回滚 |
| 观察 Console | 回滚前后位置和 Checksum 对比 |

## 核心 API

**RollbackCoordinator**
| 方法 | 说明 |
|------|------|
| `Step(input)` | 推进一帧（写输入和命令，不 Tick） |
| `SaveSnapshot()` | 保存完整 ECS 快照 + Checksum |
| `ReceiveAuthoritativeInput(frame, input)` | 比较 → 回滚 → 重模拟 |
| `CalculateChecksum()` / `VerifyChecksum(frame)` | 状态校验 |
| `CurrentFrame` / `TickLength` | 帧号 / 帧时长 |

**RollbackBootstrap**
| 成员 | 说明 |
|------|------|
| `Coordinator` / `InputApplier` / `World` | 获取内部实例 |
| `ReceiveRemoteInput(frame, input)` | 触发回滚快捷入口 |

## Checksum 覆盖

全部生产组件：`Position`, `Velocity`, `MoveSpeed`, `View`, `Health`, `Stat`, `DamageRequest`, `PlayerInputSnapshot`, `BuffRuntime`, `CompressedParallelBuffRuntime`, `AddBuffRequest`, `RemoveBuffRequest` 等 — 所有值类型字段参与 Hash。空标记组件按其类型名参与。

## 设计约束

- `Simulate()` 只写输入和 BeforeTick 命令；`Tick()` 执行 World.Tick + AfterTick 命令 — 与 `TimeSimulator` 时序一致
- 快照仅在 World Idle 时保存（`AfterTick` 中）
- 回滚到 frame-1 的最近快照，再重模拟追回当前帧
