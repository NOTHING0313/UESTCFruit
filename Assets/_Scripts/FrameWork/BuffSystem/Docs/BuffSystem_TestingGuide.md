# BuffSystem Testing Guide

## Phase 3I-12J Effect / CompositeEffect 行为测试入口

Phase 3I-12J 新增 Editor-only Effect / CompositeEffect 专项测试入口，用于覆盖 `BuffEffectRegistry`、单 Effect 生命周期、missing / invalid Effect、CompositeEffect 调用顺序、Event Effect 和 Graph-generated style 调用链。

菜单入口：

```text
Tools / BuffSystem / Testing / Run BuffSystem Effect Tests
Tools / BuffSystem / Testing / Open BuffSystem Effect Test Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemEffectTestEntry.RunEffectTests()
BuffSystem.EditorTesting.BuffSystemEffectTestEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/效果测试结果.md
```

覆盖范围：

- Effect 能力发现：探测 `BuffEffectRegistry`、`BuffEffectExecutorBase`、`IBuffEventEffectExecutor<TEvent>`、CompositeEffect authoring pattern 和 graph-generated style pattern。
- BuffEffectRegistry：验证注册、重复注册替换语义、`Remove`、`Clear`、missing id 和测试 registry 隔离。
- 单 Effect 生命周期：验证 `OnApply`、`OnTick`、`OnRemove`、`OnRefresh`、`OnStackChanged` 和 `BuffEffectContext`。
- Missing / Invalid Effect：验证未注册 EffectId、`0` / 负数 EffectId 的 no-crash 行为，并记录当前 public view 语义。
- CompositeEffect 顺序：使用测试内 double 验证多个 action 在各生命周期中的声明顺序。
- CompositeEffect 生命周期分发：通过 in-memory `BuffSystemCore` 验证 Add / Tick / Remove / RefreshAll / Append / ClearAll / Expire 的 composite 分发。
- Event Effect：验证匹配事件、错误事件、Tick 隔离、Remove 后停止事件以及事件上下文。
- Graph-generated style：验证 readonly action 字段和 `IBuffGraphAction.Execute(in context)` 风格调用链。

报告状态：

- `PASS`：用例通过。
- `FAIL`：用例断言失败或抛出异常。
- `SKIP`：显式跳过。
- `NOT_SUPPORTED`：当前项目未发现对应 authoring pattern 或 public/runtime API，测试不改 runtime。

最终 Summary：

- `Failed > 0` 时为 `FAIL`。
- `Failed == 0` 且存在 `NOT_SUPPORTED` 时为 `PARTIAL_NOT_SUPPORTED`。
- `Failed == 0` 且 `NotSupported == 0` 时为 `PASS`。

边界声明：

