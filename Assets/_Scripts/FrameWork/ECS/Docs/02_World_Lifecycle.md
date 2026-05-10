# 02. World 生命周期与缓冲机制

## 1. WorldStates

| 状态 | 含义 |
|---|---|
| `Initialization` | 初始化或空闲阶段，允许立即修改 Entity / Component / System |
| `Ticking` | System 正在执行，结构变化需要进入 Buffer |
| `AfterTicking` | System 执行结束，正在播放 `StructuralChangeBuffer` |
| `SystemOperating` | 正在播放 `SystemChangeBuffer` |
| `Disposing` | World 正在释放，外部修改请求会被忽略 |

## 2. 单帧 Tick 流程

```text
World.Tick(context)
    ↓
SetWorldState(Ticking)
    ↓
SystemManager.Tick(context)
    ↓
SetWorldState(AfterTicking)
    ↓
StructuralChangeBuffer.Playback(world)
    ↓
SetWorldState(SystemOperating)
    ↓
SystemManager.PlaybackSystemChanges()
    ↓
SetWorldState(Initialization)
```

## 3. StructuralChangeBuffer 的职责

`StructuralChangeBuffer` 负责缓存 **World.Tick 内部由 System 产生的结构变化**：

- 新增组件
- 移除组件
- 销毁 Entity

它存在的原因是：System 执行期间通常正在遍历 Query 结果，如果此时直接修改 ArcheType 分组或组件 Store，可能破坏当前遍历。

因此，在 `Ticking` 阶段：

```csharp
World.SetComponent(entity, component);    // 新增组件时进入 Buffer
World.RemoveComponent<T>(entity);         // 进入 Buffer
World.DestroyEntity(entity);              // 进入 Buffer
```

但如果 Entity 已经持有该组件，`SetComponent` 只是覆盖已有数据，不改变 ArcheType，可以立即执行。

## 4. SystemChangeBuffer 的职责

`SystemChangeBuffer` 负责缓存 System 列表变化：

- AddSystem
- RemoveSystem
- ClearSystem

System 列表修改会影响 Tick 遍历顺序，所以同样需要统一播放。正式模拟开始后，建议 System 列表尽量固定；如果只是临时禁用某类逻辑，优先使用组件或状态控制，而不是频繁 Add / Remove System。

## 5. FrameCommandBuffer 与 StructuralChangeBuffer 的区别

| 类型 | 用途 | 生命周期 | 是否按帧保存 |
|---|---|---|---|
| `StructuralChangeBuffer` | Tick 内部逻辑产生的结构变化 | 当前 Tick 内收集，当前 Tick 末播放 | 否 |
| `SimulationFrameCommandBuffer` | Tick 外部输入 / UI / 网络 / 剧情指令 | 按 frameNumber 保存，可用于回放 | 是 |

简单说：

- System 自己推导出的结构变化，走 `StructuralChangeBuffer`。
- Tick 外部想影响 World 的行为，走 `SimulationFrameCommandBuffer` 或 `SimulationFrameCommandScheduler`。
