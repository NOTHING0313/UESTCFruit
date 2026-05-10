# 07. 后续扩展规则与限制

## 1. 组件设计规则

- 组件实现 `IComponentData`。
- 优先使用 `struct`。
- 只保存数据，不写复杂逻辑。
- 核心逻辑组件不要持有 `GameObject`、`Transform`、`MonoBehaviour`。

## 2. System 设计规则

- System 通过 Query 获取 Entity。
- 逻辑结果依赖顺序时使用 `ExecuteSorted()`。
- System 内部状态尽量少；需要同步或回滚的状态放入组件。
- 正式模拟开始后，System 列表尽量固定。

## 3. World 修改规则

| 场景 | 推荐方式 |
|---|---|
| 初始化阶段创建实体 / 添加组件 | 直接使用 World API |
| Tick 内由逻辑推导出的结构变化 | 直接使用 World API，由 StructuralChangeBuffer 延迟播放 |
| Tick 外部 UI / 网络 / 剧情请求 | 使用 SimulationFrameCommandScheduler / Buffer |
| 输入 | 使用 PlayerInputSnapshot + InputSnapshotBuffer |

## 4. Query 规则

- 普通调试或无顺序依赖可用 `Execute()`。
- 影响逻辑结果时使用 `ExecuteSorted()`。
- 避免在 foreach Query 时直接修改会改变 ArcheType 的结构；直接调用 World API 即可，由 Buffer 保护。

## 5. Unity Adapter 规则

- Unity 输入只负责采样，不直接决定玩法逻辑。
- Unity View 只负责表现同步，不反向写入逻辑状态。
- GameObject 的创建、销毁通过 View 系统统一处理。

## 6. 当前限制

- 还没有实现 WorldSnapshot / RestoreSnapshot。
- 还没有实现 StateHash。
- 还没有替换成确定性定点数。
- 浮点数逻辑在严格跨平台帧同步下仍需进一步收束。
- Rollback 主流程尚未接入。

这些限制不影响当前 World Core 作为单机固定帧 ECS 底座使用，但在继续推进网络回滚时需要逐步补齐。