- 该入口只使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry` / `BuffSystemCore`。
- 该入口不创建 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 ID Registry。
- 该入口不修改 `BuffEffectRegistryBootstrap`。
- 该入口不修改 production whitelist / validation whitelist / compressed eligibility。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。
- 该入口不宣称 rollback-ready。

## Phase 3I-12L Trigger / EventTrigger 专项测试入口

Phase 3I-12L 新增 Editor-only Trigger / EventTrigger 专项测试入口，用于覆盖 Tick trigger 与 EventTrigger 的配置保存、事件热路径、上下文、生命周期交错、storage eligibility fallback 和边界行为。

菜单入口：

```text
Tools / BuffSystem / Testing / Run BuffSystem Trigger Tests
Tools / BuffSystem / Testing / Open BuffSystem Trigger Test Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemTriggerTestEntry.RunTriggerTests()
BuffSystem.EditorTesting.BuffSystemTriggerTestEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/触发器测试结果.md
```

覆盖范围：

- Trigger Discovery：探测 `BuffConfigData.BuffTriggerType` / `EventIds`、`BuffDefinition.TriggerType` / `EventIds`、`IBuffSystem.Raise<TEvent>`、`IBuffEventEffectExecutor<TEvent>` 和既有事件 smoke runner。
- Trigger Config：验证 Tick / EventTrigger 配置保存、非法 trigger 文档化、`CopyTo` 与 `ToDefinition` 是否保留 trigger 字段。
- Tick Trigger Isolation：验证 Tick-only Buff 会触发 `OnTick`，但不会被 `Raise<TEvent>` 触发事件回调；Remove / Expire 后不再 Tick。
- EventTrigger Execution：验证 Add / Tick 不触发事件回调，匹配 EventId 触发、错误 EventId 不触发，Remove / Expire 后不再触发，多层行为按当前 runtime 语义记录。
- Trigger Context：验证事件回调中的 Target / Source / Definition / EventId，并覆盖错误 Source / Target 不泄漏。
- Lifecycle Interleaving：覆盖 Add / Event / Remove、Add / Event / Expire、Refresh / Event、Append / Event、ClearAll / Event。
- Storage / Eligibility：验证 EventTrigger 不满足 CompressedParallel eligibility，并在 compressed validation factory 下 fallback 到 EntityPerStack；Tick trigger 合法 compressed 定义仍可通过 eligibility。
- Boundary：记录 unknown event、null payload、reentrant trigger 和 OnWorldRestored 后事件索引行为。

报告状态：

- `PASS`：用例通过。
- `FAIL`：用例断言失败或抛出异常。
- `SKIP`：显式跳过。
- `NOT_SUPPORTED`：当前 public/runtime API 不支持该行为，测试不改 runtime。
- `MANUAL_REQUIRED`：需要人工或 Unity 场景验证的能力。

最终 Summary：

- `Failed > 0` 时为 `FAIL`。
- `Failed == 0` 且存在 `MANUAL_REQUIRED` 时为 `PARTIAL_MANUAL_REQUIRED`。
- `Failed == 0` 且未发现 runtime trigger 能力时为 `TRIGGER_RUNTIME_API_NOT_FOUND`。
- 其余情况为 `PASS`。

边界声明：

- 该入口只使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry` / `BuffSystemCore`。
- 该入口不创建 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 ID Registry。
- 该入口不修改 `BuffEffectRegistryBootstrap`。
- 该入口不修改 production whitelist / validation whitelist / compressed eligibility。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。
- 该入口不宣称 rollback-ready。

## Phase 3I-12I Storage / CompressedParallel 自动化测试入口

Phase 3I-12I 新增 Editor-only Storage / CompressedParallel 专项测试入口，用于自动化覆盖 EntityPerStack baseline、compressed eligibility、EntityPerStack vs Compressed public API 行为一致性、restore hook / cache 和轻量 performance snapshot。

菜单入口：

```text
Tools / BuffSystem / Testing / Run BuffSystem Storage Tests
Tools / BuffSystem / Testing / Open BuffSystem Storage Test Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemStorageTestEntry.RunStorageTests()
BuffSystem.EditorTesting.BuffSystemStorageTestEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/存储模式测试结果.md
```

覆盖范围：

- Discovery：发现 EntityPerStack、CompressedParallel validation factory、compressed eligibility utility、既有 compressed runners 和安全 Editor-only 创建能力。
- EntityPerStack Baseline：Add / Tick / Remove、Append + MaxStack、RefreshAll、ReplaceEarliestWhenFull、Expire、Source / Target 隔离。
- Compressed Eligibility：valid config、EventTrigger fallback、storage mode fallback、991001 smoke pilot eligibility，以及 EffectId 不属于 runtime eligibility 的事实记录。
- EntityPerStack vs Compressed：TryGetBuff、GetBuffs(target)、Append、RefreshAll、ReplaceEarliestWhenFull、Expire、RemoveBySource、ClearAll 的 public API 对齐。
- Restore Hook / Cache：通过反射调用现有 internal `OnWorldRestored`，只验证 cache / lookup 重建，不声明 RollBackSystem ready。
- Performance Snapshot：轻量 Add / Tick / Remove 计时快照，只记录指标，不按耗时阈值判失败。

