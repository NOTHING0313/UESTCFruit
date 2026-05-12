# ECS Runtime Inspector

`ECS Runtime Inspector` 是 ECSFrameWork 的只读运行时调试面板，用于在 Unity Play Mode 中查看当前 `World` 的运行状态。

## 功能

当前 Inspector 会显示：

- World 总览信息
- SimulateRunner 帧推进状态
- System Profile 简表
- ArcheType 分组信息
- Entity 预览列表
- ComponentStore 简表
- SingletonComponent 映射
- WorldEvent 缓冲区信息

该工具只调用 `World` 的 Debug API，不直接访问 `EntityManager`、`ComponentManager`、`SystemManager` 等内部字段。

## 使用方式一：TimeSimulator

`TimeSimulator` 已实现 `IECSRuntimeDebugSource`。

在 Play Mode 中选中挂载 `TimeSimulator` 的对象，即可在 Inspector 下方看到 `ECS Runtime Inspector` 区域。

前提是启动代码已经调用：

```csharp
TimeSimulator.instance.InitSimulator(runner);
```

这样 Inspector 才能通过 `runner.World` 获取当前运行中的 `World`。

## 使用方式二：ECSRuntimeDebugTarget

如果项目不是由 `TimeSimulator` 持有 `World`，可以把 `ECSRuntimeDebugTarget` 挂到任意场景对象上，然后在启动代码中绑定：

```csharp
using ECSFrameWork;
using UnityEngine;

public sealed class MyECSBootstrap : MonoBehaviour
{
    [SerializeField] private ECSRuntimeDebugTarget debugTarget;

    private World _world;
    private SimulateRunner _runner;

    private void Awake()
    {
        _world = new World();
        _runner = new SimulateRunner(_world, 0.02f, 4);
        debugTarget.Bind(_world, _runner);
    }
}
```

然后在 Play Mode 中选中 `ECSRuntimeDebugTarget` 所在对象即可查看调试面板。

## 设计约束

- Inspector 是只读工具，不直接修改 ECS 状态。
- 所有数据来源于 `World.GetDebugSnapshot()` 和 `FillXXX()` 系列 Debug API。
- Editor 脚本位于 `FrameWork/ECS/Editor`，不会进入正式构建。
- Entity 列表默认只显示前 64 个，避免 Inspector 在大量 Entity 时卡顿。

## 后续扩展

该 Inspector 是后续 `ECSWorldDebuggerWindow` 的基础。完整 EditorWindow 可以继续复用 `IECSRuntimeDebugSource` 和当前 Debug API，实现 Entities / Systems / ArcheTypes / Events / Singletons 等完整分页窗口。
