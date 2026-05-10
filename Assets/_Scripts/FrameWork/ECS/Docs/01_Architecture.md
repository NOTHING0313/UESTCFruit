# 01. ECS World Core 架构

## 1. 模块分层

```text
Unity Adapter 层
    UnityInputAdapter
    ViewManager / ViewSpawnSystem / ViewSyncSystem / ViewDestroySystem

时间推进层
    TimeSimulator
    SimulateRunner
    SimulationContext

帧输入 / 帧指令层
    InputSnapshotBuffer
    WorldInputApplier
    SimulationFrameCommandBuffer
    SimulationFrameCommandScheduler
    SimulationFrameCommandApplier

ECS Core 层
    World
    EntityManager
    ComponentManager
    ComponentStore<T>
    ArcheTypeManager
    SystemManager
    EntityQueryBuilder

缓冲层
    StructuralChangeBuffer
    SystemChangeBuffer
```

## 2. World 的定位

`World` 是 ECS Core 的对外门面，负责统一管理：

- Entity 创建、销毁、存活校验
- Component 增删改查
- ArcheType 分组更新
- Query 创建与执行
- System 添加、移除、Tick 调度
- Tick 中结构变化缓冲播放

外部模块应优先通过 `World` 访问 ECS。底层 Manager 主要作为内部实现细节存在。

## 3. Entity / Component 数据关系

```text
EntityInfo
    ID
    Version

EntityData[ID]
    alive
    version
    ComponentMask256

ComponentStore<T>
    sparse[entityID] -> denseIndex
    denseEntity[denseIndex]
    denseComponent[denseIndex]
```

`EntityInfo` 本身不直接持有组件，只是定位实体槽位的句柄。组件数据存放在对应的 `ComponentStore<T>` 中。

## 4. ArcheType 分组

每种组件类型会被 `ComponentTypeRegistry` 分配一个 ID，并映射到 `ComponentMask256` 的某个 bit。

当 Entity 持有的组件发生变化时：

```text
oldMask = Entity 当前组件组合
新增 / 移除 Component
newMask = Entity 新组件组合
ArcheTypeManager.ChangeGroup(entity, oldMask, newMask)
```

`ArcheTypeManager` 使用：

```text
Dictionary<ComponentMask256, ArcheTypeGroup>
```

对 Entity 进行分组。`ArcheTypeGroup` 内部通过 `List<EntityInfo>` 保存实体，并通过 `Dictionary<EntityInfo, int>` 记录实体下标，使移除 Entity 时可以使用尾元素回填的方式接近 O(1) 完成。Query 会通过 include / exclude mask 找到匹配分组。

## 5. System 数据流

典型移动流程：

```text
PlayerInputComponent
    ↓ InputMoveSystem
VelocityComponent
    ↓ MovementSystem
PositionComponent
    ↓ ViewSyncSystem
Unity Transform
```

其中 `PositionComponent` 是逻辑真值，`Transform.position` 只是表现同步结果。