Compressed 自动化策略：

- Editor 测试通过反射调用现有 internal `BuffSystemCore.CreateForCompressedParallelValidation(...)`。
- 如果该 internal factory 不可用，compressed compare / restore / performance case 标记为 `MANUAL_REQUIRED`，不会为了测试修改 runtime。
- `MANUAL_REQUIRED` 会使报告结果变为 `PARTIAL_MANUAL_REQUIRED`，但不等价于 runtime fail。

显式未覆盖：

- EventTrigger：compressed 当前按设计 fallback EntityPerStack，留给 Trigger 专项。
- Tag：由 Phase 3I-12G Tag runner 覆盖。
- Rollback：不宣称 rollback-ready。
- View / Scene / Prefab：不运行 PlayMode，不保存 scene。

边界声明：

- 该入口只使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry` / `BuffSystemCore`。
- 该入口不创建 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 ID Registry。
- 该入口不修改 `BuffEffectRegistryBootstrap`。
- 该入口不修改 production whitelist / validation whitelist / compressed eligibility。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。
- 单个 case 失败不会中断后续 case，失败会写入 `存储模式测试结果.md`。

## Phase 3I-12G Tag 专项测试入口

Phase 3I-12G 新增 Editor-only Tag 专项测试入口，用于发现并记录当前 BuffSystem 的 Tag 能力边界。

菜单入口：

```text
Tools / BuffSystem / Testing / Run BuffSystem Tag Tests
Tools / BuffSystem / Testing / Open BuffSystem Tag Test Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemTagTestEntry.RunTagTests()
BuffSystem.EditorTesting.BuffSystemTagTestEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/标签测试结果.md
```

当前发现口径：

- `BuffConfigData.Tags` 是 authoring / config metadata。
- `BuffConfigDataLoader` 具备 config-level tag lookup，例如 `BuffHasTag`、`FindBuffsWithTag`、`FindBuffWithAllTags`。
- `BuffDefinition` 当前不保存 Tag。
- `IBuffSystem` 当前没有按 Tag 查询 live runtime Buff 的 public API。
- 因此 Tag runner 在 runtime Tag query 缺失时输出 `TAG_RUNTIME_API_NOT_FOUND`，并将相关 runtime case 标记为 `NOT_SUPPORTED`，不会修改 runtime 来让测试变绿。

覆盖范围：

- Tag Discovery：探测配置字段、loader 查询、runtime query 和 cleanup 能力。
- Tag Config：验证 `BuffConfigData.Tags` authoring 字段与 `CopyTo` 的稳定性。
- Tag Query / Target Source Isolation / Stack Refresh Replace / Remove Expire Cleanup / Boundary：仅在存在 runtime public Tag query API 后才能实测；当前按 `NOT_SUPPORTED` 记录。

显式未覆盖：

- EventTrigger：留到 Trigger 专项。
- CompressedParallel：留到 12I。
- Rollback：不宣称 rollback-ready。
- View / Scene / Prefab：不运行 PlayMode，不保存 scene。

边界声明：

- 该入口只做 Editor-only 测试与报告。
- 该入口不新增 Tag runtime API。
- 该入口不修改 `BuffSystemCore.cs` 或 `IBuffSystem`。
- 该入口不创建 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 ID Registry。
- 该入口不修改 `BuffEffectRegistryBootstrap`。
- 该入口不修改 production whitelist / validation whitelist / compressed eligibility。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene 或 Prefab。
- 单个 case 失败不会中断后续 case，缺失能力会记录为 `NOT_SUPPORTED`。

## Phase 3I-12H Lifecycle 专项测试入口

Phase 3I-12H 新增 Editor-only 生命周期专项测试入口，用于深测 BuffSystem 生命周期回调在边界与交错操作下的触发次数、顺序证据和 Effect context。

菜单入口：

```text
Tools / BuffSystem / Testing / Run BuffSystem Lifecycle Tests
Tools / BuffSystem / Testing / Open BuffSystem Lifecycle Test Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemLifecycleTestEntry.RunLifecycleTests()
BuffSystem.EditorTesting.BuffSystemLifecycleTestEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/生命周期测试结果.md
```

覆盖范围：

- OnApply：Add、Append、RefreshAll、Replace 的 apply 边界。
- OnTick / TickInterval：TickInterval、Remove 后 Tick、Expire 后 Tick、多层 Tick 口径。
- OnRemove：Manual Remove、Expire、ClearAll、Remove missing、Replace remove 语义。
- OnRefresh：RefreshAll、Append、Replace 与 RemainingFrames 刷新语义。
- OnStackChanged：Append、MaxStack、Remove、Expire、RefreshAll、Replace 的 stack delta。
- Interleaving：Add / Refresh / Remove / Expire / ClearAll / Replace 交错回调。
- Effect Context：OnApply、OnTick、OnRemove、OnRefresh、OnStackChanged 的 target/source/config context。

显式未覆盖：

- EventTrigger：留到 Trigger 专项。
- Tag：留到 12G。
- CompressedParallel：留到 12I。
- Rollback：不宣称 rollback-ready。
- View / Scene / Prefab：不运行 PlayMode，不保存 scene。

边界声明：

- 该入口只使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry` / `BuffSystemCore`。
- 该入口使用测试内部 `CountingLifecycleEffect`，只注册到测试自有 `BuffEffectRegistry`。
- 该入口不创建 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 ID Registry。
- 该入口不修改 `BuffEffectRegistryBootstrap`。
- 该入口不修改 production whitelist / validation whitelist / compressed eligibility。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene 或 Prefab。
- 单个 case 失败不会中断后续 case，失败会记录回调计数与事件序列。

