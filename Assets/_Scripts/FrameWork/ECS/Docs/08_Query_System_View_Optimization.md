# Query / System / View 优化说明

## 1. Query 条件缓存

当前 Query 优化的核心是：缓存 `EntityQueryDescription` 对应的 ArcheType 分组匹配结果，而不是缓存某一帧的 Entity 结果。

原因是 Entity 结果会随着组件增删持续变化，如果直接缓存 Entity 列表，很容易拿到过期实体；而 ArcheType 分组只在结构变化时更新，因此可以通过 `ArcheTypeVersion` 判断缓存是否过期。

推荐 System 写法：

```csharp
private readonly List<Entity> _entities = new List<Entity>(128);
private EntityQueryDescription _query;

protected override void OnSystemCreate()
{
    _query = World.Query().With<PositionComponent>().With<VelocityComponent>().BuildDescription();
}

public override void Tick(in SimulationContext context)
{
    World.FillQuery(_query, _entities, false);

    for (int i = 0; i < _entities.Count; i++)
    {
        Entity entity = _entities[i];
    }
}
```

## 2. Execute / ExecuteSorted / Fill 的选择

- `Execute()`：返回未排序快照，会分配新的 `List<Entity>`，适合测试或临时逻辑。
- `ExecuteSorted()`：返回稳定排序快照，适合结果顺序会影响逻辑结果的场景。
- `Fill(results, sorted)` / `World.FillQuery(...)`：复用外部 List，适合高频 System。

## 3. System 改造原则

当前示例 System 已经改为：

1. 在 `OnSystemCreate()` 构建 QueryDescription。
2. 在字段中持有复用的 `List<Entity>`。
3. 在 `Tick()` 中调用 `World.FillQuery()`。
4. 在 `OnSystemDestroy()` 清理临时容器。

这样可以减少每个逻辑帧中的链式 Query 构造和 List 分配。

## 4. ViewManager 与对象池解耦

`ViewManager` 现在通过 `IViewInstanceProvider` 创建和释放表现对象：

- `DefaultViewInstanceProvider`：默认使用 `Instantiate / Destroy`。
- `PoolSystemViewInstanceProvider`：通过反射适配已有 `PoolSystem.GameObjectPoolCenter`，如果对象池不存在会自动回退到 `Instantiate / Destroy`。

使用对象池时：

```csharp
ViewManager viewManager = new ViewManager(new PoolSystemViewInstanceProvider());
```

这样 `ViewSpawnSystem` 和 `ViewDestroySystem` 不需要知道对象池实现，后续更换池化方案时只需要替换 Provider。
