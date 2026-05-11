# ECS World Core 接入说明

当前目录记录这套轻量 ECS 的核心设计、生命周期、对外 API 和接入边界。它面向项目中负责 **World / Entity / Component / System / 固定逻辑帧调度** 的模块交付。

## 文档索引

| 文档 | 内容 |
|---|---|
| `01_Architecture.md` | 整体架构、模块分层、数据流 |
| `02_World_Lifecycle.md` | World Tick 生命周期、StructuralChangeBuffer、SystemChangeBuffer |
| `03_Core_API.md` | World / Entity / Component / Query / System 常用 API |
| `04_Input_FrameCommand.md` | 输入快照与外部帧指令的按帧记录、消费方式 |
| `05_Buff_View_Integration.md` | Buff 与 View 层接入边界 |
| `06_Test_Guide.md` | 当前测试脚本说明与验收点 |
| `07_Extension_Rules.md` | 后续扩展规则与限制 |
| `08_Query_System_View_Optimization.md` | Query 缓存、System 执行和 View Provider 优化 |
| `09_Performance_Benchmark.md` | 性能 Benchmark 使用说明 |
| `10_Component_ForEach_Optimization.md` | 高频组件 ForEach 遍历优化说明 |
| `11_WorldEventBuffer.md` | WorldEventBuffer 事件输出通道、使用方式与清理时机 |
| `12_SingletonComponent_And_Namespace.md` | Singleton Component 与命名空间整理 |
| `13_Entity_Rename.md` | Entity 命名规范与对外句柄说明 |
| `14_Public_API_Guide.md` | 当前 ECSFrameWork 对外接口完整使用文档 |
| `16_EntityBuilder.md` | EntityBuilder 链式实体创建入口、使用规则与后续扩展关系 |
| `17_EntityPrefab_EntityFactory.md` | EntityPrefab 实体模板、EntityFactory 注册创建入口与运行时覆盖规则 |
| `18_Prefab_SO_GameplayFactory.md` | Unity PrefabSO、ComponentPresetSO、DefinitionSO 与 GameplayEntityFactory 创建链路 |
| `19_Public_API_Usage_Guide_Latest.md` | 当前 ECSFrameWork 对外接口和调用方式完整说明 |

## 核心原则

1. `World` 是 ECS Core 的统一入口，外部业务不要直接操作 `EntityManager`、`ComponentManager`、`ArcheTypeManager`。
2. `Entity` 是实体句柄，由 `ID + Version` 组成，用于避免旧句柄误操作复用后的 Entity。
3. `IComponentData` 组件只保存数据，逻辑放在 `IFixedStepSystem` 中。
4. `World.Tick` 内产生的结构变化通过 `StructuralChangeBuffer` 延迟到逻辑帧末执行。
5. Tick 外部的输入、UI、网络、剧情等修改请求，应该通过 `InputSnapshotBuffer` 或 `SimulationFrameCommandBuffer` 按逻辑帧记录。
6. Unity 表现层只跟随 ECS 状态同步，不能反向决定逻辑结果。
7. 外部代码只应依赖 `ECSFrameWork` 命名空间下的公开入口，不要依赖内部 Manager / Store / Buffer 实现细节。