## Phase 3I-12F Functional Coverage 测试入口

Phase 3I-12F 新增 Editor-only 基础功能语义覆盖入口，用于覆盖 BuffSystem public API 的基础功能矩阵。

菜单入口：

```text
Tools / BuffSystem / Testing / Run BuffSystem Functional Coverage Tests
Tools / BuffSystem / Testing / Open BuffSystem Functional Coverage Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemFunctionalCoverageEntry.RunFunctionalCoverageTests()
BuffSystem.EditorTesting.BuffSystemFunctionalCoverageEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/功能覆盖测试结果.md
```

覆盖范围：

- Add / Query：`AddBuff`、`TryGetBuff`、`GetBuffs(target)`、错误 target/source/config 查询。
- Duration / Expire：有限 duration、forever、过期移除、过期 `OnRemove`。
- Stack / Refresh / Replace：Append、MaxStack、RefreshAll、ReplaceEarliestWhenFull、StackChanged。
- Remove / Clear：手动 Remove、ClearAll、不存在 Buff remove、过期后重复 remove。
- Source / Target：同 config 多 target / 多 source 隔离。
- Effect / Lifecycle Basic：`OnApply`、`OnTick`、`OnRemove`、`OnRefresh`、`OnStackChanged`、Effect context。
- Boundary：`MaxStack=1`、`Duration=1`、`TickInterval > Duration`、缺失 definition、缺失 effect。

显式未覆盖：

- Tag：留到后续独立阶段。
- CompressedParallel：留到后续独立 storage coverage 阶段。
- Rollback：不宣称 rollback-ready。
- View / Scene / Prefab：不运行 PlayMode，不保存 scene。

边界声明：

