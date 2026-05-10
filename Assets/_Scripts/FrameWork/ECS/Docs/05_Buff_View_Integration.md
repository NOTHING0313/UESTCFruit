# 05. Buff 与 View 接入边界

## 1. Buff 接入：IBuffTargetResolver

Buff 模块不建议直接持有完整 `World`。当前提供：

```csharp
IBuffTargetResolver resolver = new WorldBuffTargetResolver(world);
```

它允许 Buff 读取或修改有限组件：

```csharp
bool alive = resolver.IsAlive(entity);
bool hasHealth = resolver.HasHealth(entity);
ref HealthComponent health = ref resolver.GetHealth(entity);
```

设计目的：限制 Buff 对 ECS 结构的修改权限，避免 Buff 随意创建 / 销毁 Entity 或增删组件。

如果 Buff 后续需要产生结构变化，推荐通过：

- Buff System 统一处理
- 请求组件，例如 `DamageRequestComponent`
- 帧指令，例如 `SimulationFrameCommandBuffer`

## 2. View 接入：IWorldViewReader

表现层应通过只读接口读取 World 状态：

```csharp
IWorldViewReader reader = new WorldViewReader(world);
```

可读取：

- View ID
- Position
- Health
- Alive Entities

表现层不要直接调用：

```csharp
world.SetComponent(...)
world.DestroyEntity(...)
ref PositionComponent position = ref world.GetComponent<PositionComponent>(entity)
```

除非该表现层脚本明确属于 ECS Adapter，而不是普通 View。

## 3. Unity View 规则

- `PositionComponent` 是逻辑真值。
- `Transform.position` 是表现结果。
- View 层不能通过 Transform 反向修改 ECS Position。
- 生成 / 销毁 GameObject 应通过 View 请求组件或 `WorldUnityExtensions` 辅助接口完成。

## 4. 推荐表现同步链路

```text
PrefabViewRequestComponent
    ↓ ViewSpawnSystem
ViewComponent(viewID)
    ↓ ViewSyncSystem
Transform.position = PositionComponent
    ↓ ViewDestroySystem
Destroy / Unregister GameObject
```
