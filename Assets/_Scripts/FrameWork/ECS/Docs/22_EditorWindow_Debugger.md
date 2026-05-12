# ECS World Debugger EditorWindow

## 作用

`ECSWorldDebuggerWindow` 是 ECSFrameWork 的独立编辑器调试窗口，用于在 Unity Play Mode 中查看当前 `World` 的运行状态。它与 Runtime Inspector 使用同一套 Debug API，但展示范围更完整，适合常驻在编辑器中观察 Entity、System、ArcheType、ComponentStore、Singleton 和 WorldEvent。

窗口入口：

```text
Window / ECSFrameWork / World Debugger
```

## 调试源

EditorWindow 不直接创建或持有 `World`，而是扫描当前场景中实现了 `IECSRuntimeDebugSource` 的 `MonoBehaviour`。

当前支持的调试源包括：

```text
TimeSimulator
ECSRuntimeDebugTarget
其他自定义实现 IECSRuntimeDebugSource 的 MonoBehaviour
```

如果使用自定义 Bootstrap，可以挂载 `ECSRuntimeDebugTarget`，并在运行时绑定：

```csharp
[SerializeField] private ECSRuntimeDebugTarget debugTarget;

private World _world;
private SimulateRunner _runner;

private void Awake()
{
    _world = new World();
    _runner = new SimulateRunner(_world, 0.02f, 4);
    debugTarget.Bind(_world, _runner);
}
```

## 页面说明

### Overview

显示 World 总览状态：

```text
World State
Created Entity Count
Alive Entity Count
Entity Capacity
Component Type Count
Component Store Count
ArcheType Count
Query Cache Count
System Count
Singleton Count
WorldEvent Count
Pending Structural Changes
Pending System Changes
Runner Frame / Tick 状态
```

### Entities

显示当前存活 Entity 列表。选中 Entity 后，可以查看：

```text
ID
Version
Alive
Component Count
ComponentMask256
Component Type 列表
```

第一版只显示组件类型，不反射显示组件字段值，避免 EditorWindow 本身产生过多 GC 或破坏 ECS 封装。

### Systems

显示当前注册的 System Profile：

```text
System Name
SystemTickSequence
Enabled
Last Tick ms
Average Tick ms
Max Tick ms
Tick Count
```

当前版本只读，不在窗口中启用或禁用 System。

### ArcheTypes

显示当前 ArcheType 分组。选中分组后，可以查看：

```text
Mask
Entity Count
Component Count
Component Type 列表
该 ArcheType 下的 Entity 列表
```

该页面用于检查 Entity 是否进入正确分组，以及 Query 相关性能问题。

### Component Stores

显示每个 ComponentStore 的容量和数量：

```text
Component Type
Register ID
Count
Capacity
Sparse Capacity
```

该页面适合观察 Store 是否异常扩容，以及某类组件数量是否符合预期。

### Singletons

显示当前 SingletonComponent 映射：

```text
Component Type
内部承载 Entity
Alive
```

### World Events

显示当前 WorldEventBuffer 中缓存的事件：

```text
Event Type
Count
Oldest Frame
Newest Frame
```

用于检查事件是否被正常写入和清理。

## 刷新机制

窗口顶部提供：

```text
Refresh Targets
Auto Refresh
Refresh Interval
Refresh Now
Dump Snapshot
```

默认 `Auto Refresh` 开启，刷新间隔为 `0.25` 秒。窗口内部复用缓存 `List`，不会在每次 GUI 绘制时频繁创建临时集合。

## 设计约束

1. EditorWindow 只通过 `World.GetDebugSnapshot()` 和 `FillXXX()` Debug API 读取数据。
2. EditorWindow 不访问 `EntityManager`、`ComponentManager`、`ArcheTypeManager`、`SystemManager` 等内部字段。
3. EditorWindow 第一版只读，不修改 Entity / Component / System / Event。
4. 所有修改操作未来都必须走 `World` 对外 API，不能绕过生命周期和 Buffer 规则。
5. EditorWindow 脚本位于 `FrameWork/ECS/Editor/`，不会进入正式构建。

## 后续扩展建议

后续可以在不破坏当前结构的基础上继续扩展：

```text
1. 组件字段只读展开显示
2. Entity / ArcheType / ComponentStore 排序
3. System Profile 简单耗时曲线
4. WorldSnapshot 导出按钮
5. Query Cache 调试页
6. 在 System Enable / Disable API 完成后增加只调用正式 API 的控制按钮
```