- 该入口只使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry` / `BuffSystemCore`。
- 该入口不创建 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 ID Registry。
- 该入口不修改 `BuffEffectRegistryBootstrap`。
- 该入口不修改 production whitelist / validation whitelist / compressed eligibility。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene 或 Prefab。
- 单个 case 失败不会中断后续 case，失败会记录到报告。

## Phase 3I-12B 高强度测试入口

Phase 3I-12B 新增 Editor-only 高强度测试入口，用于覆盖 BuffSystem 的压力、性能、随机模糊和稳定性长跑场景。

菜单入口：

```text
Tools / BuffSystem / Testing / Run Advanced BuffSystem Tests
Tools / BuffSystem / Testing / Run BuffSystem Stress Tests
Tools / BuffSystem / Testing / Run BuffSystem Performance Tests
Tools / BuffSystem / Testing / Run BuffSystem Fuzz Tests
Tools / BuffSystem / Testing / Run BuffSystem Soak Tests
Tools / BuffSystem / Testing / Open BuffSystem Advanced Test Result
```

静态调用入口：

```text
BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.RunAllAdvancedBuffSystemTests()
BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.RunStressTests()
BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.RunPerformanceTests()
BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.RunFuzzTests()
BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.RunSoakTests()
BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.OpenLatestResult()
```

结果输出：

```text
Assets/_Scripts/FrameWork/BuffSystem/Test/测试结果.md
```

Profile：

```text
Quick: EntityCount=500, BuffPerEntity=5, TickFrames=1000, FuzzIterations=5000, SoakFrames=5000, QueryIterations=10000, ChurnIterations=5000
Standard: EntityCount=2000, BuffPerEntity=10, TickFrames=5000, FuzzIterations=50000, SoakFrames=20000, QueryIterations=100000, ChurnIterations=50000
Heavy: EntityCount=10000, BuffPerEntity=20, TickFrames=10000, FuzzIterations=200000, SoakFrames=100000, QueryIterations=500000, ChurnIterations=200000
```

当前菜单默认运行 Quick Profile。Heavy Profile 通过代码常量保护，默认关闭，避免误触导致 Unity Editor 长时间卡顿。

覆盖范围：

- Stress：大量 Entity / Buff 的 Add / Tick / Remove、同 ConfigId 多 target、高频 Stack / Refresh、AddRemove churn。
- Performance：Add、Tick、Remove、TryGetBuff、GetBuffs(target)，并拆分 Setup / Measured 统计窗口；Query 性能用例会同时记录命中 / 未命中统计和 public query 不变量。
- Fuzz：固定 seed 的 Add / Remove / Tick / Refresh / Query 随机序列，并记录失败 iteration、action 名称、expected active/stack before-after、实际 TryGet / GetBuffs 结果和最近 50 条操作。
- Soak：长时间 Tick、周期性 Add / Remove / Refresh / Query、Buff 数量和内存趋势不应无限增长。
- CompressedParallel：Advanced Test 不硬接 MonoBehaviour ContextMenu Runner，不调用 internal validation factory；报告中标记为 ManualRequired。

边界声明：

- 该入口只覆盖 BuffSystem 高强度 Editor-only 路径。
- 该入口不创建正式 Buff asset。
- 该入口不生成 Effect.cs。
- 该入口不写 registry。
- 该入口不修改 Bootstrap。
- 该入口不修改 production whitelist / validation whitelist。
- 该入口不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene 或 Prefab。
- 该入口不证明 rollback-ready。
- 该入口不证明 View 场景表现正确。
- 该入口不等价于完整 PlayMode / 网络同步测试。
- Quick Profile PASS 只能代表 Quick 烈度下的 Editor-only 自动用例通过；Standard / Heavy 需要人工单独运行或解除保护后验证。
- CompressedParallel 高强度对比仍需手动运行 `BuffSystemCompressedParallelValidationRunner` 与 `BuffSystemStoragePerformanceRunner`。

Fuzz oracle 口径：

```text
action mapping: Add / Remove / Tick / Refresh / TryGet / GetBuffs / AddTwiceAndTick / ClearAll
expectedStack <= 0 => expectedActive=false
Add / Remove / Tick / Refresh / Query 后以 public TryGet 可见性同步 expectedActive / expectedStack
duration 无法由轻量模型精确模拟时，Fuzz 只做 public query 合法性弱不变量
PotentialRuntimeBehaviorMismatch 表示需人工复核 public behavior，不等于本入口已证明 runtime bug
```

## Phase 3I-12A 入口

BuffSystem 当前新增 Editor-only 测试编排入口：

```text
Tools / BuffSystem / Testing / Run All BuffSystem Tests
Tools / BuffSystem / Testing / Run BuffSystem Smoke Tests
Tools / BuffSystem / Testing / Open Last BuffSystem Test Report
```

Unity MCP、batchmode 或其他自动化入口可以调用：

```text
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAllBuffSystemTests()
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunUnitTests()
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunIntegrationTests()
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunWhiteBoxTests()
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunBlackBoxTests()
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunSmokeTests()
BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAuthoringSmokeTests()
```

报告输出：

```text
Temp/BuffSystemTestReports/latest.json
Temp/BuffSystemTestReports/latest.md
```

## 覆盖范围

自动入口覆盖：

- Unit：`BuffDefinitionRegistry`、`BuffEffectRegistry`、compressed eligibility helper。
- Integration：内存 `World` 中的 `BuffSystemCore` Add / Tick / TryGet / GetBuffs / Remove。
- WhiteBox：通过反射调用 `OnWorldRestored`，确认只重建派生缓存，不触发生命周期 Effect。
- BlackBox：通过 public API 验证不同 source 的查询表现。
- Smoke：只读验证 `991001 Debug_CompressedParallel_TickSmoke`、`990101` production effect 注册、compressed eligibility。
- AuthoringSmoke：只读扫描 `BuffConfigData`、检测 Graph / Composite authoring 服务类型、扫描 Bootstrap 注册行。

不自动覆盖：

- 现有五个 MonoBehaviour ContextMenu Runner 的真实执行。
- View production path 场景验证。
- PlayMode / Scene / Prefab 验证。
- RollBackSystem restore 正确性。
- 性能 Runner 的完整耗时测量。
- production whitelist 扩大。

## 现有 Runner 边界

以下 Runner 仍保留为手动验证入口：

```text
BuffSystemPhase2AValidationRunner
BuffSystemCompressedParallelValidationRunner
BuffSystemRestoreHookValidationRunner
BuffSystemStorageBehaviorConsistencyRunner
BuffSystemStoragePerformanceRunner
```

Phase 3I-12A 入口只检测这些 Runner 类型和 ContextMenu 方法是否存在，不会在 Editor 测试入口中创建场景对象来伪造执行。

## 失败处理

每个测试 case 独立执行。单个 case 失败后，Runner 会继续执行后续 case，并在报告中记录失败原因和异常。

在 Unity batchmode 中，如果最终报告存在失败，入口会调用 `EditorApplication.Exit(1)`。

## MCP / 本地脚本

辅助脚本：

```text
Tools/BuffSystemTesting/run_buffsystem_mcp_tests.ps1
```

该脚本不会硬编码未知 MCP API。推荐流程：

1. 由 Unity MCP 或 Unity `-executeMethod` 调用 `BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAllBuffSystemTests`。
2. 使用脚本等待并读取 `Temp/BuffSystemTestReports/latest.json`。

```powershell
.\Tools\BuffSystemTesting\run_buffsystem_mcp_tests.ps1 -WaitForReport
```

## 边界声明

- 不宣称 100% 覆盖。
- 不宣称 BuffSystem rollback-ready。
- 不修改 BuffSystem runtime。
- 不修改 `BuffEffectRegistryBootstrap`。
- 不修改 production whitelist / validation whitelist。
- 不创建正式 Buff asset。
- 不生成正式 Effect 文件。
- 不保存 scene。
