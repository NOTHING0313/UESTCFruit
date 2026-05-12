# Fixed32 迁移评估

## 结论

当前 ECS Core 里的 `float` 转换为 `Fixed32` 属于 **中等偏高复杂度** 的确定性改造，不建议一次性全局替换。推荐按下面顺序推进：

1. 先让逻辑层组件支持 `Fixed32`，尤其是 `PositionComponent`、`VelocityComponent`、`MoveSpeedComponent`、`PlayerInputSnapshotComponent`。
2. 再把 `SimulationContext.tickLength` 改为 `Fixed32`，并同步修改 `SimulateRunner` / `TimeSimulator` 的逻辑帧参数。
3. 最后处理 Unity Adapter 边界，在 Unity 输入、Transform、Inspector 显示处保留 `float`，只在进入 ECS Core 前转换为 `Fixed32`。

这样做能避免 Unity 表现层和 ECS 逻辑层互相污染，也能降低一次性改动导致的问题排查成本。

## 复杂度判断

### 低风险部分

- `SimulationFrameCommand`、`CommandBuffer`、`DebugCommandHistory`、`FrameCommandHistory` 本身不依赖浮点运算。
- `Entity`、`ComponentStore`、`ArcheType`、`WorldEvent`、`SystemManager` 等结构管理代码基本不需要改。
- EditorWindow 只负责展示值，能直接通过 `ToString()` 显示 `Fixed32`。

### 中风险部分

- 输入快照和移动系统当前使用 `float`，需要替换为 `Fixed32` 后重新检查所有运算路径。
- `SimulationContext.tickLength` 参与每帧移动计算，改为 `Fixed32` 后会影响所有 System 的接口调用。
- 测试脚本里大量 `Mathf.Approximately` 需要替换为基于 raw 值或 Fixed32 差值的断言。

### 高风险部分

- Unity 边界 API 仍然使用 `float` / `Vector2` / `Vector3`，不能直接要求全项目都改成 Fixed32。
- 如果已经有视图同步、动画、Transform 写入，这些地方必须明确区分“逻辑坐标”和“表现坐标”。
- `Fixed32Math.Atan2` 的参数顺序需要统一约定，否则后续迁移三角函数时容易出现方向错误。

## 推荐架构

建议新增确定性向量类型，而不是继续把 Unity 的 `Vector3` 混进逻辑层：

```csharp
public struct FixedVector3
{
    public Fixed32 x;
    public Fixed32 y;
    public Fixed32 z;
}
```

逻辑组件使用 `Fixed32` / `FixedVector3`，Unity Adapter 负责转换：

```csharp
Vector3 viewPosition = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);
```

## 不建议的做法

不要直接用全局搜索把 `float` 替换成 `Fixed32`。这会把 Editor、Unity 表现层、测试工具、时间采样层全部卷进来，改动范围过大，并且会破坏 Unity API 的调用便利性。

## 建议拆分任务

1. 引入 `FixedVector2` / `FixedVector3`。
2. 新增 Fixed 版组件，不急着删除旧组件。
3. 改造 `InputMoveSystem` / `MovementSystem`。
4. 改造 `SimulationContext`。
5. 修改测试断言。
6. 最后清理旧 float 组件。
