# RollBackSystem 使用文档

## 概述

RollBackSystem 是一个客户端预测 + 服务端校验的回滚框架，用于帧同步游戏的确定性模拟。

**核心流程：** 本地预测输入 → 缓存快照 → 收到权威输入后回滚到历史帧 → 用正确输入重模拟。

## 架构分层

```
┌─────────────────────────────────────────┐
│  RollbackBootstrap （入口，接 TimeSimulator） │
│  RollbackDemoSetup  （Demo 层，创建玩家）    │
├─────────────────────────────────────────┤
│  RollbackCoordinator （协调器，核心调度）     │
│  WorldRollbackAdapter （ECS World 适配器）   │
├─────────────────────────────────────────┤
│  InputBuffer / SnapshotRingBuffer / Checksum │  ← 数据 Buffer
└─────────────────────────────────────────┘
```

### 驱动流（正常帧）

```
TimeSimulator.Update
  → Runner.BeforeTick
      → RollbackBootstrap.OnBeforeTick
          → CollectInput (从 UnityInputAdapter 采集)
          → coordinator.Step(snapshot)
              → inputBuffer.Save(frame, input)
              → adapter.Simulate(input, context)
                  → frameCommandSource.ApplyCommandsToWorld   // 外部帧命令
                  → inputApplier.Apply(world, input)          // 写入 PlayerInputSnapshotComponent
      → Runner.TickFrame → World.Tick → InputMoveSystem / MovementSystem
```

### 回滚流

```
ReceiveRemoteInput(frame, authoritativeInput)
  → coordinator.ReceiveAuthoritativeInput(frame, input)
      → 比较预测输入 vs 权威输入 → 不同
      → RollbackTo(frame - 1) → world.Restore(snapshot)  // ECS 完整恢复
      → inputBuffer.Save(frame, authoritativeInput)
      → ResimulateTo(targetFrame)
          → 逐帧: adapter.Simulate(重放输入) + Tick
```

---

## 场景接入

### 方式 1：完整接入（推荐）

1. 场景中已有 `TimeSimulator` + `Bootstrap(SimulationInitializer)` 运行
2. 在 `Bootstrap` GameObject 上额外添加两个组件：
   - `RollbackBootstrap`（勾选 Enable）
   - `RollbackDemoSetup`（勾选 Auto Setup，填好 Move Speed 等参数）
3. 可选：添加 `RollbackInputTest`（Space 触发回滚测试）

### 方式 2：自定义接入

只挂载 `RollbackBootstrap`，业务层自行注入 System 和创建 Entity：

```csharp
var world = TimeSimulator.Instance.DebugWorld;
world.AddSystem(new InputMoveSystem());
world.AddSystem(new MovementSystem());

var player = world.CreateEntity();
world.SetComponent(player, new PositionComponent(0, 0, 0));
world.SetComponent(player, new VelocityComponent(0, 0, 0));
world.SetComponent(player, new MoveSpeedComponent(5f));
world.SetComponent(player, new PlayerInputSnapshotComponent(0f, 0f));

var rb = FindObjectOfType<RollbackBootstrap>();
rb.InputApplier.RegisterPlayer(1, player);
```

---

## 核心 API

### RollbackCoordinator

| 方法 | 说明 |
|------|------|
| `Step(TInput input)` | 推进一帧，只写输入和帧命令，不 Tick |
| `SaveSnapshot()` | 保存当前帧的 ECS 完整快照 |
| `ReceiveAuthoritativeInput(int frame, TInput input)` | 触发回滚+重模拟 |
| `RollbackTo(int frame)` | 回滚到 frame 之前的状态 |
| `ResimulateTo(int targetFrame, Action onEachFrame)` | 重模拟到目标帧 |
| `CurrentFrame` | 当前逻辑帧号 |

### RollbackBootstrap

| 成员 | 说明 |
|------|------|
| `Coordinator` | 获取 Coordinator 实例 |
| `InputApplier` | 获取 InputApplier（用于注册玩家） |
| `ReceiveRemoteInput(int frame, PlayerInputSnapshot input)` | 触发回滚的便捷入口 |

### PlayerSnapshotInputApplier

```csharp
void RegisterPlayer(int playerID, Entity entity);   // 绑定玩家 Entity
void Apply(World world, PlayerInputSnapshot input);  // 写入 PlayerInputSnapshotComponent
```

---

## 测试工具

| 脚本 | 用途 |
|------|------|
| `RollbackTest` | 纯逻辑测试，挂载即跑，FakeWorld 验证 Step→SaveSnapshot→ReceiveAuthoritativeInput |
| `RollbackInputTest` | 交互测试，Space 触发回滚，A/D 移动，验证回滚后位置修正 |

---

## 设计约束

1. **Simulate() 只写输入，不 Tick** — `WorldRollbackAdapter.Simulate()` 只执行帧命令回放和输入写入，`World.Tick()` 由 `SimulateRunner.TickFrame()` 统一调用
2. **回滚到 frame-1** — `ReceiveAuthoritativeInput` 回滚到错误帧之前的快照，再逐帧重放
3. **World 必须 Idle 才能 CaptureSnapshot** — snapshot 在 Tick 完成后的稳定边界保存
