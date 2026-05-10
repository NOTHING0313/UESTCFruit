# ECS Performance Benchmark

## 目的

`ECSPerformanceBenchmarkBootstrap` 用于粗略观测当前 ECS 框架在常见高频路径上的运行成本，包括：

1. Entity 创建。
2. Component 写入。
3. Query 条件缓存后的 `FillQuery`。
4. `World.Tick` 与 `MovementSystem` 执行。
5. Entity 销毁与组件清理。

这个测试不是精确的 Unity Profiler 替代品，它的主要价值是为后续优化提供对比基线。

## 使用方式

把 `ECSPerformanceBenchmarkBootstrap` 挂到一个空 GameObject 上，进入 Play Mode 即可自动运行。

也可以在 Inspector 组件右键菜单中调用：

```text
Run ECS Benchmark
```

## 主要参数

```text
runOnStart
    是否在 Start 时自动执行。

entityCount
    创建的 Entity 数量，默认 10000。

tickCount
    正式计时的逻辑帧数量，默认 1000。

warmupTickCount
    预热逻辑帧数量，默认 16，不计入正式 Tick 耗时。

queryRepeatCount
    Query Fill 重复执行次数，默认 100。

tickLength
    逻辑帧步长，默认 0.02。

enableSystemProfile
    是否开启 SystemProfileInfo 统计。

includeDestroyBenchmark
    是否测试 Entity 销毁性能。
```

## 结果解读

每条 Benchmark 会输出：

```text
Total
    当前测试项总耗时。

Avg
    当前测试项平均单次操作耗时。

Ops
    操作次数。

MemoryDelta
    当前测试项前后托管内存变化。
```

`MemoryDelta` 使用 `GC.GetTotalMemory` 估算，只能作为趋势参考。更精确的 GC Alloc 应以 Unity Profiler 为准。

## 注意事项

1. 第一次运行可能受到 JIT、Unity Editor 状态、Console 输出等因素影响。
2. 建议多运行几次，观察平均趋势，而不是只看单次结果。
3. 如果要对比优化效果，应保持参数一致。
4. 如果 Entity 数量较大，测试可能会让 Editor 短暂卡顿，这是预期行为。
5. SystemProfileInfo 使用 Stopwatch 统计真实执行耗时，不参与 ECS 逻辑。


## 容量预热

Benchmark 新增 `enableCapacityPrewarm` 开关，默认开启。开启后会在创建 Entity 前调用：

```csharp
world.EnsureEntityCapacity(entityCount);
world.EnsureComponentCapacity<PositionComponent>(entityCount);
world.EnsureComponentCapacity<VelocityComponent>(entityCount);
world.EnsureComponentCapacity<HealthComponent>(entityCount);
```

这些 API 内部统一使用 `ToolFunction.EnsureArrayLength(ref array, length)` 扩容。`ComponentStore` 的 sparse 数组在扩容后会把新增位置初始化为 `-1`，避免默认值 `0` 被误认为 denseIndex。

该预热主要用于减少大量创建 Entity / Component 时的数组扩容抖动。


## 细分 Benchmark 项目

当前 Benchmark 额外输出以下组件访问与结构变更路径：

1. `Query + GetComponent<T1,T2>`：通用 Query 结果列表 + 单实体组件查找路径。
2. `ForEach<T>`：单组件 dense 遍历路径。
3. `ForEach<T1,T2>`：双组件 dense 遍历路径。
4. `ForEach<T1,T2,T3>`：三组件 dense 遍历路径。
5. `SetComponent Overwrite Existing`：覆盖已有组件，不改变 ArcheType。
6. `AddComponent New Type`：新增组件并触发 ArcheType 迁移。
7. `RemoveComponent`：移除组件并触发 ArcheType 迁移。

这些项目用于区分查询成本、组件访问成本与结构变更成本。
