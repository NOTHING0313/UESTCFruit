# BuffSystem Changelog
## CodexUnityBridge - BuffSystem Suite Result Parser Fix

- 修正 `CodexUnityBuffSystemBridge` 的 `testResult` 解析逻辑，避免非 `tag` suite 仅因报告正文提到 `TAG_RUNTIME_API_NOT_FOUND` 被整体误判。
- `tag` suite 使用 `FAIL > TAG_RUNTIME_API_NOT_FOUND > PASS > Unknown` 优先级。
- `full_report` suite 只按报告文件是否存在返回 `REPORT_EXISTS` / `REPORT_NOT_FOUND`，不把 Tag 边界说明当作总报告结果。
- 其他 suite 使用 `FAIL > PASS > Unknown` 优先级，并忽略正文中的已知 Tag 边界说明。
- `buffsystem_status.json` / `buffsystem_messages.log` 增加解析规则与被忽略边界文本信息，便于后续排查。
- 本阶段不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap、whitelist、测试用例、ProjectSettings 或 Packages。

## CodexUnityBridge - BuffSystem Suite Bridge

- 新增 UESTCFruit 专用 `CodexUnityBuffSystemBridge` Editor-only 文件请求监听器。
- 支持 `Temp/CodexUnityBridge/request_buffsystem.json` 请求文件。
- 通过固定 suite 白名单执行 BuffSystem Editor-only 测试入口：`functional`、`lifecycle`、`tag`、`storage`、`trigger`、`effect`、`advanced_standard`、`full_report`。
- 输出 `Temp/CodexUnityBridge/buffsystem_status.json` 和 `Temp/CodexUnityBridge/buffsystem_messages.log`。
- `full_report` 只检查既有总报告文件，不运行测试。
- 不支持任意 method 反射执行；JSON 只能选择固定 suite。
- 本阶段不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap、whitelist、ProjectSettings 或 Packages。

## Phase 3I-12K - BuffSystem Full Coverage 总报告

- 汇总 Functional Coverage、Lifecycle、Tag、Storage / CompressedParallel、Trigger / EventTrigger、Effect / CompositeEffect 与 Advanced Standard Profile 的测试结果。
- 生成 BuffSystem Editor-only 单体测试总报告：`Assets/_Scripts/FrameWork/BuffSystem/Test/BuffSystem_测试总报告.md`。
- 明确 Tag runtime query 当前为 `TAG_RUNTIME_API_NOT_FOUND`，不在本阶段补 runtime API。
- 明确 Rollback、View、PlayMode、Scene / Prefab、Network Sync 与 production whitelist 不属于当前测试通过结论。
- 本阶段不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap 或 whitelist。

## Phase 3I-12E - Advanced Standard Profile 实跑与归档

- 新增 `Tools / BuffSystem / Testing / Run BuffSystem Advanced Standard Tests` Editor 菜单入口，并保留 `BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry.RunAllAdvancedBuffSystemStandardTests()` 作为 MCP / executeMethod 可调用入口。
- Standard Profile 复用现有 `BuffSystemAdvancedTestRunner.RunAll(BuffSystemAdvancedTestProfile.Standard)`，覆盖 Stress / Performance / Fuzz / Soak / Query / Churn / public query consistency / fuzz oracle consistency。
- Standard Profile 参数保持为：`EntityCount=2000`、`BuffPerEntity=10`、`TotalBuffCount=20000`、`TickFrames=5000`、`FuzzIterations=50000`、`SoakFrames=20000`、`QueryIterations=100000`、`ChurnIterations=50000`。
- Heavy Profile 仍保持 `AllowHeavyProfile=false` 默认关闭，不自动运行。
- 报告路径沿用现有 Advanced Runner 输出：`Assets/_Scripts/FrameWork/BuffSystem/Test/测试结果.md`。
- 本轮通过 MCP 调用 Standard 菜单时发生 timeout，`测试结果.md` 当前仍是旧 Quick Profile 报告；Standard Profile 实跑结果需要 Unity Editor 手动确认后再归档为 PASS / FAIL。
- 本阶段不修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap、whitelist、eligibility、Packages 或 ProjectSettings。

## Phase 3I-12J-A - GraphStyle Effect 测试修复

- 修复 Effect / CompositeEffect 专项测试中 Graph-generated style `OnApply` / `OnRemove` 用例的测试侧问题。
- 将 GraphStyle 用例切换为独立 `EffectId=991304`，避免复用 `991301` CountingEffect、`991302` CompositeTestEffect 或 `991303` EventEffect。
- 修正 `OnApply` / `OnRemove` 断言口径：通过 `BuffSystemCore` 调用时，Add / Remove 所在 Tick 可能伴随 `StackChanged` / `Tick` 等同帧生命周期，因此用例改为验证对应 action 是否被执行、executor 是否解析正确，并记录完整 trace。
- 增加 GraphStyle fixed case 的诊断信息：`Classification`、`KeyEvidence`、`ResolvedExecutorType`、`TraceBeforeAction`、`TraceAfterAction`、`TriggeredViaCore` 和 `LifecycleWarmupFrames`。
- 本阶段仅修改 Editor-only 测试与 Changelog；未修改 BuffSystem runtime、Graph codegen / emitter、registry、Bootstrap、whitelist、eligibility、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。

## Phase 3I-12J - BuffSystem Effect / CompositeEffect 行为测试

- 新增 BuffSystem Effect / CompositeEffect 专项测试入口：
  - `Tools / BuffSystem / Testing / Run BuffSystem Effect Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Effect Test Result`
- 新增 MCP / executeMethod 静态入口：
  - `BuffSystem.EditorTesting.BuffSystemEffectTestEntry.RunEffectTests()`
  - `BuffSystem.EditorTesting.BuffSystemEffectTestEntry.OpenLatestResult()`
- 新增 `BuffSystemEffectTestRunner`、`BuffSystemEffectTestReport`、`BuffSystemEffectTestCaseResult`，测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/效果测试结果.md`。
- 覆盖 Effect discovery、`BuffEffectRegistry`、单 Effect lifecycle、missing / invalid Effect、CompositeEffect order、CompositeEffect lifecycle dispatch、Event Effect 和 graph-generated style 调用链。
- CompositeEffect 行为测试使用测试内 double 验证顺序与分发，不新增 runtime CompositeEffect base，不修改 Graph 功能代码。
- Event Effect 测试使用现有 `IBuffSystem.Raise<TEvent>` 与 `IBuffEventEffectExecutor<TEvent>`，不新增 runtime trigger API。
- 报告 Summary 支持 `PASS` / `FAIL` / `PARTIAL_NOT_SUPPORTED`，并记录 Total、Passed、Failed、Skipped、NotSupported。
- 本阶段仅新增 Editor-only 测试与测试文档；未修改 BuffSystem runtime、`BuffSystemCore.cs`、`IBuffSystem`、registry、Bootstrap、whitelist、eligibility、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。

## Phase 3I-12L - BuffSystem Trigger / EventTrigger 专项测试

- 新增 BuffSystem Trigger / EventTrigger 专项测试入口：
  - `Tools / BuffSystem / Testing / Run BuffSystem Trigger Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Trigger Test Result`
- 新增 MCP / executeMethod 静态入口：
  - `BuffSystem.EditorTesting.BuffSystemTriggerTestEntry.RunTriggerTests()`
  - `BuffSystem.EditorTesting.BuffSystemTriggerTestEntry.OpenLatestResult()`
- 新增 `BuffSystemTriggerTestRunner`、`BuffSystemTriggerTestReport`、`BuffSystemTriggerTestCaseResult`，测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/触发器测试结果.md`。
- 覆盖 Trigger Discovery、Trigger Config、Tick trigger isolation、EventTrigger execution、Trigger context、Lifecycle interleaving、Storage / eligibility 和 Boundary。
- EventTrigger 测试使用现有 `IBuffSystem.Raise<TEvent>` 与 `IBuffEventEffectExecutor<TEvent>`，不会新增 runtime trigger API。
- EventTrigger storage 测试只验证当前 eligibility false / fallback 行为，不扩大 compressed whitelist，不修改 compressed eligibility。
- 如果当前 runtime trigger API 缺失，报告会标记为 `NOT_SUPPORTED` / `TRIGGER_RUNTIME_API_NOT_FOUND`，而不是修改 runtime。
- 本阶段仅新增 Editor-only 测试与测试文档；未修改 BuffSystem runtime、`BuffSystemCore.cs`、`IBuffSystem`、registry、Bootstrap、whitelist、eligibility、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。

## Phase 3I-12I - BuffSystem Storage / CompressedParallel 自动化测试

- 新增 BuffSystem Storage / CompressedParallel 专项测试入口：
  - `Tools / BuffSystem / Testing / Run BuffSystem Storage Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Storage Test Result`
- 新增 MCP / executeMethod 静态入口：
  - `BuffSystem.EditorTesting.BuffSystemStorageTestEntry.RunStorageTests()`
  - `BuffSystem.EditorTesting.BuffSystemStorageTestEntry.OpenLatestResult()`
- 新增 `BuffSystemStorageTestRunner`、`BuffSystemStorageTestReport`、`BuffSystemStorageTestCaseResult`，测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/存储模式测试结果.md`。
- 覆盖 Discovery、EntityPerStack baseline、compressed eligibility、EntityPerStack vs Compressed public API 行为一致性、restore hook / cache 和轻量 performance snapshot。
- Compressed 自动化通过反射调用现有 internal validation factory；如果 factory 不可用，相关 case 标记为 `MANUAL_REQUIRED`，不修改 runtime 来让测试变绿。
- 报告 Summary 支持 `PASS` / `FAIL` / `PARTIAL_MANUAL_REQUIRED`，并记录 Total、Passed、Failed、Skipped、ManualRequired。
- Performance snapshot 只记录指标，不按耗时阈值判失败。
- 本阶段仅新增 Editor-only 测试与测试文档；未修改 BuffSystem runtime、`BuffSystemCore.cs`、`IBuffSystem`、registry、Bootstrap、whitelist、eligibility、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。

## Phase 3I-12G - BuffSystem Tag 专项测试

- 新增 BuffSystem Tag 专项测试入口：
  - `Tools / BuffSystem / Testing / Run BuffSystem Tag Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Tag Test Result`
- 新增 MCP / executeMethod 静态入口：
  - `BuffSystem.EditorTesting.BuffSystemTagTestEntry.RunTagTests()`
  - `BuffSystem.EditorTesting.BuffSystemTagTestEntry.OpenLatestResult()`
- 新增 `BuffSystemTagTestRunner`、`BuffSystemTagTestReport`、`BuffSystemTagTestCaseResult`，测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/标签测试结果.md`。
- 增加 Tag 能力发现、Tag 配置、Tag 查询、Target/Source 隔离、Stack/Refresh/Replace、Remove/Expire 清理与边界测试矩阵。
- 当前发现 `BuffConfigData.Tags` 与 `BuffConfigDataLoader` config-level tag lookup 存在，但 `BuffDefinition` 不保存 Tag，`IBuffSystem` 当前没有 live runtime Tag query public API。
- 如果当前 runtime 不支持 Tag query API，报告会标记为 `NOT_SUPPORTED` / `TAG_RUNTIME_API_NOT_FOUND`，而不是修改 runtime。
- 本阶段不修改 BuffSystem runtime、`BuffSystemCore.cs`、`IBuffSystem`、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap、whitelist、eligibility、Packages 或 ProjectSettings。

## Phase 3I-12H - BuffSystem Lifecycle 专项深测

- 新增 BuffSystem 生命周期专项测试入口：
  - `Tools / BuffSystem / Testing / Run BuffSystem Lifecycle Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Lifecycle Test Result`
- 新增 MCP / executeMethod 静态入口：
  - `BuffSystem.EditorTesting.BuffSystemLifecycleTestEntry.RunLifecycleTests()`
  - `BuffSystem.EditorTesting.BuffSystemLifecycleTestEntry.OpenLatestResult()`
- 新增 `BuffSystemLifecycleTestRunner`、`BuffSystemLifecycleTestReport`、`BuffSystemLifecycleTestCaseResult`，测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/生命周期测试结果.md`。
- 覆盖 OnApply、OnTick / TickInterval、OnRemove、OnRefresh、OnStackChanged、生命周期交错和 Effect Context 基础正确性。
- 测试内部使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry` / `BuffSystemCore`，并用测试自有 `CountingLifecycleEffect` 记录回调计数与事件序列。
- 明确 EventTrigger、Tag、CompressedParallel、Rollback、View / Scene / Prefab 留给后续阶段，不宣称覆盖或 rollback-ready。
- 本阶段不修改 BuffSystem runtime、`BuffSystemCore.cs`、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap、whitelist、eligibility、Packages 或 ProjectSettings。

## Phase 3I-12F-FixRefreshAllFunctionalTest - 修正 RefreshAll 功能测试预期

- 修正 Functional Coverage 中 `RefreshAll` 的测试预期：`RefreshAll` 语义为刷新已有层，并在未满 `MaxStack` 时追加本次 incoming 层。
- 将原单一用例拆分为两个语义明确的 case：
  - `Functional_Stack_RefreshAll_NotFull_AppendsIncomingAndRefreshesExisting`
  - `Functional_Stack_RefreshAll_WhenFull_RefreshesWithoutAppending`
- 未满层场景验证：首次添加 2 层、再次添加 1 层后，public `Stack` 应增长到 3，并触发 `OnRefresh` / `OnStackChanged`。
- 满层场景验证：首次添加到 `MaxStack=3` 后再次添加，不再追加新层，但仍刷新现有层并触发 `OnRefresh`。
- 本阶段仅修改 Functional Coverage Editor-only 测试与 Changelog；未修改 BuffSystem runtime、`BuffSystemCore.cs`、registry、Bootstrap、whitelist、eligibility、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。

## Phase 3I-12F - Functional Coverage Tests 基础功能语义覆盖

- 新增 Editor-only 基础功能语义覆盖入口：
  - `Tools / BuffSystem / Testing / Run BuffSystem Functional Coverage Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Functional Coverage Result`
- 新增静态调用入口 `BuffSystem.EditorTesting.BuffSystemFunctionalCoverageEntry.RunFunctionalCoverageTests()` / `OpenLatestResult()`，可用于 Unity 菜单、MCP 或 batchmode `-executeMethod`。
- 新增 `BuffSystemFunctionalCoverageRunner`、`BuffSystemFunctionalCoverageReport`、`BuffSystemFunctionalCoverageCaseResult`，测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/功能覆盖测试结果.md`。
- 第一版覆盖 Add / Query、Duration / Expire、Stack / Refresh / Replace、Remove / Clear、Source / Target、Effect / Lifecycle Basic、Boundary 七类基础功能语义，case 数量不少于 25。
- 每个 case 独立执行，失败不会中断后续 case；报告记录 `Category`、`CaseName`、`Status`、`Expected`、`Actual`、`InvariantChecks`、`FailureReason`、`Exception`、`DurationMs`。
- 明确将 Tag、CompressedParallel、Rollback、View 标记为 `NotCovered`，留给后续独立阶段，不宣称 100% 覆盖或 rollback-ready。
- 本阶段仅新增 Editor-only 测试与测试文档；未修改 BuffSystem runtime、`BuffSystemCore.cs`、`IBuffSystem`、registry、Bootstrap、whitelist、eligibility、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 ProjectSettings。

## Phase 3I-12D - Advanced Test Oracle 修正与 Fuzz 最小复现

- 修正 Query Performance 用例的真实性断言：
  - `Perf_TryGetBuff_RepeatedQueries` 现在同时覆盖 active 命中、missing config miss、hit + miss 查询总数、ViewData `ConfigId` / `Target` / `Source` / `Stack` 合法性。
  - `Perf_GetBuffs_TargetQueries` 现在同时覆盖 active target 非空、empty target 空结果、返回 `Target` 一致、返回 `ConfigId` 属于测试 config set、查询总数和返回 ViewData 检查数。
- 修正 Fuzz oracle 诊断：
  - 新增 `FuzzAction` action name mapping，报告不再只显示 `action=0..7`。
  - Fuzz expected model 明确 `expectedStack <= 0` 视为 inactive。
  - Add / Remove / Tick / Refresh / Query 后以 public `TryGetBuff` 可见性同步 expected active / stack，避免轻量 oracle 在 duration / 延迟可见性上做过强假设。
  - Fuzz 失败路径会在抛出前写入 `ActualOperations`、`MeasuredElapsedMs`、`FailureIteration`、`ExpectedCounts`、`ActualCounts` 和最近 50 条 before/after 操作。
- 新增 deterministic repro case：`Repro_Fuzz_Seed32001_Iteration34`，用于复核 seed `32001`、iteration `34` 附近的历史失败路径；若仍出现不一致，应分类为 `PotentialRuntimeBehaviorMismatch`，不得直接修改 runtime。
- `测试结果.md` 输出补充 Query 命中 / 未命中统计、Fuzz action mapping、Fuzz model 更新规则和失败诊断字段。
- 本阶段仅修改 Advanced Test Editor-only 测试与测试文档；未修改 BuffSystem runtime、ECS、RollBackSystem、View、registry、Bootstrap、whitelist、Buff asset、Scene、Prefab 或 Packages。

## Phase 3I-12C - Advanced Test 烈度修正与真实性增强

- 提升 Advanced Test Quick / Standard / Heavy Profile 烈度参数：
  - Quick：`EntityCount=500`、`BuffPerEntity=5`、`TickFrames=1000`、`FuzzIterations=5000`、`SoakFrames=5000`、`QueryIterations=10000`、`ChurnIterations=5000`。
  - Standard：`EntityCount=2000`、`BuffPerEntity=10`、`TickFrames=5000`、`FuzzIterations=50000`、`SoakFrames=20000`、`QueryIterations=100000`、`ChurnIterations=50000`。
  - Heavy：`EntityCount=10000`、`BuffPerEntity=20`、`TickFrames=10000`、`FuzzIterations=200000`、`SoakFrames=100000`、`QueryIterations=500000`、`ChurnIterations=200000`，仍由 `AllowHeavyProfile=false` 默认关闭。
- Advanced Test 报告新增 / 强化：`ExpectedOperations`、`ActualOperations`、`InvariantChecks`、`InvariantFailures`、`SetupElapsedMs`、`MeasuredElapsedMs`、`SetupGCAllocBytes`、`MeasuredGCAllocBytes`、`GCMethod`、`GCMeasurementWindow`、`ProfileParameters`、`LastOperations`。
- Stress 用例补充真实 Add / Tick / Remove 计数、Remove 后 public query 可见性断言、Stack / RemainingFrames / Target 不变量。
- Performance 用例拆分 Setup / Measured 统计窗口，明确 OperationCount 口径，不再把 setup 成本混入核心 measured 指标。
- Fuzz 用例提升 iterations，固定 seed，记录最近 50 条操作，并加入轻量期望模型与 public query 不变量检查。
- Soak 用例提升长跑帧数，周期性执行 Add / Remove / Refresh / Query，并记录 active view 增长趋势与内存前后值。
- CompressedParallel 自动高强度对比不再以 `SKIP` 伪装覆盖，改为 `MANUAL_REQUIRED`，需手动运行既有 `BuffSystemCompressedParallelValidationRunner` / `BuffSystemStoragePerformanceRunner`。
- 本阶段仅修改 Advanced Test Editor-only 测试与测试文档；未修改 BuffSystem runtime、ECS、RollBackSystem、View、registry、Bootstrap、whitelist、Buff asset、Scene 或 Prefab。

## Phase 3I-12B - BuffSystem Stress / Performance / Fuzz / Soak 测试脚本

- 新增 BuffSystem 高强度 Editor-only 测试入口。
- 新增菜单：
  - `Tools / BuffSystem / Testing / Run Advanced BuffSystem Tests`
  - `Tools / BuffSystem / Testing / Run BuffSystem Stress Tests`
  - `Tools / BuffSystem / Testing / Run BuffSystem Performance Tests`
  - `Tools / BuffSystem / Testing / Run BuffSystem Fuzz Tests`
  - `Tools / BuffSystem / Testing / Run BuffSystem Soak Tests`
  - `Tools / BuffSystem / Testing / Open BuffSystem Advanced Test Result`
- 新增静态调用入口 `BuffSystem.EditorTesting.BuffSystemAdvancedTestEntry`，可用于菜单、Unity MCP `execute_menu_item` 或 Unity batchmode `-executeMethod`。
- 支持压力测试、性能测试、随机模糊测试和稳定性长跑测试。
- 测试结果输出到 `Assets/_Scripts/FrameWork/BuffSystem/Test/测试结果.md`。
- 默认使用 Quick Profile，避免 Unity Editor 卡死。
- Heavy Profile 通过 `AllowHeavyProfile=false` 默认关闭。
- 测试只使用 in-memory `World` / `BuffDefinitionRegistry` / `BuffEffectRegistry`，不创建正式 Buff asset，不生成 Effect.cs，不写 registry。
- 本阶段未修改 BuffSystem runtime、ECS、RollBackSystem、View、Scene、Prefab、registry、Bootstrap 或 whitelist。

## Phase 3I-12A - BuffSystem MCP test orchestration entry

- 新增 Editor-only 测试编排入口 `BuffSystem.EditorTesting.BuffSystemMcpTestEntry`。
- 新增菜单：
  - `Tools / BuffSystem / Testing / Run All BuffSystem Tests`
  - `Tools / BuffSystem / Testing / Run BuffSystem Smoke Tests`
  - `Tools / BuffSystem / Testing / Open Last BuffSystem Test Report`
- 新增静态调用入口：
  - `RunAllBuffSystemTests()`
  - `RunUnitTests()`
  - `RunIntegrationTests()`
  - `RunWhiteBoxTests()`
  - `RunBlackBoxTests()`
  - `RunSmokeTests()`
  - `RunAuthoringSmokeTests()`
- 新增 `BuffSystemFullTestRunner`，用于执行无场景对象依赖的 unit / integration / white-box / black-box / smoke / authoring smoke 子集。
- 新增报告模型 `BuffSystemTestReport` / `BuffSystemTestCaseResult`，测试结果写入：
  - `Temp/BuffSystemTestReports/latest.json`
  - `Temp/BuffSystemTestReports/latest.md`
- 新增覆盖矩阵，明确区分 `Covered` / `SmokeOnly` / `ManualScene` / `NotCovered`，不宣称 100% 覆盖。
- 新增 `BuffSystem_TestingGuide.md`，记录菜单入口、MCP / executeMethod 调用方式、报告路径、覆盖边界和手动验证项。
- 新增 `Tools/BuffSystemTesting/run_buffsystem_mcp_tests.ps1` 与 README，用于等待 / 读取测试报告；脚本不硬编码未知 Unity MCP API。
- 现有五个 MonoBehaviour ContextMenu Runner 仍保留为独立手动回归入口，本阶段只检测其类型和方法存在，不在测试入口中创建场景对象伪造执行。
- 默认 `RunDestructiveWriteSmoke=false`，不会创建 Buff asset、不会生成 Effect 模板、不会写 registry。
- 本阶段未修改 BuffSystem runtime、`BuffSystemCore.cs`、`BuffEffectRegistryBootstrap.cs`、BuffConfigData asset、registry、whitelist、eligibility、ECS、RollBackSystem、View、Scene / Prefab / `.meta` 或 Packages。

## Phase 3I-11V - CompositeEffect 文档 closeout 与验证清单

- 整理 CompositeEffect 图形化生成的推荐使用流程。
- 新增 `BuffSystem_CompositeEffectAuthoring.md`，集中归档适用场景、推荐图结构、节点职责、顺序规则、预览、草稿生成、一键生成、自动注册开关、失败清理策略、验证清单和 production 边界。
- 明确 `EffectCompositionRootNode` / `EffectNode` / `ScriptActionNode` 的职责边界。
- 明确 `BuffRootNode` / `EffectBindingNode` / `Action Placeholder` 的 legacy / deprecated 状态。
- 归档 CompositeEffect 预览、草稿生成、一键 Buff + CompositeEffect 生成的验证流程。
- 补充自动注册关闭 / 失败时的一键生成边界：不创建 `BuffConfigData`，避免 Buff 指向未注册 Effect。
- 补充 production whitelist、rollback-ready、runtime truth 的边界说明。
- 本阶段仅更新文档，未修改 runtime，未生成 Effect / Buff asset，未写 registry / Bootstrap。

## Phase 3I-11U - 一键创建 Buff + CompositeEffect 草稿

- Authoring Hub 图形化模式的 CompositeEffect 区域新增 `从图一键创建 Buff + CompositeEffect 草稿` 按钮。
- 一键流程复用 `BuffGraphCompositeEffectPlanBuilder` 与 `BuffGraphCompositeEffectEmitter`，先生成最终 CompositeEffect `.cs` 草稿。
- 一键流程会写入 Effect ID Registry，并在 Settings 开启自动注册时维护 `BuffEffectRegistryBootstrap.cs` auto 区块。
- Bootstrap 自动注册成功后，才会创建 `BuffConfigData` 草稿，并确保 `BuffConfigData.EffectId == CompositeEffectId`。
- 一键流程会写入 Buff ID Registry，并在生成报告中显示 CompositeEffect 路径、CompositeEffectId、CompositeEffectClassName、BuffConfigData 路径、Buff ConfigId、BuffName、BuffConfigData.EffectId 与 Registry 状态。
- 自动注册关闭或自动注册失败时，一键流程会停止在 Buff 创建前，保留已生成的 CompositeEffect `.cs` 与 Effect ID Registry，并显示 `registry.Register(...)` 手动片段，避免 Buff 指向未注册 Effect。
- 该流程只注册最终 CompositeEffect，不注册 child EffectNode；旧 `EffectBindingNode` 仍作为 legacy 信息被 Composite 流程忽略。
- 本阶段不修改 runtime core，不修改 `BuffEffectRegistry` public API，不加入 whitelist，不修改 compressed eligibility，不创建正式 gameplay Buff，不保存 scene。

## Phase 3I-11T - CompositeEffect 真实生成最小实现

- Authoring Hub 图形化模式的 CompositeEffect 区域新增 `从图创建 CompositeEffect 草稿` 按钮。
- 该按钮执行 `BuffCandidateGraph -> BuffGraphCompositeEffectPlan -> BuffGraphCompositeEffectEmitter -> CompositeEffect.cs` 的真实生成链路。
- 生成成功后会写入 Effect ID Registry，并在 Settings 开启时维护 `BuffEffectRegistryBootstrap.cs` auto 区块。
- 自动注册关闭时不会修改 Bootstrap，只在生成报告中显示 `registry.Register(...)` 手动片段。
- CompositeEffectId 优先使用 `EffectCompositionRootNode.FinalEffectId`；缺失且自动分配开启时分配正式段 EffectId；显式使用 990000+ Debug / Smoke / Reserved 保留段会被阻止。
- CompositeEffectClassName 优先使用 `EffectCompositionRootNode.FinalEffectClassName`，否则使用 BuffName / GraphName 生成 `*CompositeEffect`。
- 目标 `.cs` 已存在时阻止生成，不覆盖已有文件。
- 本阶段只注册最终 CompositeEffect，不注册 child EffectNode。
- 本阶段不创建 BuffConfigData，不实现“一键 Buff + CompositeEffect”，不修改 runtime core，不修改 whitelist / eligibility，不修改 BuffEffectRegistry public API。

## Phase 3I-11R - CompositeEffect Graph Generate 预览接入

- Authoring Hub 图形化模式新增 CompositeEffect 代码预览区。
- 预览会基于多个 EffectNode 和 ScriptActionNode 生成 CompositeEffect 代码文本。
- 支持复制预览代码到剪贴板，但不会写入 `.cs` 文件。
- 预览会显示 CompositeEffectId、CompositeEffectClassName、EffectNode 数量、Action 总数、顺序模式、生命周期摘要、预览状态，以及 Error / Warning / Info。
- 本阶段复用 `BuffGraphCompositeEffectPlanBuilder` 与 `BuffGraphCompositeEffectEmitter`，只生成字符串预览。
- 本阶段未生成 Effect 文件，未创建 BuffConfigData，未写 ID Registry，未自动注册 Effect，未修改 runtime / Bootstrap / whitelist / eligibility。

## Phase 3I-11P-GraphSemanticsCleanup - BuffCandidateGraph 图语义收口

- 新增 Editor-only `EffectCompositionRootNode`，作为新图推荐的 Effect 组合入口。
- 明确 `EffectCompositionRootNode.Effects` / 旧 `BuffRootNode.Effects` 连接表示成员关系，不表示执行顺序。
- `EffectNode.Next` 形成完整链时作为显式顺序；未使用 `Next` 时 fallback 到 `ExecutionOrder`。
- `EffectNode.Next` 与 `ExecutionOrder` 同时存在且顺序冲突时，Evaluation / Graph codegen 会报 Error。
- `ScriptActionNode.Next` 第一版按同生命周期内 Action 链校验，要求与 `ExecutionOrder` 保持一致；冲突会阻止 Graph codegen。
- `BuffRootNode` 保留旧图兼容，但菜单移动到 Deprecated 分组，节点 UI 显示兼容警告。
- `EmptyActionPlaceholderNode` 保留旧图兼容，但菜单移动到 Deprecated 分组，节点 UI 和 Evaluation 明确提示不会生成可运行调用。
- Authoring Hub 候选摘要新增 Effect 组合根、Effect 顺序模式、旧 BuffRoot 使用状态和废弃占位节点数量。
- Graph Bridge / Generate / Effect codegen 统一读取 Effect 顺序和组合根字段，避免 UI 摘要与生成计划口径不一致。
- 本阶段未生成 Effect `.cs` / CompositeEffect `.cs`，未创建 `BuffConfigData`，未写入 ID Registry，未自动注册 Effect。
- 本阶段未修改 runtime、`BuffSystemCore.cs`、`BuffEffectRegistryBootstrap.cs`、whitelist、eligibility、ECS、RollBackSystem、View、Scene / Prefab / `.meta` 或 xNode package。

## Phase 3I-11Q - CompositeEffect Editor-only 最小实现

- 新增 CompositeEffect 的 Editor-only plan / builder / emitter。
- 支持从多个 EffectNode 收集生命周期 ScriptActionNode，并生成单个 CompositeEffect 代码文本。
- CompositeEffect 仍是普通 BuffEffectExecutorBase 派生类，不修改 BuffSystem runtime。
- 复用已有 `BuffGraphEffectActionCallPlan` 与 `BuffScriptActionNodeValidator`，并在 Composite 生成前执行 EffectNode / ScriptActionNode 顺序和 Action 类型校验。
- CompositeEffect 跨 Effect 顺序复用 `BuffGraphEffectOrderUtility`：完整 `EffectNode.Next` 链优先，未使用 `Next` 时按 `ExecutionOrder`，冲突 / 重复 / 分叉 / 环会进入 error。
- CompositeEffect 的同 Effect + 同 lifecycle Action 顺序支持 `ScriptActionNode.Next` 完整链；未使用 `Next` 时按 `ExecutionOrder`，冲突 / 重复 / 分叉 / 环会进入 error。
- CompositeEffect 合并生命周期时按 Effect 顺序再按 Action 顺序生成调用，`OnStackChanged` 仍只调用 `Execute(in context)`，不传递 delta。
- `EffectBindingNode` 收口为 legacy fallback：菜单移动到 Deprecated 分组；存在 `EffectCompositionRootNode` / `EffectNode` 时，新结构优先，旧节点仅保留兼容。
- 本阶段未接入 Hub 按钮，未生成 Effect 文件，未自动注册 Effect，未创建 BuffConfigData，未写入 ID Registry。
- 本阶段未修改 `BuffSystemCore.cs`、`BuffEffectRegistryBootstrap.cs`、whitelist、eligibility、xNode package、Scene / Prefab / `.meta`。

## Phase 3I-11O-UXCleanup - 数值编辑页候选图旧入口收口

- 移除 Create Buff 页中的旧“从候选图导入基础字段”按钮。
- 移除 Effect Template 页中的旧“从候选图导入 Effect 字段 / 调用链”按钮。
- 候选图相关创建流程统一收口到“图形化编辑 -> Graph Generate”。
- 本阶段只调整 Editor UI，未修改 runtime、Bootstrap、whitelist，也未创建 Buff 或 Effect。
## Phase 3I-11O-FixCompile - 自动注册阶段编译错误修复

- 修复 `BuffAuthoringText.cs` 中因文案字符串断裂导致的 CS1010 / CS1002 编译风险，保留所有常量名并改为合法单行字符串。
- 修复 `BuffGraphGenerateService.cs` 中多处 report / result 文案字符串断裂导致的 CS1010 / CS1026 编译风险。
- 对自动注册新增 Editor-only 文件执行字符串静态检查，未发现同类断裂字符串。
- 本阶段只修复 Editor-only 编译问题，未修改 runtime，未修改 `BuffEffectRegistryBootstrap.cs`，未注册 Effect，未加入 whitelist。
## Phase 3I-11O - Effect 自动注册黑箱化最小实现

- 新增 Editor-only `BuffEffectBootstrapRegistrationScanner`，只读扫描 `BuffEffectRegistryBootstrap.cs` 中的 `registry.Register(...)` 注册行，并区分手工区与 auto 区块。
- 新增 Editor-only `BuffEffectBootstrapAutoRegistryPatcher`，只维护 `// <buffsystem-auto-effect-registry>` 与 `// </buffsystem-auto-effect-registry>` 之间的 auto 区块。
- 新增 Editor-only `BuffEffectBootstrapAutoRegistryReport`，用于向 Authoring Hub 展示自动注册写入结果、错误、警告和提示。
- Authoring Hub Settings 新增 `自动注册 Effect 到 Bootstrap` 开关，默认开启。
- Effect Template 生成 `.cs` 草稿成功并写入 ID Registry 后，会按 Settings 尝试维护 Bootstrap auto 区块；失败时保留已生成 Effect 和 ID Registry，不做回滚，并显示可手动复制的注册片段。
- Graph 生成主 Effect 草稿、以及一键创建 Buff + 主 Effect 草稿后，会在 ID Registry 写入成功时按 Settings 尝试维护 Bootstrap auto 区块。
- 单独从 Graph 创建 Buff 草稿不会触发 Effect 自动注册。
- auto 注册会拒绝 `990000+` Debug / Smoke / Reserved 保留段 EffectId，拒绝手工区同 ID / 同 class 冲突，拒绝 auto 区块 marker 不成对的文件。
- auto 区块内注册按 EffectId 升序写入，格式为 `registry.Register(200001, new GeneratedPoisonEffect());`。
- 本阶段不自动加入 whitelist，不修改 compressed eligibility，不代表 runtime 验证通过，也不代表 rollback-ready。
- 本阶段未修改 BuffSystem runtime core、`IBuffSystem`、production whitelist、validation whitelist、ECS、RollBackSystem、View、Scene、Prefab、Packages 或 xNode。

## Phase 3I-11M-Fix - Graph Effect 璋冪敤閾剧敓鎴愬畬鏁存€т慨澶?
- 淇 Graph 鐢熸垚 Effect 鑽夌鏃惰皟鐢ㄩ摼鎻愮ず涓嶆竻鏅扮殑闂銆?- 褰?`EffectNode` 鐢熷懡鍛ㄦ湡绔彛杩炴帴鏈夋晥 `ScriptActionNode` 鏃讹紝鐢熸垚鐨?Effect 浼氭槑纭寘鍚?`readonly` action 瀛楁鍜?`Execute(in context)` 璋冪敤銆?- 鏂板璋冪敤閾鹃瑙堬紝鏄剧ず `OnApply / OnTick / OnRemove / OnRefresh / OnStackChanged` 鍚勭敓鍛藉懆鏈熷搴旂殑 Action 椤哄簭銆?- 鏂板鐢熸垚瀹屾暣鎬ц鏁帮細`ExpectedActionCallCount`銆乣GeneratedActionFieldCount`銆乣GeneratedActionExecuteCallCount`锛岄伩鍏嶆湁 Action 浣嗙敓鎴愮┖妯℃澘鐨勬儏鍐点€?- `EmptyActionPlaceholderNode` 浼氫綔涓?warning 鏄剧ず锛屼笉浼氱敓鎴愬彲杩愯璋冪敤浠ｇ爜锛屽苟鎻愮ず鏇挎崲涓?`ScriptActionNode`銆?- 鏂囨。鍜屾彁绀烘槑纭尯鍒嗭細Effect 鐢熷懡鍛ㄦ湡璋冪敤閾剧敱宸ュ叿鐢熸垚锛涘叿浣撶帺娉曢€昏緫浠嶅湪 Action 鑴氭湰 `Execute(in context)` 涓疄鐜般€?- 鏈樁娈典笉鑷姩娉ㄥ唽 Effect锛屼笉淇敼 `BuffEffectRegistryBootstrap`锛屼笉鍔犲叆 whitelist锛屼笉淇敼 runtime core銆?
## Phase 3I-11M - Graph 涓€閿垱寤?Buff + 涓?Effect 鑽夌

- Authoring Hub 鐨勫浘褰㈠寲缂栬緫妯″紡鏂板 `鍥惧舰鍖栫敓鎴?/ Graph Generate` 鍖哄煙銆?- 鏂板涓変釜鍥惧舰鍖栫敓鎴愬叆鍙ｏ細
  - `浠庡浘鍒涘缓涓?Effect 鑽夌`
  - `浠庡浘鍒涘缓 Buff 鑽夌`
  - `浠庡浘涓€閿垱寤?Buff + 涓?Effect 鑽夌`
- 鏂板 Editor-only 鐢熸垚璁″垝涓庣粨鏋滄ā鍨嬶細
  - `BuffGraphGeneratePlan.cs`
  - `BuffGraphGenerateReport.cs`
  - `BuffGraphGenerateService.cs`
- 鐢熸垚娴佺▼浼氬厛鏋勫缓 Graph Generate Plan锛屽苟澶嶇敤鐜版湁 ID 鑷姩鍒嗛厤銆丟raph codegen preflight銆丒ffect preflight銆丅uff preflight銆?- 浠讳綍 Error 閮戒細闃绘鍐欏叆锛沇arning / Info 浼氭樉绀哄湪鐢熸垚璁″垝鎴栫敓鎴愮粨鏋滀腑銆?- 涓€閿祦绋嬫寜椤哄簭鍏堢敓鎴愪富 Effect `.cs` 鑽夌锛屽啀鍒涘缓 `BuffConfigData` 鑽夌 asset锛屽苟鍦ㄦ垚鍔熷悗鐢卞伐鍏峰唴閮ㄩ粦绠辨洿鏂?ID Registry銆?- 濡傛灉 Effect 鑽夌宸茬粡鐢熸垚浣?Buff 鑽夌鍒涘缓澶辫触锛屽伐鍏蜂笉浼氳嚜鍔ㄥ垹闄ゅ凡鐢熸垚鐨?Effect 鑽夌锛屼細鍦ㄧ粨鏋滀腑鎻愮ず鐢ㄦ埛鎵嬪姩妫€鏌ユ垨娓呯悊銆?- 涓?Effect 閫夋嫨瑙勫垯娌跨敤 Phase 3I-11L锛氬彧閫夋嫨 `ExecutionOrder` 鏈€灏忕殑 `EffectNode`锛涘 Effect 鍥惧彧鏄剧ず warning锛屼笉鐢熸垚 `CompositeEffect`銆?- 鏈樁娈典笉鑷姩娉ㄥ唽 Effect锛屼笉淇敼 `BuffEffectRegistryBootstrap`锛屼笉鍔犲叆 whitelist锛屼笉淇敼 compressed eligibility锛屼笉淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffSystemCore.cs`銆乣IBuffSystem`銆丒CS銆丷ollBackSystem銆乂iew銆丼cene銆丳refab銆亁Node package銆丳ackages manifest 鎴?`.meta`銆?
## Phase 3I-11L - Graph 鐢熸垚 Effect 鑽夌璋冪敤閾?
- Effect Template 鏀寔浠?`BuffCandidateGraph` 瀵煎叆 Effect 璋冪敤閾俱€?- 鐢熸垚 Effect 鑽夌鏃讹紝鍙牴鎹富 `EffectNode` 鐢熷懡鍛ㄦ湡绔彛鍜岀洿鎺ヨ繛鎺ョ殑 `ScriptActionNode` 鐢熸垚 `readonly` action 瀛楁涓?`Execute(in context)` 璋冪敤銆?- 璋冪敤閾炬寜 `ScriptActionNode.ExecutionOrder` 鍗囧簭鐢熸垚锛涘悓涓€鐢熷懡鍛ㄦ湡鍐呴噸澶?`ExecutionOrder` 浼氫綔涓?Graph Codegen Preflight error 闃绘鐢熸垚銆?- 绗竴鐗堝彧鐢熸垚鍗曚釜涓?Effect锛涘綋鍥句腑瀛樺湪澶氫釜 `EffectNode` 鏃讹紝閫夋嫨 `ExecutionOrder` 鏈€灏忕殑鑺傜偣骞舵樉绀?warning锛屼笉鐢熸垚 `CompositeEffect`銆?- `OnStackChanged` 绗竴鐗堜粛璋冪敤 `IBuffGraphAction.Execute(in context)`锛屼笉浼氬悜 Action 浼犻€?`delta`銆?- 鐢熸垚鍓嶄細鎵ц Effect Preflight 涓?Graph Codegen Preflight锛汫raph 閿欒浼氶樆姝㈢敓鎴?`.cs` 鑽夌銆?- 鏈樁娈垫湭鑷姩娉ㄥ唽 Effect锛屾湭淇敼 `BuffEffectRegistryBootstrap`锛屾湭鍔犲叆 whitelist锛屾湭淇敼 runtime core銆?- 鏈樁娈垫湭鍒涘缓 `BuffConfigData`锛屾湭淇敼 xNode package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11K - IBuffGraphAction runtime-safe 鏈€灏忔帴鍙?
- 鏂板 runtime-safe 鎺ュ彛 `IBuffGraphAction`锛屼緵鍚庣画鍥惧舰鍖?Effect 璋冪敤閾剧敓鎴愪娇鐢ㄣ€?- `IBuffGraphAction` 绗竴鐗堝彧鍖呭惈 `Execute(in BuffEffectContext context)`銆?- 绗竴鐗堜笉鏂板鐢熷懡鍛ㄦ湡缁嗗垎鎺ュ彛锛屼篃涓嶅鐞?`OnStackChanged` 鐨?`delta` 鍙傛暟銆?- `ScriptActionNode` 鏍￠獙鍗囩骇锛氳剼鏈繀椤诲疄鐜?`IBuffGraphAction` 鎵嶈兘琚涓烘湁鏁?Action銆?- 鏈疄鐜?`IBuffGraphAction` 鐨勮剼鏈細鍦?`ScriptActionNode` / Authoring Hub 鎽樿涓樉绀轰负 invalid銆?- `public parameterless constructor` 鏆傛椂浠嶄綔涓?warning锛屼笉浣滀负 blocking error銆?- 鎺ㄨ崘 Buff Graph Action 鑴氭湰鐩綍涓?`Assets/_Scripts/FrameWork/BuffSystem/Actions`锛屾湰闃舵涓嶈嚜鍔ㄥ垱寤鸿鐩綍銆?- 鏈樁娈典笉鐢熸垚 Effect 浠ｇ爜锛屼笉鍒涘缓 `BuffConfigData`锛屼笉娉ㄥ唽 Effect锛屼笉鍔犲叆 whitelist銆?- 鏈樁娈垫湭淇敼 `BuffSystemCore`銆乣BuffEffectRegistryBootstrap`銆亁Node package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11I - ScriptActionNode Editor-only 鍘熷瀷

- 鏂板 Editor-only `ScriptActionNode`锛屽彲鍦?`BuffCandidateGraph` 涓〃绀鸿剼鏈姛鑳借妭鐐广€?- `ScriptActionNode` 鏀寔鎷栧叆 `MonoScript`锛屽苟鑷姩璇诲彇鑴氭湰绫诲瀷銆佸～鍏?`ActionTypeName`銆乣ActionName` 鍜?`ActionDisplayName`銆?- 鏂板 Editor-only `BuffScriptActionNodeValidator`锛岀敤浜庢鏌ョ┖鑴氭湰銆乣GetClass()` 涓虹┖銆乤bstract銆佹硾鍨嬬被鍨嬪畾涔夈€乣MonoBehaviour` / `UnityEngine.Object` 娲剧敓銆佺被鍚?/ namespace銆佹棤 public 鏃犲弬鏋勯€犮€佹帹鑽愯矾寰勫拰绂佺敤 API 瀛楃涓层€?- `EffectNode` 鐢熷懡鍛ㄦ湡绔彛鍙互杩炴帴 `ScriptActionNode`锛岀敤浜庤〃杈剧敓鍛藉懆鏈熷埌鍔熻兘鑺傜偣鐨勮璁″叧绯汇€?- `BuffCandidateGraphEvaluation` 璇嗗埆 `ScriptActionNode`锛屽苟灏嗘棤鏁堣剼鏈€佸崰浣嶈妭鐐硅繛鎺ャ€侀噸澶?`ExecutionOrder` 绛変綔涓?Editor warning銆?- Authoring Hub 鍥惧舰鍖栨憳瑕佹樉绀?ScriptActionNode 鏁伴噺銆佹湁鏁?/ 鏃犳晥鏁伴噺銆亀arning 鏁伴噺銆丄ction 鎽樿鍜?Action 璀﹀憡銆?- 鏈樁娈典笉鏂板 runtime interface锛屼笉鐢熸垚 Effect 璋冪敤浠ｇ爜锛屼笉鍒涘缓 `BuffConfigData`锛屼笉淇敼 runtime / registry / whitelist銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap`銆亁Node package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11G - EffectNode 鐢熷懡鍛ㄦ湡绔彛涓?Buff 鍥惧 Effect 椤哄簭鍘熷瀷

- 鏂板 Editor-only `BuffRootNode`锛岀敤浜庡湪 `BuffCandidateGraph` 涓〃杈?Buff 鏍硅妭鐐逛笌 Effect 杩炴帴鍏ュ彛銆?- 鏂板 Editor-only `EffectNode`锛岀敤浜庤〃杈?`EffectId`銆丒ffect 鍚嶇О銆丒ffect 绫诲悕鍜?`ExecutionOrder`銆?- `EffectNode` 鏀寔 `OnApply`銆乣OnTick`銆乣OnRemove`銆乣OnRefresh`銆乣OnStackChanged` 鐢熷懡鍛ㄦ湡杈撳嚭绔彛銆?- 鏂板 Editor-only `EmptyActionPlaceholderNode`锛岀敤浜庤鐢熷懡鍛ㄦ湡绔彛鍏堣繛鎺ュ埌鍗犱綅鍔熻兘鑺傜偣銆?- `BuffCandidateGraphEvaluation` 璇嗗埆 `BuffRootNode` / `EffectNode`锛屽苟妫€鏌?Effect 鏁伴噺銆侀噸澶嶆墽琛岄『搴忋€丒ffectId / 绫诲悕鍜岀敓鍛藉懆鏈熻繛鎺ユ憳瑕併€?- 鏃?`EffectBindingNode` 鏆傛椂淇濈暀鍏煎锛汢ridge 浼樺厛璇诲彇鏂?`EffectNode`锛屾病鏈?`EffectNode` 鏃?fallback 鍒版棫鑺傜偣銆?- Authoring Hub 鍥惧舰鍖栨憳瑕佹樉绀?EffectNode 鏁伴噺銆佹墽琛岄『搴忋€佺敓鍛藉懆鏈熻繛鎺ユ憳瑕併€佸 Effect 鎻愮ず鍜屾棫鑺傜偣浣跨敤鐘舵€併€?- 鏈樁娈靛彧瀹炵幇鍥剧粨鏋勫拰 Editor UI锛屼笉鐢熸垚 Effect 浠ｇ爜锛屼笉鍒涘缓 BuffConfigData锛屼笉淇敼 runtime / registry / whitelist銆?- 鏈樁娈垫湭淇敼 registry bootstrap銆亁Node package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11F - 鍒涘缓 Buff / Effect 鍓嶇疆 Preflight 涓庨粦绠?Registry 鍐欏叆

- 鏂板 Editor-only Preflight 缁撴灉妯″瀷锛屽寘鍚?`Info`銆乣Warning`銆乣Fixup`銆乣Error` 鍥涚被 issue銆?- Create Buff 鍦ㄥ垱寤?BuffConfigData 鑽夌鍓嶈嚜鍔ㄨ繍琛?Buff Preflight锛岄敊璇細闃绘鍒涘缓锛屽彲淇椤逛細鑷姩琛ラ粯璁ゅ€笺€?- Effect Template 鍦ㄧ敓鎴?Effect `.cs` 鑽夌鍓嶈嚜鍔ㄨ繍琛?Effect Preflight锛岄敊璇細闃绘鐢熸垚銆?- Preflight 閫氳繃鍚庣洿鎺ュ垱寤?/ 鐢熸垚锛屼笉鍐嶅洜 warning 寮瑰嚭浜屾纭銆?- 鍒涘缓 BuffConfigData 鎴愬姛鍚庯紝宸ュ叿浼氬湪鍐呴儴鑷姩缁存姢 ID Registry JSON锛屽苟鍐欏叆 `Generated` 鐘舵€?Buff 鏉＄洰銆?- 鐢熸垚 Effect 妯℃澘鎴愬姛鍚庯紝宸ュ叿浼氬湪鍐呴儴鑷姩缁存姢 ID Registry JSON锛屽苟鍐欏叆 `Generated` 鐘舵€?Effect 鏉＄洰銆?- ID Registry 浠嶄綔涓洪粦绠辨満鍒讹紝涓嶅啀鏆撮湶鎵嬪姩棰勭暀 / 閲嶅缓鎿嶄綔銆?- Graph 瀵煎叆 Create Buff / Effect Template 鍚庯紝鏈€缁堝垱寤?/ 鐢熸垚浠嶈蛋鍚屼竴濂?Preflight銆?- 鏈樁娈垫湭鑷姩娉ㄥ唽 Effect锛屾湭鍔犲叆 whitelist锛屾湭淇敼 runtime銆?- 鏈樁娈垫湭淇敼 registry bootstrap銆亁Node package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11E-Fix2 - 鑷姩 ID 鍒嗛厤鏍￠獙 UX 鏀跺彛

- 璋冩暣 Buff / Effect ID 鏍￠獙鎻愮ず锛宍990000+` Debug / Smoke / Reserved 娈靛湪鏅€氬垱寤烘祦绋嬩腑浣滀负涓嶅彲鎺ュ彈 ID銆?- Create Buff 椤甸潰涓墜鍔ㄨ緭鍏ヤ繚鐣欐 ConfigId 鏃朵細鏄庣‘鎻愮ず鐐瑰嚮 `閲嶆柊鍒嗛厤 Buff ID`銆?- Effect Template 椤甸潰涓墜鍔ㄨ緭鍏ヤ繚鐣欐 EffectId 鏃朵細鏄庣‘鎻愮ず鐐瑰嚮 `閲嶆柊鍒嗛厤 Effect ID`銆?- 鑷姩鍒嗛厤鎺ㄨ崘 ID 澧炲姞鍏滃簳淇濇姢锛屼笉浼氳繑鍥?`990000+` 淇濈暀娈点€?- `EffectId` 鏈缃彁绀轰笌 ConfigId 鍚堟硶鎬ф彁绀烘媶鍒嗭紱Create Buff 椤甸潰涓?`EffectId=0` 鍙綔涓?warning锛屼笉褰卞搷 ConfigId 鍚堟硶鎬с€?- 鏈樁娈垫湭鍒涘缓 `BuffConfigData`锛屾湭鐢熸垚 Effect 浠ｇ爜锛屾湭淇敼 runtime / registry / whitelist / compressed eligibility銆?- 鏈樁娈垫湭淇敼 xNode package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11E-Fix - ID 鑷姩鍒嗛厤 UX 绠€鍖栦笌榛戠鍖?
- Settings 椤甸潰绉婚櫎澶嶆潅 ID Registry 鍐欏叆 / 鍒嗛厤鎿嶄綔锛屼笉鍐嶆毚闇插垱寤虹┖ Registry JSON銆侀噸寤?Registry銆佹墜鍔ㄩ鐣?Buff / Effect ID 鎸夐挳銆?- Settings 椤甸潰鏂板 `鑷姩鍒嗛厤 Buff / Effect ID` 寮€鍏筹紝榛樿寮€鍚€?- 鏂板 Editor-only `BuffAuthoringIdService.cs`锛岄泦涓彁渚涗笅涓€涓彲鐢?Buff ConfigId / EffectId 鎺ㄨ崘涓庡敮涓€鎬ф牎楠屻€?- Buff 鍒涘缓娴佺▼鏀寔鑷姩鍒嗛厤 ConfigId锛屽苟鎻愪緵 `閲嶆柊鍒嗛厤 Buff ID` 鎸夐挳銆?- Buff 鍒涘缓娴佺▼鍦ㄧ敤鎴锋墜鍔ㄤ慨鏀?ConfigId 鍚庢墽琛屽敮涓€鎬ф牎楠岋紝鍐茬獊鏃堕樆姝㈠垱寤恒€?- Effect 妯℃澘娴佺▼鏀寔鑷姩鍒嗛厤 EffectId锛屽苟鎻愪緵 `閲嶆柊鍒嗛厤 Effect ID` 鎸夐挳銆?- Effect 妯℃澘娴佺▼鍦ㄧ敤鎴锋墜鍔ㄤ慨鏀?EffectId 鍚庢墽琛屽敮涓€鎬ф牎楠岋紝鍐茬獊鏃堕樆姝㈢敓鎴愩€?- Graph 瀵煎叆 Create Buff / Effect Template 鏃讹紝濡傛灉 ID 缂哄け鎴栧啿绐佷笖鑷姩鍒嗛厤寮€鍚紝浼氭浛鎹负鍙敤 ID銆?- ID Registry Store / Allocator / Scanner 淇濈暀涓哄唴閮ㄦ湇鍔★紝Registry JSON 浠庣敤鎴疯瑙掕浆涓洪粦绠辨満鍒躲€?- 鏈樁娈垫湭鍒涘缓 `BuffConfigData`锛屾湭鐢熸垚 Effect 浠ｇ爜锛屾湭淇敼 runtime / registry / whitelist / compressed eligibility銆?- 鏈樁娈垫湭淇敼 xNode package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11E - ID Registry 鍐欏叆涓庤嚜鍔ㄥ垎閰?
- 鏂板 Editor-only `BuffAuthoringIdRegistryStore.cs`锛岃礋璐?ID Registry JSON 鐨勫畨鍏ㄨ鍙栥€佸垱寤恒€佸啓鍏ャ€佺埗鐩綍鍒涘缓鍜岃鐩栧墠澶囦唤銆?- 鏂板 Editor-only `BuffAuthoringIdRegistryAllocator.cs`锛岃礋璐ｆ帹鑽愬苟棰勭暀涓嬩竴涓?Buff ConfigId / EffectId銆?- ID Registry schema 澧炲姞 `status` 瀛楁锛岀敤浜庡尯鍒?`Reserved`銆乣Generated`銆乣Imported` 鍜?`Unknown`銆?- Authoring Hub 鐨?`Settings` 椤甸潰鏂板 `ID Registry 鍐欏叆 / 鍒嗛厤` 鍖哄煙銆?- 鏀寔鐢ㄦ埛鎵嬪姩鍒涘缓绌?Registry JSON銆?- 鏀寔鐢ㄦ埛鎵嬪姩浠庡綋鍓嶆壂鎻忕粨鏋滈噸寤?Registry JSON銆?- 鏀寔鐢ㄦ埛鎵嬪姩棰勭暀涓嬩竴涓?Buff ConfigId锛屽苟鍐欏叆 Registry JSON銆?- 鏀寔鐢ㄦ埛鎵嬪姩棰勭暀涓嬩竴涓?EffectId锛屽苟鍐欏叆 Registry JSON銆?- 鎺ㄨ崘 ID 榛樿浠?Buff `100001`銆丒ffect `200001` 寮€濮嬶紝骞惰烦杩囧凡鍗犵敤 ID 涓?`990000+` Debug / Smoke / Reserved 娈点€?- Registry 鍐欏叆鍓嶄細妫€鏌ヨ矾寰勫繀椤讳綅浜?`Assets/` 涓嬶紝涓斿凡鏈?JSON 蹇呴』鍙В鏋愶紱鏍煎紡閿欒鏃朵笉浼氳鐩栧師鏂囦欢銆?- 棰勭暀 ID 涓嶄細鍒涘缓 `BuffConfigData`锛屼笉浼氱敓鎴?Effect 浠ｇ爜锛屼笉浼氭敞鍐?Effect銆?- 鏈樁娈垫湭淇敼 runtime / registry / whitelist / compressed eligibility / xNode package / Packages manifest銆?- 鏈樁娈垫湭淇濆瓨 scene锛屾湭淇敼 prefab锛屾湭鎵嬪啓 `.meta`銆?
## Phase 3I-11D - ID Registry 璁捐涓庡彧璇绘牎楠?
- 鏂板 ID Registry JSON schema 瀵瑰簲鐨?Editor-only 鏁版嵁缁撴瀯銆?- 鏂板 ID 鍗犵敤鍙鎵弿鑳藉姏锛屽彲鎵弿 `BuffConfigData`銆丒ffect 鑴氭湰銆乣BuffEffectRegistryBootstrap` 鍜屽凡鏈?Registry JSON銆?- Authoring Hub 鐨?`Settings` 椤甸潰澧炲姞 `ID Registry 鍙鏍￠獙` 鍖哄煙銆?- 璇ュ尯鍩熸敮鎸?`鎵弿 ID 鍗犵敤` 鍜?`澶嶅埗鎵弿鎶ュ憡`锛屽苟鏄剧ず鎺ㄨ崘涓嬩竴涓?Buff ConfigId / EffectId銆佹壂鎻忔暟閲忋€丒rrors 涓?Warnings銆?- 鎺ㄨ崘 ID 榛樿浠?Buff `100001`銆丒ffect `200001` 寮€濮嬶紝骞惰烦杩囧凡鍗犵敤 ID 涓?`990000+` Debug / Smoke / Reserved 娈点€?- Registry JSON 涓嶅瓨鍦ㄦ椂鍙樉绀?warning锛屼笉鍒涘缓鏂囦欢銆?- 鏈樁娈典笉鍒涘缓 Registry JSON锛屼笉鍐欏叆 Registry JSON锛屼笉鑷姩鍒嗛厤 ID銆?- 鏈樁娈垫湭鍒涘缓 `BuffConfigData`锛屾湭鐢熸垚 Effect 浠ｇ爜锛屾湭淇敼 runtime / registry / whitelist / compressed eligibility銆?- 鏈樁娈垫湭淇敼 xNode package銆丳ackages manifest銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-11C - Authoring Hub 妯″紡閫夋嫨鍗°€佸垱寤哄浘鎸夐挳涓?Settings 闈㈡澘

- Authoring Hub 澧炲姞 `鏁板€肩紪杈慲銆乣鍥惧舰鍖栫紪杈慲 涓?`Settings` 涓変釜椤跺眰妯″紡銆?- `鏁板€肩紪杈慲 妯″紡淇濈暀鍘熸湁 `閰嶇疆妫€鏌ュ櫒`銆乣鍒涘缓 Buff`銆乣Effect 妯℃澘` 涓変釜宸ュ叿銆?- `鍥惧舰鍖栫紪杈慲 妯″紡涓繚鐣欏€欓€夊浘鑱斿姩鍖猴紝骞舵柊澧?`鍒涘缓鍥綻 鎸夐挳銆?- `鍒涘缓鍥綻 浼氬湪 Settings 閰嶇疆鐨勫浘榛樿鐩綍涓垱寤?`BuffCandidateGraph`锛岄粯璁ょ洰褰曚负 `Assets/_Scripts/FrameWork/BuffSystem/AuthoringGraphs`銆?- 鏂板 Editor-only Settings 瀛樺偍绫?`BuffAuthoringHubSettings.cs`锛屼娇鐢?`EditorPrefs` 淇濆瓨鏈満宸ュ叿璺緞鍋忓ソ銆?- Settings 椤甸潰鏀寔閰嶇疆鍥鹃粯璁ょ洰褰曘€丅uff 閰嶇疆鐩綍銆丒ffect 鑴氭湰鐩綍鍜?ID Registry JSON 璺緞銆?- 鏈樁娈典粎瀹炵幇 Editor 宸ュ叿 UI 涓庤矾寰勮缃紝涓嶅垱寤?`BuffConfigData`锛屼笉鐢熸垚 Effect 浠ｇ爜锛屼笉瀹炵幇 ID Registry锛屼笉瀹炵幇 Preflight銆?- 鏈樁娈垫湭淇敼 runtime / registry / whitelist / compressed eligibility / xNode package / Packages manifest銆?- 鏈樁娈垫湭淇濆瓨 scene锛屾湭淇敼 prefab锛屾湭鎵嬪啓 `.meta`銆?
## Phase 3I-10D - BuffCandidateGraph 涓?Authoring Hub 蹇嵎缂栬緫宸ヤ綔娴侀泦鎴?
- Authoring Hub 椤堕儴鏂板 `鍊欓€夊浘鑱斿姩 / Candidate Graph Link` 鍖哄煙锛屽彲閫夋嫨 `BuffCandidateGraph` 骞舵煡鐪嬪€欓€夋憳瑕併€?- 鏂板 Editor-only 妗ユ帴灞?`BuffCandidateGraphBridge.cs`锛岀敤浜庝粠鍊欓€夊浘鏋勫缓鎽樿銆丆reate Buff 鑽夌瀵煎叆鏁版嵁鍜?Effect Template 鑽夌瀵煎叆鏁版嵁銆?- `Create Buff` 椤甸潰鏀寔浠庡€欓€夊浘瀵煎叆鍩虹瀛楁锛屽苟瑙﹀彂鐜版湁鏍￠獙棰勮锛涜鎿嶄綔涓嶄細鑷姩鍒涘缓 `BuffConfigData`銆?- `Effect Template` 椤甸潰鏀寔浠庡€欓€夊浘瀵煎叆 Effect 瀛楁锛屽苟瑙﹀彂鐜版湁鏍￠獙棰勮锛涜鎿嶄綔涓嶄細鑷姩鐢熸垚 Effect `.cs`銆?- `Validator` 椤甸潰澧炲姞鍊欓€夊浘 ConfigId 涓庣湡瀹?`BuffConfigData` 鏄惁瀛樺湪鐨勫鐓ф彁绀猴紱Validator 浠嶅彧鎵弿鐪熷疄閰嶇疆璧勬簮銆?- 鏂板涓枃鏂囨。 `BuffSystem_xNodeAuthoringGraph.md`锛岃鏄?xNode 鍊欓€夊浘涓?Authoring Hub 鐨勫垎宸ャ€佹帹鑽愭祦绋嬪拰杈圭晫銆?- 鏇存柊 `BuffSystem_AuthoringGuide.md`锛屽鍔?xNode 鍊欓€夊浘宸ヤ綔娴佸叆鍙ｈ鏄庛€?- 鏈樁娈垫湭淇敼 runtime / registry / whitelist / compressed eligibility / xNode package / Packages manifest銆?- 鏈樁娈垫湭鍒涘缓 graph asset锛屾湭鍒涘缓 `BuffConfigData`锛屾湭鐢熸垚 Effect `.cs` 鏂囦欢锛屾湭淇濆瓨 scene銆?
## Phase 3I-10C-Polish - BuffCandidateGraph 鑺傜偣 UI 鍙鎬т慨澶?
- 鏂板 Editor-only 鑷畾涔?xNode 鑺傜偣缁樺埗鏂囦欢 `BuffCandidateNodeEditors.cs`銆?- 浣跨敤 xNode `NodeEditor.CustomNodeEditor`銆乣OnHeaderGUI`銆乣OnBodyGUI`銆乣GetWidth` 鍜?`NodeEditorGUILayout.PropertyField` 浼樺寲鑺傜偣鏄剧ず銆?- 涓?`BuffCandidateStartNode`銆乣BuffShapeNode`銆乣EffectBindingNode`銆乣CompressedEligibilityNode`銆乣RuntimeDependencyRiskNode`銆乣CandidateDecisionNode` 璁剧疆鏇村鐨勮妭鐐瑰搴︺€?- 灏嗚妭鐐瑰瓧娈垫樉绀烘敼涓烘洿鐭殑涓枃 / 涓嫳娣峰悎鏍囩锛屽噺灏戦暱瀛楁鍚嶉伄鎸°€?- 闀挎枃鏈瓧娈电户缁鐢ㄧ幇鏈?`TextArea` 搴忓垪鍖栨樉绀猴紝涓嶉噸鍛藉悕瀛楁锛屼笉鏀瑰彉 graph asset 濂戠害銆?- 鏈樁娈靛彧淇敼 Editor UI 鏄剧ず锛屼笉淇敼 `BuffCandidateGraph` 濂戠害銆乪valuation 閫昏緫銆乺untime銆乺egistry 鎴?whitelist銆?- 鏈樁娈垫湭鍒涘缓 graph asset锛屾湭鍒涘缓 `BuffConfigData`锛屾湭鐢熸垚 Effect `.cs` 鏂囦欢锛屾湭淇濆瓨 scene銆?
## Phase 3I-10C-Fix - BuffCandidateGraph 鍒涘缓鑿滃崟鍙鎬т慨澶?
### Changed

- 涓?`BuffCandidateGraph` 鐨?`CreateAssetMenu` 琛ュ厖 `order = 5100`锛岀敤浜庢彁楂?Unity Create 鑿滃崟鎺掑簭绋冲畾鎬с€?- 鏂板 Editor-only 鍏滃簳鑿滃崟锛?```text
Assets / Create / BuffSystem / Buff Candidate Graph
```
- 璇ヨ彍鍗曞彧鍦ㄧ敤鎴锋墜鍔ㄧ偣鍑绘椂鍒涘缓 xNode 鍊欓€夊鏌ュ浘锛屾湰闃舵鏈嚜鍔ㄥ垱寤?graph asset銆?- 淇濈暀 `BuffCandidateGraph` 浣滀负 Editor-only authoring / review 鍘熷瀷锛屼笉浣滀负 production config source銆?
### Scope confirmation

- 鏈樁娈垫湭鍒涘缓 `BuffCandidateGraph` asset銆?- 鏈樁娈垫湭鍒涘缓鎴栦慨鏀?`BuffConfigData` asset銆?- 鏈樁娈垫湭鐢熸垚 Effect `.cs` 鏂囦欢銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 registry銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭淇敼 xNode package銆丳ackages manifest銆丳rojectSettings銆乻cene銆乸refab 鎴?`.meta`銆?
## Phase 3I-10C - BuffCandidateGraph 鏈€灏?Editor-only 鍘熷瀷

### 鏂板

- 鏂板 xNode 鍊欓€夊鏌ュ浘鏈€灏忓師鍨嬩唬鐮侊紝鐩綍锛?
```text
Assets/_Scripts/FrameWork/BuffSystem/Editor/AuthoringGraphs
```

- 鏂板 `BuffCandidateGraph`锛岀敤浜庣湡瀹?gameplay Buff 杩涘叆 production whitelist 鍓嶇殑鍙鍖栧€欓€夊鏌ャ€?- 鏂板绗竴鐗堣妭鐐圭被鍨嬶細
  - `BuffCandidateStartNode`
  - `BuffShapeNode`
  - `EffectBindingNode`
  - `CompressedEligibilityNode`
  - `RuntimeDependencyRiskNode`
  - `CandidateDecisionNode`
- 鏂板鏈€灏?evaluation锛屼粎妫€鏌ヨ妭鐐规暟閲忓畬鏁存€с€?
### 杈圭晫

- `BuffCandidateGraph` 鍙敤浜?Editor authoring / review銆?- 鍥?asset 涓嶅簲鏀惧叆 `Assets/Resources/BuffSystem/Buff`銆?- 鍥句笉鍙備笌 runtime 鍔犺浇銆?- 鍥句笉鏄?production config source銆?- 鏈樁娈典笉鐢熸垚 `BuffConfigData`銆?- 鏈樁娈典笉鐢熸垚 Effect `.cs`銆?- 鏈樁娈典笉娉ㄥ唽 Effect銆?- 鏈樁娈典笉淇敼 registry銆?- 鏈樁娈典笉淇敼 whitelist銆?- 鏈樁娈典笉淇敼 BuffSystem runtime銆?- 鏈樁娈典笉淇敼 xNode package銆丳ackages manifest 鎴?ProjectSettings銆?- 鏈樁娈垫湭鍒涘缓 graph asset锛屾湭淇濆瓨 scene銆?
### 鍚庣画

- 鍚庣画鍙繘鍏?`Phase 3I-10D`锛屼负 `BuffCandidateGraph` 璁捐涓枃 Markdown 瀹℃煡鎶ュ憡瀵煎嚭銆?- 鍚庣画濡傞渶杩炴帴璺緞鏍￠獙锛屽簲鍦?evaluation 涓ˉ鍏?`Start -> Decision` 鐨勭鍙ｉ亶鍘嗛€昏緫銆?- `BuffCandidateGraph` 涓嶈兘鏇夸唬 `BuffAuthoringValidator`銆丷unner 鎴?Unity 鎵嬪姩楠岃瘉銆?
## Phase 3I-9C-Cleanup - Remove standalone Odin prototype entry

### Changed

- 鐢ㄦ埛纭涓嶉渶瑕佷繚鐣欑嫭绔?Odin Hub prototype銆?- 绉婚櫎 `Tools / BuffSystem / Authoring Hub Odin Prototype` 瀵瑰簲鐨?Editor-only prototype 鏂囦欢銆?- 绉婚櫎 `BuffAuthoringOdinHubWindow` 浠ュ強浠呮湇鍔¤鐙珛绐楀彛鐨?prototype page / view model銆?- 淇濈暀鍘?`Tools / BuffSystem / Authoring Hub` 浣滀负褰撳墠鍞竴涓诲叆鍙ｃ€?- Odin 鍚庣画鍙綔涓哄師 Authoring Hub 鍐呭眬閮ㄥ寮烘柟鍚戙€?
### Scope confirmation

- 鏈樁娈垫湭淇敼鍘?IMGUI Authoring Hub 閫昏緫銆?- 鏈樁娈垫湭淇敼 `BuffAuthoringHubWindow.cs`銆?- 鏈樁娈垫湭淇敼 `BuffAuthoringValidatorWindow.cs`銆?- 鏈樁娈垫湭淇敼 `BuffCreateWizardWindow.cs`銆?- 鏈樁娈垫湭淇敼 `EffectTemplateGeneratorPanel.cs`銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 registry銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭淇敼 `BuffConfigData.cs`銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇濆瓨 scene銆?- 鏈樁娈垫湭瀹夎銆佸崌绾ф垨鍒犻櫎 Odin / Sirenix 鎻掍欢銆?
## Phase 3I-9C-Fix - Validator layout readability fix

### Changed

- 淇 Authoring Hub 鐨?Validator 鎵弿缁撴灉涓暱瀛楁琚埅鏂殑闂銆?- `BuffType / TriggerType / ParallelStorageMode / EffectRegistered / CompressedEligibility / Category` 鏀逛负澶氳鍙瀛楁鏄剧ず銆?- 缁撴灉椤逛粠澶氫釜鐭垪妯悜鎸ゅ帇甯冨眬锛岃皟鏁翠负鍩虹淇℃伅銆佽涓洪厤缃€丒ffect / Eligibility 鍒嗗潡甯冨眬銆?- 闀垮瓧娈靛€间娇鐢ㄥ彲閫変腑鏂囨湰鏄剧ず锛岄伩鍏嶄腑鏂?label 鎸ゅ帇 value 鍖哄煙銆?
### Scope confirmation

- 鏈樁娈靛彧淇敼 Editor UI 鏄剧ず甯冨眬銆?- 鏈樁娈垫湭淇敼鎵弿閫昏緫銆?- 鏈樁娈垫湭淇敼 validation 閫昏緫銆?- 鏈樁娈垫湭淇敼鍒嗙被閫昏緫銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 registry銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇濆瓨 scene銆?- 鏈樁娈垫湭鏂板鐙珛 Odin Hub銆?- Odin 鍚庣画浠嶄綔涓哄師 Authoring Hub 鍐呭眬閮ㄥ寮烘柟鍚戙€?
## Phase 3I-9C - Odin Authoring Hub prototype

### Added

- 鏂板 Editor-only Odin 鍘熷瀷绐楀彛锛歚BuffAuthoringOdinHubWindow.cs`銆?- 鏂板鑿滃崟鍏ュ彛锛?
```text
Tools / BuffSystem / Authoring Hub Odin Prototype
```

- 鏃?IMGUI Authoring Hub 淇濈暀涓?fallback锛?
```text
Tools / BuffSystem / Authoring Hub
```

### Prototype pages

- Odin prototype 褰撳墠鍖呭惈锛?  - `閰嶇疆妫€鏌ュ櫒 / Validator`
  - `鍒涘缓 Buff / Create Buff`
  - `Effect 妯℃澘 / Effect Template`
- `Validator` page 浠呭仛鍙鎵弿锛屽鐢?`BuffAuthoringValidationUtility`锛屼笉淇敼 asset / whitelist / runtime銆?- `Create Buff` page 浠呭仛琛ㄥ崟鍜屾牎楠岄瑙堬紝涓嶅垱寤?`BuffConfigData` asset銆?- `Effect Template` page 浠呭仛琛ㄥ崟銆佹牎楠屽拰 registry snippet 澶嶅埗锛屼笉鐢熸垚 `.cs` 鏂囦欢銆?
### Scope confirmation

- 鏈樁娈垫湭淇敼鐜版湁 IMGUI Authoring Hub 鑿滃崟琛屼负銆?- 鏈樁娈垫湭淇敼 `BuffConfigData.cs`銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap.cs` 鎴?production registry銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇敼 scene / prefab / `.meta`銆?- 鏈樁娈垫湭瀹夎銆佸崌绾ф垨鍒犻櫎 Odin銆?
### Manual verification plan

- 鎵撳紑 `Tools / BuffSystem / Authoring Hub Odin Prototype`銆?- 鍦?`閰嶇疆妫€鏌ュ櫒 / Validator` 鎵ц `鎵弿 / 鍒锋柊`锛岀‘璁?`991001 Debug_CompressedParallel_TickSmoke` 琚瘑鍒负 smoke/debug锛屼笖 effect registered / compressed eligibility 鍧囦负 true銆?- 鍦?`鍒涘缓 Buff / Create Buff` 楠岃瘉榛樿鍊笺€乣ConfigId=991001` duplicate銆乣EffectId=990101` registered銆乣EffectId=0` warning銆乧ompressed eligibility 鍜?`Unlimited=true` warning銆?- 鍦?`Effect 妯℃澘 / Effect Template` 楠岃瘉 `EffectId=990101` 涓嶅彲鐢熸垚銆乣EffectId=100001 + PoisonTickEffect` 鍙€氳繃鏍￠獙锛屽苟纭 registry snippet 涓?`registry.Register(100001, new PoisonTickEffect());`銆?
## Phase 3I-9B - Authoring UI localization text foundation

### Added

- 鏂板 Editor-only 鏂囨闆嗕腑绠＄悊绫伙細`BuffAuthoringText.cs`銆?- `BuffAuthoringText` 褰撳墠闆嗕腑绠＄悊 Authoring Hub 鐨勪富瑕?UI 鏂囨锛屼緵鐜版湁 IMGUI 宸ュ叿鍜屾湭鏉?Odin 宸ュ叿澶嶇敤銆?
### Localized UI scope

- `BuffAuthoringHubWindow`锛?  - window title / header 鏀逛负 `Buff 鍒朵綔宸ュ叿 / Authoring Hub`銆?  - tabs 鏀逛负 `閰嶇疆妫€鏌ュ櫒 / 鍒涘缓 Buff / Effect 妯℃澘`銆?  - HelpBox 浣跨敤闆嗕腑涓枃鏂囨銆?- `BuffAuthoringValidatorWindow`锛?  - 鎵弿鎸夐挳銆佺粺璁￠」銆佸瓧娈靛悕銆侀棶棰樻爣棰樸€丆ategory 鏄剧ず鏂囨鏀逛负涓枃鎴栦腑鑻辨贩鍚堛€?  - `EffectRegistered / CompressedEligibility` 鐨勬樉绀虹粨鏋滄敼涓?`鏄?/ 鍚?/ 鏈煡`銆?- `BuffCreateWizardWindow`锛?  - 涓昏鍒嗙粍銆佸瓧娈点€佹寜閽€佹牎楠岄瑙堛€侀敊璇?/ 璀﹀憡 / 寤鸿鏍囬鏀逛负涓枃鎴栦腑鑻辨贩鍚堛€?  - Category 鏄剧ず澶嶇敤缁熶竴涓枃鏂囨銆?- `EffectTemplateGeneratorPanel`锛?  - 涓昏鍒嗙粍銆佸瓧娈点€佹寜閽€佹牎楠岄瑙堛€侀敊璇?/ 璀﹀憡 / 寤鸿鏍囬鏀逛负涓枃鎴栦腑鑻辨贩鍚堛€?  - 淇濈暀 callback 鏂规硶鍚嶃€乺egistry snippet 鍜岀敓鎴愮被缁撴瀯銆?
### Preserved technical terms

- 鏈樁娈典繚鐣欎互涓嬫妧鏈湳璇垨涓嫳娣峰悎鏄剧ず锛?  - `Buff`
  - `Effect`
  - `ConfigId`
  - `EffectId`
  - `Runtime`
  - `Whitelist`
  - `Registry`
  - `Tick`
  - `CompressedExpiryFrameList`
  - `EntityPerStack`

### Scope confirmation

- 鏈樁娈垫湭淇敼 validation 閫昏緫銆?- 鏈樁娈垫湭淇敼 asset 鍒涘缓閫昏緫銆?- 鏈樁娈垫湭淇敼 Effect 妯℃澘鐢熸垚閫昏緫銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap.cs` 鎴?production registry銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇敼 scene / prefab / `.meta`銆?- Odin 宸叉娴嬪瓨鍦紝浣嗘湰闃舵灏氭湭杩涜 Odin Hub 閲嶆瀯銆?
### Next

- 鍚庣画鍙繘鍏?`Phase 3I-9C锛歄din Authoring Hub prototype`銆?- Odin prototype 搴斾紭鍏堝鐢?`BuffAuthoringText` 鍜?`BuffAuthoringValidationUtility`锛屽苟缁х画淇濇寔 runtime 闆朵緷璧栥€?
## Phase 3I-8 - Authoring Toolkit first-loop closeout

### Completed first-loop capabilities

- BuffSystem Authoring Toolkit 绗竴杞棴鐜凡瀹屾垚銆?- 褰撳墠缁熶竴鍏ュ彛锛?
```text
Tools / BuffSystem / Authoring Hub
```

- 褰撳墠 Hub 宸插寘鍚細
  - `Validator`
  - `Create Buff`
  - `Effect Template`
- `BuffAuthoringValidationUtility` 宸插畬鎴愯交閲忔娊鍙栵紝骞舵帴鍏?`Validator / Create Buff / Effect Template`銆?- `BuffSystem_AuthoringGuide.md` 宸插畬鎴愶紝骞跺凡涓庡綋鍓?UI 瀛楁 / 鎸夐挳瀵归綈銆?- Phase 3I-7B 瀵圭収澶嶆牳缁撴灉鍏ㄩ儴 PASS锛?  - `Create Buff` 瀵圭収 PASS銆?  - `Effect Template` 瀵圭収 PASS銆?  - Changelog 澶嶆牳 PASS銆?
### Confirmed current state

- `Validator` 鍙瘑鍒?`991001 Debug_CompressedParallel_TickSmoke`銆?- `991001 Debug_CompressedParallel_TickSmoke` 褰撳墠浠嶆槸 smoke/debug pilot锛屼笉鏄寮?gameplay Buff銆?- `990101` 褰撳墠浠嶆槸 `DebugNoOpTickEffectId`銆?- production whitelist 鏈墿澶с€?- 褰撳墠鏃犵湡瀹?gameplay Buff 鍊欓€夎繘鍏?compressed whitelist銆?
### Authoring boundaries

- Authoring Toolkit 涓嶈嚜鍔ㄦ敞鍐?Effect銆?- Authoring Toolkit 涓嶈嚜鍔ㄤ慨鏀?`BuffEffectRegistryBootstrap`銆?- Authoring Toolkit 涓嶈嚜鍔ㄥ姞鍏?whitelist銆?- Authoring Toolkit 涓嶈嚜鍔ㄤ慨鏀?runtime銆?- Authoring Toolkit 涓嶈嚜鍔ㄤ繚瀛?scene銆?- Authoring Toolkit 涓嶈瘉鏄?rollback-ready銆?- `Validator` 鏄?authoring 杈呭姪锛屼笉鏄?runtime 瀹夊叏璇佹槑銆?- EffectId const 闈欐€佹壂鎻忓彧鏄緟鍔╂鏌ワ紝涓嶈兘瑕嗙洊鎵€鏈夊姩鎬佹敞鍐屾潵婧愩€?- 婊¤冻 compressed eligibility 涓嶇瓑浜庤繘鍏?production whitelist銆?
### UX / feature backlog

- [Backlog] `Create Buff` 澧炲姞鐪熸鐨?Reset / Clear 鎸夐挳銆?- [Backlog] `Create Buff` 鏀寔浠庣幇鏈?BuffConfigData clone draft銆?- [Backlog] `Create Buff` 鏀寔鑷姩寤鸿涓嬩竴涓彲鐢?ConfigId銆?- [Backlog] `Effect Template` 鏀寔 Event Effect 妯℃澘锛屼絾闇€瑕佷簨浠剁被鍨嬮€夋嫨鏈哄埗銆?- [Backlog] `Effect Template` 鏀寔鎵撳紑鐢熸垚鍚庣殑 `.cs` 鏂囦欢銆?- [Backlog] `Validator` 鏀寔瀵煎嚭鎵弿鎶ュ憡銆?- [Backlog] `Validator` 鏀寔鎸?Category / EffectRegistered / Eligibility 杩囨护銆?- [Backlog] Candidate workflow锛氱湡瀹?gameplay Buff 鍊欓€夊鏌ラ潰鏉挎垨 Runner銆?- [Backlog] 姝ｅ紡 ID 鍒嗘瑙勮寖寰呰礋璐ｄ汉纭銆?- [Backlog] Odin / UI Toolkit 浼樺寲鍙綔涓哄悗缁綋楠屽寮猴紝涓嶄綔涓哄綋鍓嶇‖渚濊禆銆?
### Next

- Phase 3I Authoring Toolkit 鍙殏鏃跺皝鐗堛€?- 涓嬩竴姝ヤ紭鍏堢瓑寰呯湡瀹?gameplay Buff 鍊欓€夋彁浜ゃ€?- 濡傛湁鍊欓€夛紝杩涘叆 `Phase 3H-8A / Production Candidate Review`銆?- 濡傜户缁伐鍏风嚎锛岃繘鍏?`Phase 3I-9 UX Backlog 瀹炵幇璁捐`銆?
### Scope confirmation

- 鏈樁娈靛彧淇敼 BuffSystem Changelog銆?- 鏈樁娈垫湭鏂板鐙珛 backlog 鏂囨。銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap.cs` 鎴?production registry銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭淇敼 Editor 宸ュ叿浠ｇ爜銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇敼 scene / prefab / `.meta`銆?
## Phase 3I-7A - AuthoringGuide UI field alignment

### Documentation alignment

- 淇 `BuffSystem_AuthoringGuide.md` 涓庡綋鍓?Editor UI 鐨勫瓧娈?/ 鎸夐挳瀵圭収銆?- 琛ュ叏 `Create Buff` 褰撳墠瀛楁銆侀粯璁ゅ€煎拰鎸夐挳锛?  - `ConfigId`
  - `Buff Name`
  - `Description`
  - `Save Path`
  - `Target Asset`
  - `BuffType`
  - `TriggerType`
  - `ParallelStorageMode`
  - `Unlimited`
  - `MaxStack`
  - `Duration`
  - `TickTime`
  - `StackUpPolicy`
  - `StackDownPolicy`
  - `EffectId`
  - `EffectRegistered`
  - `Validate`
  - `Create Draft Asset`
  - `Open Authoring Validator`
  - `Cancel / Close`
- 鏄庣‘褰撳墠 `Create Buff` 宸ュ叿娌℃湁閲嶇疆瀛楁鐨?`Clear` 鎸夐挳锛涘鏈潵闇€瑕侊紝搴斿彟寮€ UX 鏀硅繘闃舵銆?- 琛ュ叏 `Effect Template` 褰撳墠瀛楁銆侀粯璁ゅ€煎拰鎸夐挳锛?  - `EffectId`
  - `Effect Class Name`
  - `Effect Display Name / Note`
  - `Target Folder`
  - `Namespace`
  - `Target File`
  - callback selection
  - `Validate`
  - `Generate Template`
  - `Copy Registry Snippet`
  - `Open Effect Folder`
  - `Clear`
- 琛ュ厖 `Open Effect Folder` 杈圭晫锛氬彧鎵撳紑鐩爣鐩綍锛屼笉浠ｈ〃鐢熸垚妯℃澘锛屼笉娉ㄥ唽 Effect锛屼篃涓嶄慨鏀?`BuffEffectRegistryBootstrap`銆?
### Scope confirmation

- 鏈樁娈靛彧淇敼 BuffSystem 鏂囨。銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap.cs` 鎴?production registry銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭淇敼 Editor 宸ュ叿浠ｇ爜銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇敼 scene / prefab / `.meta`銆?
## Phase 3I-6 - Authoring Guide added

### Added documentation

- 鏂板 `BuffSystem_AuthoringGuide.md`锛岀敤浜庡綊妗?Buff / Effect authoring 宸ュ叿閾剧殑鎺ㄨ崘浣跨敤娴佺▼銆?- 鏂囨。瑕嗙洊浠ヤ笅鍐呭锛?  - `Tools / BuffSystem / Authoring Hub` 缁熶竴鍏ュ彛銆?  - `Validator / Create Buff / Effect Template` 涓変釜 tab 鐨勭敤閫斻€?  - 浠庨浂鍒朵綔 Buff 鐨勬帹鑽愭祦绋嬨€?  - Effect 妯℃澘鐢熸垚娴佺▼銆?  - 浜哄伐娉ㄥ唽 Effect 鐨勮竟鐣屻€?  - Create Buff 鍒涘缓 BuffConfigData 鑽夌鐨勬祦绋嬨€?  - Validator 妫€鏌ラ」銆?  - compressed whitelist 鍊欓€夋爣鍑嗐€?  - Effect 缂栧啓绾︽潫銆?  - ID 浣跨敤寤鸿銆?  - 宸ュ叿涓嶄細鑷姩鍋氫粈涔堛€?  - 甯歌閿欒涓庡鐞嗗缓璁€?  - 褰撳墠宸茬煡杈圭晫銆?  - 鏈€灏忕ず渚嬫祦绋嬨€?
### Authoring boundaries

- `Effect Template` 鍙敓鎴?Effect 鑽夌妯℃澘锛屼笉鑷姩娉ㄥ唽 Effect銆?- `Create Buff` 鍙垱寤?BuffConfigData 鑽夌锛屼笉鑷姩鍔犲叆 whitelist銆?- `Validator` 鏄?authoring 杈呭姪锛屼笉鏇夸唬 Runner / 鍦烘櫙楠岃瘉 / 浜哄伐瀹℃壒銆?- 婊¤冻 compressed eligibility 涓嶇瓑浜庤嚜鍔ㄨ繘鍏?production whitelist銆?- EventTrigger / Unlimited / 渚濊禆閫愬眰 runtime entity 鐨?Buff 褰撳墠涓嶈繘鍏?compressed whitelist銆?- `991001 Debug_CompressedParallel_TickSmoke` 浠嶆槸 smoke/debug pilot锛屼笉鏄寮?gameplay Buff銆?- 姝ｅ紡 ID 鍒嗘瑙勮寖浠嶅緟椤圭洰璐熻矗浜虹‘璁ゃ€?- BuffSystem 浠嶄笉鑳藉绉?rollback-ready銆?
### Scope confirmation

- 鏈樁娈靛彧淇敼 BuffSystem 鏂囨。銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap.cs` 鎴?production registry銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭淇敼 Editor 宸ュ叿浠ｇ爜銆?- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭淇敼 scene / prefab / `.meta`銆?
## Phase 3I-5B - BuffAuthoringValidationUtility closeout

### Implemented utility

- 宸叉柊澧?Editor-only shared validation utility锛歚BuffAuthoringValidationUtility.cs`銆?- 璇?utility 褰撳墠闆嗕腑澶嶇敤浠ヤ笅鍙 authoring 妫€鏌ヨ兘鍔涳細
  - `ScanBuffAssets`
  - `BuildConfigIdIndex`
  - `IsConfigIdDuplicate`
  - `CheckProductionEffectRegistered`
  - `IsEffectIdUsedByBuffConfigData`
  - `GetBuffConfigEffectHits`
  - `ComputeCompressedEligibility(BuffConfigData)`
  - `ComputeCompressedEligibility(fields)`
  - `IsDebugOrSmoke`
  - `ScanEffectIdConstants`
  - `MakeSafeFileName`
  - `NormalizeAssetPath`

### Integrated tools

- `BuffAuthoringValidationUtility` 宸叉帴鍏ヤ互涓?Editor 宸ュ叿锛?  - `BuffAuthoringValidatorWindow`
  - `BuffCreateWizardWindow`
  - `EffectTemplateGeneratorPanel`
- compressed eligibility Editor 妫€鏌ュ彛寰勪繚鎸佷笉鍙橈細

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
```

### Manual verification

- Unity Console 鎵嬪姩纭鏃?error銆?- `Validator` tab 浠嶈兘鎵弿鍒?`991001 Debug_CompressedParallel_TickSmoke`銆?- Validator 缁熻浠嶄负锛?
```text
Total=1
Eligible=0
Smoke=1
Invalid=0
```

- `991001 Debug_CompressedParallel_TickSmoke` 浠嶆樉绀猴細

```text
EffectRegistered=True
CompressedEligibility=True
Category=Smoke / Debug Only
```

- `Create Buff` tab 榛樿瀛楁姝ｅ父鏄剧ず锛?  - `ConfigId = 100001`
  - `Buff Name = NewBuff`
  - `Save Path = Assets/Resources/BuffSystem/Buff`
  - `Target Asset = Assets/Resources/BuffSystem/Buff/100001_NewBuff.asset`
  - `BuffType = Parallel`
  - `TriggerType = Tick`
  - `ParallelStorageMode = Entity Per Stack`
  - `Unlimited = false`
  - `MaxStack = 1`
  - `Duration = 1`
  - `TickTime = 1`
  - `StackUpPolicy = Append`
  - `StackDownPolicy = Remove Earliest`
  - `EffectId = 0`
  - `EffectRegistered = Unknown`
- `Effect Template` tab 姝ｅ父鏄剧ず锛?  - `EffectId = 0`
  - `Effect Class Name = NewBuffEffect`
  - `Target Folder = Assets/_Scripts/FrameWork/BuffSystem/Effects`
  - `Namespace = BuffSystem`
  - 榛樿 callbacks锛歚OnApply / OnTick / OnRemove` enabled锛宍OnRefresh / OnStackChanged` disabled銆?
### Scope confirmation

- 鏈樁娈垫湭鍒涘缓 Buff asset銆?- 鏈樁娈垫湭鐢熸垚 Effect 妯℃澘鏂囦欢銆?- 鏈樁娈垫湭鍒涘缓鎴栦慨鏀?`.meta`銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production registry 鎴?`BuffEffectRegistryBootstrap.cs`銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility runtime 閫昏緫銆?- 鏈樁娈垫湭淇敼 scene / prefab銆?
### Boundaries

- `BuffAuthoringValidationUtility` 浠呬负 Editor authoring 宸ュ叿鏈嶅姟銆?- `BuffAuthoringValidationUtility` 涓嶈繘鍏?runtime 璁捐銆?- `BuffAuthoringValidationUtility` 涓嶆浛浠?`BuffDefinition` 鎴?runtime validator銆?- EffectId const 闈欐€佹壂鎻忎粛鍙槸杈呭姪闈欐€佹鏌ワ紝涓嶄唬琛ㄨ鐩栨墍鏈夊姩鎬佹敞鍐屾潵婧愩€?- 鏈樁娈典笉璇佹槑 BuffSystem rollback-ready銆?
### Next

- `BuffAuthoringValidationUtility` 褰撳墠鍙涓鸿交閲忔娊鍙栧畬鎴愩€?- 涓嬩竴姝ュ缓璁繘鍏?`Phase 3I-6锛欰uthoring 宸ュ叿閾炬枃妗ｄ笌浣跨敤娴佺▼褰掓。`锛屾垨缁х画鍋?`Create Buff` / `Effect Template` 鐨勭粏椤逛綋楠岄獙璇併€?
## Phase 3I-4C - EffectTemplateGenerator closeout

### Implemented tool

- `Buff Authoring Hub -> Effect Template` tab 宸叉浛鎹?placeholder銆?- 宸叉柊澧?Editor-only 闈㈡澘 `EffectTemplateGeneratorPanel.cs`銆?- 闈㈡澘褰撳墠鏀寔锛?  - `EffectId`
  - `Effect Class Name`
  - `Effect Display Name / Note`
  - `Target Folder`
  - `Namespace`
  - callback 鍕鹃€?  - `Validate`
  - `Generate Template`
  - `Copy Registry Snippet`
  - `Open Effect Folder`
  - `Clear`

### Manual verification

- 宸茬‘璁?`EffectId = 990101` 浼氳璇嗗埆涓?production registry 宸叉敞鍐岋紝骞剁姝㈢敓鎴愰噸澶嶆ā鏉裤€?- 宸茬‘璁?`EffectId = 100001` + `PoisonTickEffect` 鍙€氳繃鏍￠獙銆?- 宸茬‘璁?`Copy Registry Snippet` 杈撳嚭鏍煎紡锛?
```csharp
registry.Register(100001, new PoisonTickEffect());
```

- 宸蹭复鏃剁敓鎴愬苟妫€鏌?`TempGeneratedEffect_DeleteMe.cs`銆?- 鐢熸垚妯℃澘鍖呭惈锛?  - 姝ｇ‘ class name
  - `internal const int EffectId`
  - `BuffEffectExecutorBase`
  - 宸查€?callbacks
- 宸插垹闄や复鏃?`.cs` 鏂囦欢銆?- 涓存椂 `.meta` 鏈敓鎴愶紝鏈€缁堜笉瀛樺湪銆?
### Template wording fix

- 宸蹭慨姝ｆā鏉挎敞閲婄鐢ㄨ瘝銆?- 鍚庣画鐢熸垚妯℃澘涓嶅啀鍖呭惈锛?  - `Time.time`
  - `Time.deltaTime`
  - `GameObject`
  - `MonoBehaviour`
- 鍘熸彁绀鸿涔変繚鐣欎负锛?  - 涓嶈浣跨敤 Unity 甯ф椂闂?API 浣滀负 Buff runtime 閫昏緫鏃堕棿銆?  - 涓嶈鐩存帴渚濊禆 View 鎴?Unity 瀵硅薄缁勪欢銆?  - Effect 搴斾紭鍏堝啓 ECS 鐘舵€併€?  - production 浣跨敤鍓嶄粛闇€鎵嬪姩娉ㄥ唽銆?
### Scope confirmation

- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 `BuffEffectRegistryBootstrap.cs` 鎴?production registry銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility銆?- 鏈樁娈垫湭淇敼 Buff asset銆?- 鏈樁娈垫湭淇敼 scene / prefab / `.meta`銆?- 鏈樁娈垫湭鍒涘缓鎴栦繚鐣欎复鏃舵ā鏉挎枃浠躲€?
### Boundaries

- `EffectTemplateGenerator` 鍙敓鎴愯崏绋挎ā鏉匡紝涓嶄唬琛?Effect 宸茶繘鍏?production registry銆?- `EffectTemplateGenerator` 涓嶈嚜鍔ㄤ慨鏀?`BuffEffectRegistryBootstrap`銆?- `EffectTemplateGenerator` 涓嶈嚜鍔ㄥ垱寤烘寮?gameplay Effect銆?- `EffectTemplateGenerator` 涓嶄慨鏀?whitelist銆?- `EffectTemplateGenerator` 涓嶈瘉鏄?rollback-ready銆?- 鐢熸垚鐨?Effect 浠嶅繀椤荤敱浜哄伐瀹炵幇閫昏緫锛屽苟鎵嬪姩鎻愪氦娉ㄥ唽瀹℃壒銆?- production 浣跨敤鍓嶄粛闇€杩愯 `BuffAuthoringValidator` 鍜岀浉鍏抽獙璇併€?
### Next

- `EffectTemplateGenerator` 褰撳墠鍙涓烘渶灏忓疄鐜板畬鎴愩€?- 涓嬩竴姝ュ彲杩涘叆 `Phase 3I-5锛欰uthoring 宸ュ叿閾句綋楠屾墦纾?/ shared validation utility 鎶藉彇`銆?
## Phase 3I-3C - Buff Authoring Hub manual verification closeout

### Implemented tool integration

- 宸叉柊澧炵粺涓€ Editor 宸ュ叿鍏ュ彛 `Tools / BuffSystem / Authoring Hub`銆?- Hub 褰撳墠鍖呭惈涓変釜 tab锛?  - `Validator`
  - `Create Buff`
  - `Effect Template`
- 鏃ц彍鍗曞叆鍙ｄ粛淇濈暀锛?  - `Tools / BuffSystem / Authoring Validator`
  - `Tools / BuffSystem / Buff Create Wizard`
- 鏃ц彍鍗曞叆鍙ｅ凡鏀逛负鎵撳紑 `Buff Authoring Hub` 骞惰烦杞埌瀵瑰簲 tab銆?
### Manual verification

- Unity Editor 涓凡纭 `Buff Authoring Hub` 绐楀彛鍙墦寮€銆?- `Validator / Create Buff / Effect Template` 涓変釜 tab 鍧囨樉绀烘甯搞€?- `Validator` tab 鐐瑰嚮 `Scan / Refresh` 鍚庯紝鎵弿缁撴灉绗﹀悎棰勬湡锛?  - `Total = 1`
  - `Eligible = 0`
  - `Smoke = 1`
  - `Invalid = 0`
- 褰撳墠鎵弿鍒?`991001 Debug_CompressedParallel_TickSmoke`銆?- `EffectRegistered = True`銆?- `CompressedEligibility = True`銆?- `Category = Smoke / Debug Only`銆?- `Create Buff` tab 鍙樉绀洪粯璁ゅ瓧娈碉細
  - `ConfigId = 100001`
  - `Buff Name = NewBuff`
  - `BuffType = parallel`
  - `TriggerType = Tick`
  - `ParallelStorageMode = EntityPerStack`
  - `Unlimited = false`
  - `MaxStack = 1`
  - `Duration = 1`
  - `TickTime = 1`
  - `EffectId = 0`
- `Effect Template` tab 褰撳墠浠呮樉绀?Phase 3I-4 placeholder锛屾病鏈夊疄鐜颁唬鐮佺敓鎴愩€?
### Pending manual checks

浠ヤ笅 `Create Buff` 缁嗛」灏氭湭鍦ㄦ湰娆?closeout 涓綊妗ｄ负宸查獙璇侊紝鍚庣画鍙繘鍏?`Phase 3I-3D` 鍗曠嫭琛ユ祴锛?
- `ConfigId = 991001` duplicate error銆?- `EffectId = 990101` registered validation銆?- `EffectId = 0` warning銆?- `CompressedExpiryFrameList` eligibility preview銆?- `Unlimited = true` warning銆?- `Open Authoring Validator` button switching back to `Validator` tab銆?
### Scope confirmation

- 鏈樁娈垫湭鍒涘缓浠讳綍 Buff asset銆?- 鏈樁娈垫湭淇敼 BuffSystem runtime銆?- 鏈樁娈垫湭淇敼 public API銆?- 鏈樁娈垫湭淇敼 production whitelist銆乿alidation whitelist 鎴?compressed eligibility銆?- 鏈樁娈垫湭淇敼宸叉湁 Buff asset銆?- `Effect Template` tab 浠嶅彧鏄崰浣嶏紝涓嶇敓鎴?Effect 浠ｇ爜銆佷笉娉ㄥ唽 Effect銆?
### Next

- 涓嬩竴姝ュ彲杩涘叆 `Phase 3I-4锛欵ffectTemplateGenerator 璁捐闃舵`銆?- 濡傛灉闇€瑕佽ˉ榻?Create Buff 缁嗛」楠屾敹锛屽彲鍏堣繘鍏?`Phase 3I-3D锛欳reate Buff validation 缁嗛」鎵嬪姩楠岃瘉`銆?
## Phase 3I-2A - BuffAuthoringValidator manual verification closeout

### Implemented tool

- 宸叉柊澧?Editor-only 宸ュ叿 `BuffAuthoringValidatorWindow.cs`銆?- 鑿滃崟鍏ュ彛涓?`Tools / BuffSystem / Authoring Validator`銆?- 榛樿鎵弿璺緞涓?`Assets/Resources/BuffSystem/Buff`銆?- 宸ュ叿鍙鎵弿 `BuffConfigData` asset锛屾樉绀哄叧閿瓧娈垫憳瑕併€丒ffect 娉ㄥ唽鐘舵€併€乧ompressed eligibility銆侀厤缃棶棰樺拰鍊欓€夊垎绫汇€?- 宸ュ叿涓嶄慨鏀?asset銆乺untime銆乸roduction whitelist銆乿alidation whitelist 鎴?compressed eligibility銆?
### Manual verification

- Unity Editor 涓墦寮€ `Tools / BuffSystem / Authoring Validator` 骞剁偣鍑?`Scan / Refresh` 鍚庯紝鎵弿缁撴灉绗﹀悎棰勬湡锛?  - `Total = 1`
  - `Eligible = 0`
  - `Smoke = 1`
  - `Invalid = 0`
- 褰撳墠鎵弿鍒?`991001 Debug_CompressedParallel_TickSmoke`銆?- `EffectRegistered = True`銆?- `CompressedEligibility = True`銆?- `Category = Smoke / Debug Only`銆?
### Conclusion

- `991001` 婊¤冻 compressed eligibility锛屼笖 `990101` Effect 宸叉敞鍐屻€?- `991001` 鏄?Debug / Smoke asset锛屼笉搴斾綔涓烘寮忕帺娉?Buff 鍊欓€夈€?- 褰撳墠娌℃湁鐪熷疄 production Buff candidate銆?- 褰撳墠涓嶆墿澶?production whitelist銆?- 褰撳墠涓嶆柊澧炴寮忕敓浜?Buff銆?
### Next

- 涓嬩竴姝ュ缓璁繘鍏?`Phase 3I-3锛欱uffCreateWizard 璁捐 / 鍚堝悓闃舵`銆?
## Phase 3H-8 - Production candidate intake plan closeout

### Current state

- 褰撳墠娌℃湁鐪熷疄鐢熶骇 Buff 鍊欓€夈€?- 褰撳墠 `Assets/Resources/BuffSystem/Buff` 涓嬪敮涓€ Resources production buff asset 鏄?`991001 Debug_CompressedParallel_TickSmoke`銆?- `991001` 宸插湪褰撳墠 View production path 涓綔涓?smoke pilot 鐢熸晥锛屼絾瀹冩槸 smoke/debug pilot锛屼笉鏄寮忕帺娉?Buff銆?- 褰撳墠 production whitelist 缁х画淇濇寔鍗曠偣 `991001`銆?- 褰撳墠涓嶆墿澶?production whitelist銆?- 褰撳墠涓嶅疄鐜?`BuffSystemProductionCandidateValidationRunner`銆?- 鍙湁璐熻矗浜?/ 绛栧垝鎻愪氦鐪熷疄 gameplay Buff 鍊欓€夊悗锛屾墠杩涘叆鍊欓€夊鏌ャ€?
### Candidate intake requirements

鐪熷疄 gameplay Buff 鍊欓€夎繘鍏?compressed production whitelist 鍓嶅繀椤绘弧瓒筹細

- `BuffType == parallel`
- `TriggerType == Tick`
- `ParallelStorageMode == CompressedExpiryFrameList`
- `Unlimited == false`
- `MaxStack <= CompressedParallelBuffLayerBuffer.Capacity`
- `EffectId` 宸叉敞鍐屽埌 production registry
- 涓嶄緷璧?EventTrigger compressed
- 涓嶄緷璧栭€愬眰 runtime entity
- 涓嶄緷璧?rollback-ready 缁撹

### Required validation before whitelist

鍊欓€夎繘鍏?whitelist 鍓嶅繀椤婚€氳繃锛?
- asset 瀛楁瀹℃煡
- effect 娉ㄥ唽瀹℃煡
- EntityPerStack vs Compressed 琛屼负涓€鑷存€ч獙璇?- Add / Tick / Remove / Expire 楠岃瘉
- TryGetBuff / GetBuffs 楠岃瘉
- Source 鍖归厤楠岃瘉
- Stack policy 楠岃瘉
- View production path 鎵嬪姩楠岃瘉
- 鎬ц兘瑙傚療
- 鍥為€€鏂规纭

### Rejection rules

- EventTrigger Buff 涓嶈繘鍏?compressed whitelist銆?- Unlimited Buff 涓嶈繘鍏?compressed whitelist銆?- `MaxStack` 瓒呰繃 `CompressedParallelBuffLayerBuffer.Capacity` 鐨?Buff 涓嶈繘鍏?compressed whitelist銆?- 闈?Tick Buff 涓嶈繘鍏?compressed whitelist銆?- 闈?parallel Buff 涓嶈繘鍏?compressed whitelist銆?- Effect 鏈敞鍐屻€佷緷璧栭€愬眰 runtime entity銆佷緷璧?View 灞傜洿鎺ユ灇涓?runtime entity銆佺己灏戣涓轰竴鑷存€ч獙璇佹垨缂哄皯鍥為€€鏂规鐨?Buff 涓嶈繘鍏?compressed whitelist銆?
### Boundary

- 褰撳墠浠嶄笉鑳藉绉?BuffSystem rollback-ready銆?- 褰撳墠浠嶄笉鑳藉绉版洿澶氱敓浜?Buff 鍙互杩涘叆 compressed whitelist銆?- 褰撳墠浠嶄笉鑳藉绉?`991001` 鏄寮忕帺娉?Buff銆?- 褰撳墠浠嶄笉鑳藉绉版墍鏈夌湡瀹炵敓浜у満鏅潎宸插畬鏁村洖褰掋€?- 鏈樁娈典笉淇敼 `BuffSystemCore.cs`銆丅uffSystem runtime銆乸ublic API銆乸roduction whitelist銆乿alidation whitelist銆乧ompressed eligibility銆丅uffConfigData asset銆丷unner銆乣SimulationInitializer.cs`銆丒CS Core銆丷ollBackSystem銆乂iewSpawnSystem銆丼cene銆丳refab 鎴?`.meta`銆?
### Closeout

- Compressed parallel production pilot 褰撳墠杩涘叆绋冲畾绛夊緟鐘舵€併€?- `991001` 鍗曠偣 smoke pilot 淇濇寔銆?- production whitelist 鏆備笉鎵╁ぇ銆?- 绛夊緟鐪熷疄 gameplay Buff 鍊欓€夋彁浜ゅ悗锛屽啀杩涘叆 Phase 3H-8A 鍊欓€夊鏌ャ€?
## Phase 3H-6C - View production smoke pilot closeout

### Validated

- `SimulationInitializer.cs` 宸插畬鎴愭渶灏?production composition path 鎺ュ叆锛屽綋鍓?View production path 浣跨敤 `BuffConfigDataLoader.Instance`銆乣BuffEffectRegistryBootstrap.RegisterProductionEffects(...)` 涓?`BuffSystemCore.CreateForProduction(...)`銆?- 鎺ュ叆鍚庝簲涓?BuffSystem Unity Editor 鎵嬪姩 Runner 鍧囦繚鎸?PASS锛?  - `BuffSystemPhase2AValidationRunner`
  - `BuffSystemCompressedParallelValidationRunner`
  - `BuffSystemRestoreHookValidationRunner`
  - `BuffSystemStorageBehaviorConsistencyRunner`
  - `BuffSystemStoragePerformanceRunner`
- `BuffConfigDataLoader` Root Path 涓?`BuffSystem/Buff`锛宭oader 鎴愬姛鍔犺浇 1 涓?Buff definition銆?- 褰撳墠鍔犺浇鍒扮殑 configId 涓?`991001`锛宍TryGetDefinition(991001) = true`銆?- `991001 Debug_CompressedParallel_TickSmoke` 鐨勫叧閿?definition 瀛楁宸茬‘璁わ細
  - `BuffType = parallel`
  - `TriggerType = Tick`
  - `ParallelStorageMode = CompressedExpiryFrameList`
  - `Unlimited = false`
  - `MaxStack = 3`
  - `DurationFrames = 120`
  - `TickIntervalFrames = 60`
  - `EffectId = 990101`
- `BuffEffectRegistryBootstrap` 娉ㄥ唽鐨?`990101 DebugNoOpTickEffect` 宸插湪 View production path 涓彲鐢紝`EffectRegistered = true`銆?- `991001` 鐨?eligibility銆乧ompressed gate銆乸roduction whitelist 鍧囬€氳繃锛?  - `Eligibility = true`
  - `CompressedGate = true`
  - `WhitelistHit = true`
  - `WhitelistConfigIds = 991001`
  - `ShouldUseCompressedParallelExpected = true`
- 鎵嬪姩 Add `991001` 骞?Tick 鍚庯紝View production path 鍒涘缓 compressed runtime锛?  - `CompressedRuntime count = 1`
  - `EntityPerStackRuntime count = 0`
  - `Compressed Path = PASS`
- public query 缁撴灉宸茬‘璁わ細
  - `TryGetBuff = true`
  - `GetBuffs count = 1`
  - `Current ConfigId View count = 1`
  - aggregate `BuffViewData` 鍙锛宍Stack = 1`锛宍RemainingFrames = 119`銆?
### Conclusion

- 褰撳墠鍙互瀹ｇО锛歚991001 Debug_CompressedParallel_TickSmoke` 宸插湪褰撳墠 View production path 涓綔涓?smoke pilot 鐢熸晥銆?- 褰撳墠鍙互瀹ｇО锛歚991001` 鍛戒腑 compressed production whitelist锛屽苟鍒涘缓 `CompressedRuntime = 1`銆乣EntityPerStackRuntime = 0`銆?- 褰撳墠鍙互瀹ｇО锛氭帴鍏ュ悗 BuffSystem 娴嬭瘯璺緞涓?View production smoke pilot 鍧囨湭鍙戠幇 BuffSystem runtime 鍥炲綊銆?
### Boundary

- 鏈樁娈典笉淇敼 `BuffSystemCore.cs`銆丅uffSystem runtime銆丷unner銆乸roduction whitelist銆乿alidation whitelist銆乧ompressed eligibility銆丒CS Core銆丷ollBackSystem銆丼cene銆丳refab銆乣.meta` 鎴?BuffConfigData asset銆?- 褰撳墠涓嶆墿澶?production whitelist銆?- 褰撳墠涓嶆柊澧炴寮忕敓浜?Buff銆?- `991001` 浠嶅彧浣滀负 production pilot smoke asset锛屼笉瑙嗕负姝ｅ紡鐜╂硶 Buff銆?- 褰撳墠浠嶄笉鑳藉绉?BuffSystem rollback-ready銆?- 褰撳墠浠嶄笉鑳藉绉版洿澶氱敓浜?Buff 鍙互杩涘叆 compressed whitelist銆?- 褰撳墠浠嶄笉鑳藉绉版墍鏈夌湡瀹炵敓浜у満鏅潎宸插洖褰掋€?
### Known non-blocking warning

- Console 涓瓨鍦?`[ViewSpawnSystem] Failed to spawn view. PrefabID = 1`銆?- 璇ラ棶棰樺綊绫讳负 `ViewSpawnSystem / Prefab 鏄犲皠闂`锛屾湰闃舵涓嶅鐞嗐€?- 璇?warning 涓嶅奖鍝嶆湰闃舵 BuffSystem compressed path 楠岃瘉缁撹锛屽洜涓?provider銆乨efinition銆乪ffect銆亀hitelist銆乥inding銆乺untime count 涓?public query 鍧囧凡楠岃瘉閫氳繃銆?
### Meta note

- `BuffSystem_Changelog.md.meta` 鏇惧嚭鐜?invalid GUID锛屽鑷?Unity 蹇界暐瀵瑰簲鏂囨。 asset銆?- 宸插垹闄?malformed `.meta`锛屽苟鐢?Unity 鍒锋柊鍚庨噸鏂扮敓鎴愶紱褰撳墠 Console 涓嶅啀鏄剧ず璇?invalid GUID 鎶ラ敊銆?- 璇ラ棶棰樺彧褰卞搷 BuffSystem 鏂囨。 asset 瀵煎叆锛屼笉褰卞搷 BuffSystem runtime銆丷unner銆乸roduction whitelist銆乧ompressed eligibility 鎴?`991001` View production smoke pilot 楠岃瘉缁撹銆?- 鏈樁娈典笉鎵嬪啓 GUID锛屼笉澶勭悊鍏朵粬 `.meta` 鏂囦欢銆?
## Phase 3H-5A - Storage performance validation closeout

### Validated

- Phase 3H-5A 宸插畬鎴愶紝浜斾釜 BuffSystem Unity Editor 鎵嬪姩 Runner 鍧?PASS锛?  - `BuffSystemPhase2AValidationRunner`锛歚========== Result: PASS ==========`
  - `BuffSystemCompressedParallelValidationRunner`锛歚========== Compressed Parallel Validation Result: PASS ==========`
  - `BuffSystemRestoreHookValidationRunner`锛歚========== Result: PASS ==========`
  - `BuffSystemStorageBehaviorConsistencyRunner`锛歚========== EntityPerStack vs Compressed Strategy Behavior Result: PASS ==========`
  - `BuffSystemStoragePerformanceRunner`锛歚========== EntityPerStack vs Compressed Performance Result: PASS ==========`
- `BuffSystemStoragePerformanceRunner` 鐨?PASS 鍙〃绀烘€ц兘娴嬮噺娴佺▼瀹屾垚锛屼笉琛ㄧず Compressed 蹇呴』鍦ㄦ墍鏈夊満鏅兘鏇村揩銆?
### Performance summary

- AddBuff + Tick 娑堣垂锛欳ompressed / EntityPerStack 鍊嶇巼鍒嗗埆涓?`0.658`銆乣0.489`銆乣0.823`銆?- Tick锛欳ompressed / EntityPerStack 鍊嶇巼鍒嗗埆涓?`0.607`銆乣0.615`銆乣0.673`銆?- RemoveEarliest / RemoveLatest / ClearAll 鍧囨樉绀?Compressed 鏇村揩锛涘吀鍨嬪€嶇巼鍖呮嫭 `0.393`銆乣0.505`銆乣0.636`銆?- TryGetBuff 鏀剁泭鏄庢樉锛屽吀鍨嬪€嶇巼鍖呮嫭 `0.213`銆乣0.360`銆乣0.773`銆?- GetBuffs(target) 鍦ㄥぇ瑙勬ā鍦烘櫙涓嬫敹鐩婃帴杩?0锛屼絾鏈鏄庢樉閫€鍖栵紱鍏稿瀷鍊嶇巼鍖呮嫭 `0.979`銆乣0.980`銆?- EventTrigger 閰嶇疆涓?CompressedParallel 鎸夎璁?fallback EntityPerStack锛汻aise 缁撴灉浠呯敤浜庣‘璁ゆ祴閲忔祦绋嬶紝涓嶄綔涓?compressed runtime 鎬ц兘鏀剁泭渚濇嵁銆?- 鏈疆鎬ц兘娴嬮噺鎶ュ憡鐨勬墍鏈夋祴閲忛」 `GCBytes = 0`銆?
### Conclusion

- BuffSystem 娴嬭瘯璺緞鍜?compressed parallel runtime 琛屼负绋冲畾銆?- CompressedParallel 鍦?Add / Tick / Remove / TryGetBuff 涓婃暣浣撶ǔ瀹氫紭浜?EntityPerStack銆?- 鏈疆娌℃湁鍙戠幇闇€瑕佷慨鏀?`BuffSystemCore` 鐨?runtime 闂銆?
### Boundary

- 鏈樁娈靛彧褰掓。楠岃瘉缁撴灉锛屼笉淇敼 runtime銆丷unner銆乸ublic API銆乣IBuffSystem`銆乧ompressed gate / whitelist / eligibility銆乤sset銆乻cene銆乸refab銆乣.meta`銆乂iew銆丒CS 鎴?RollBackSystem銆?- `SimulationInitializer.cs` 浠嶆湭鎺ュ叆 `BuffConfigDataLoader + BuffEffectRegistryBootstrap + BuffSystemCore.CreateForProduction(...)`锛涜鏂囦欢灞炰簬 View composition root锛屾湰闃舵鏈慨鏀癸紝闇€瑕佽礋璐ｄ汉鎵瑰噯鍚庡崟鐙鐞嗐€?- 鍥犳锛屼笉鑳藉洜涓烘湰娆?Runner PASS 瀹ｇО `991001` 宸插湪褰撳墠 View production path 涓敓鏁堬紝涔熶笉鑳藉绉板綋鍓?View production pilot 宸查獙璇侀€氳繃銆?
## Phase 3G-State-Reconcile-Fix-A - BuffConfigDataLoader default Resources path

### Changed

- `BuffConfigDataLoader` 榛樿 Resources Root Path 宸蹭粠 `_Scripts/FrameWork/BuffSystem/BuffConfigDataCollection` 鏀舵暃涓?`BuffSystem/Buff`銆?- 褰撳墠 production pilot asset `Debug_CompressedParallel_TickSmoke.asset` 浣嶄簬 `Assets/Resources/BuffSystem/Buff`锛宍Resources.LoadAll<BuffConfigData>("BuffSystem/Buff")` 鍙鐩栬榛樿璺緞銆?
### Boundary

- 鏈樁娈靛彧淇 BuffSystem 渚ч粯璁よ矾寰勶紝涓嶄慨鏀?View composition root銆丒CS銆丷ollBackSystem銆乻cene銆乸refab 鎴?`.meta`銆?- `SimulationInitializer.cs` 浠嶆湭鎺ュ叆 `BuffConfigDataLoader + BuffEffectRegistryBootstrap + BuffSystemCore.CreateForProduction(...)`锛涜鏂囦欢灞炰簬 View composition root锛屾湰闃舵鏈慨鏀癸紝闇€瑕佽礋璐ｄ汉鎵瑰噯鍚庡崟鐙鐞嗐€?- 鍥犳锛屼笉鑳藉洜涓烘湰娆￠粯璁よ矾寰勪慨澶嶅氨瀹ｇО `991001` 宸插湪褰撳墠 View production path 涓敓鏁堛€?- 鏈慨鏀?public API銆乸ublic constructor銆乣IBuffSystem`銆乧ompressed gate / whitelist / eligibility锛屼篃鏈柊澧?production Buff銆?
## Phase 3H-3C - Restore hook validation runner

### 鏂板

- 鏂板 `BuffSystemRestoreHookValidationRunner`锛屼綔涓?`BuffSystemCore.OnWorldRestored(World world)` 鐨?Unity Editor 鎵嬪姩楠岃瘉鍏ュ彛銆?
- Runner 楠岃瘉 EntityPerStack runtime 鍦?`OnWorldRestored` 鍓嶅悗 `TryGetBuff` / `GetBuffs` 鏌ヨ缁撴灉涓€鑷淬€?
- Runner 楠岃瘉 compressed runtime 鍦?`OnWorldRestored` 鍚庝粛鍙€氳繃 aggregate ViewData 鏌ヨ锛屼笖 `layerCount` 涓?`Stack` 涓€鑷淬€?
- Runner 楠岃瘉 EventTrigger Buff 鍦?`OnWorldRestored` 鍚庝粛鍙€氳繃 `Raise<TEvent>` 瑙﹀彂瀵瑰簲浜嬩欢 Effect銆?
- Runner 閫氳繃鎵嬪姩淇敼 runtime component 妯℃嫙 World restore 鍚庣殑缁勪欢鐪熺姸鎬佸彉鍖栵紝楠岃瘉 ViewCache 涓嶈繑鍥?stale data銆?
- Runner 楠岃瘉 `OnWorldRestored` 鏈韩涓嶄細瑙﹀彂 `OnApply` / `OnTick` / `OnRemove` / `OnEvent`銆?

### 淇濇寔涓嶅彉

- 鏈慨鏀?RollBackSystem銆丒CS銆丆ontracts銆乁tility銆丳oolSystem銆乸ublic API銆乣IBuffSystem`銆乧ompressed gate / whitelist銆乤sset銆乻cene銆乸refab 鍜?`.meta` 鏂囦欢銆?
- 鏈樁娈典笉鎺ュ叆 RollBackSystem锛屼笉瀹炵幇 snapshot restore锛屼笉瀹ｇО BuffSystem rollback-ready銆?

## Phase 3H-3B - Rollback restore transient cache hook

### 鏂板

- 鏂板 internal `BuffSystemCore.OnWorldRestored(World world)`锛岀敤浜庡悗缁?RollBackSystem 瀹屾垚 World restore 鍚庢暣鐞?BuffSystem 娲剧敓缂撳瓨銆?
- 璇?hook 鍙竻鐞?BuffSystem transient state锛屽苟浠庢仮澶嶅悗鐨?ECS World 閲嶆柊鎹曡幏 runtime entity銆侀噸寤?lookup銆?
- ECS Component 浠嶆槸鍞竴杩愯鏃剁湡鐘舵€併€傝 hook 涓?AddBuff銆佷笉 RemoveBuff銆佷笉 DestroyEntity銆佷笉 SetComponent 淇敼涓氬姟鐘舵€併€佷笉鎵ц鐢熷懡鍛ㄦ湡 Effect銆佷笉瑙﹀彂浜嬩欢銆佷笉鎵ц Tick銆?
- 璋冪敤鏂瑰繀椤讳繚璇佸閮?RollBackSystem 宸插湪绋冲畾甯ц竟鐣屽畬鎴?World restore銆?
- Entity ID / Version 鐨勭ǔ瀹氭€у繀椤荤敱澶栭儴 snapshot restore 瀹炵幇淇濊瘉銆?

### 娓呯悊 / 閲嶅缓

- 娓呯┖ command queue銆乴ifecycle effect queue銆乸ending remove 鐘舵€併€乺untime frame snapshot銆乧ompressed runtime frame snapshot銆佽姹備复鏃跺垪琛ㄣ€乪vent candidate銆乺untime lookup銆乧ompressed runtime lookup銆乂iewCache銆丒ventRuntimeIndex 鍜?frame guard銆?
- 浠庢仮澶嶅悗鐨?World 閲嶆柊鎹曡幏 `BuffRuntimeComponent` 涓?`CompressedParallelBuffRuntimeComponent` entity銆?
- 閲嶅缓 EntityPerStack 涓?compressed runtime lookup銆?
- 鏍囪 ViewCache 涓?EventRuntimeIndex dirty銆?

### 淇濇寔涓嶅彉

- 鏈慨鏀?RollBackSystem銆丒CS snapshot銆乣WorldRollbackAdapter`銆乣RollbackCoordinator`銆丏emo `WorldSnapshot`銆乸ublic API銆乣IBuffSystem`銆乧ompressed gate / whitelist銆乤sset銆乻cene銆乸refab 鍜?`.meta` 鏂囦欢銆?
- 鏈樁娈典笉瀹ｇО BuffSystem rollback-ready銆侱emo `WorldSnapshot` 浠嶄笉鑳戒綔涓?Buff runtime rollback-ready 渚濇嵁锛屽洜涓哄畠涓嶈兘淇濊瘉绋冲畾鐨?Entity ID / Version銆?

## Phase 3G-4J - Production compressed pilot validation closeout

### Validated

- `configId = 991001` (`Debug_CompressedParallel_TickSmoke`) now validates the production compressed path through the normal production composition root.
- After the Phase 3G-4I creation fix, the debug queue trace produced:
  - AfterAdd `AddQueueCount = 1`.
  - AfterTick1 `RuntimeCompressed = 1`.
  - AfterTick1 `LayerCount = 1`.
  - AfterTick1 `RemainingFrames = 120`.
  - AfterTick2 `RuntimeCompressed = 1`.
  - AfterTick2 `TryGetBuffFound = True`.
  - `GetBuffsCount = 1`.
  - `CurrentConfigViewCount = 1`.
  - current ConfigId `EntityPerStackRuntime count = 0`.
  - `Compressed Path = PASS`.
- Public query now sees one aggregate `BuffViewData` for the pilot after the capture frame.

### Remaining validation

- `stack = 3` aggregate ViewData validation.
- Explicit Remove validation.
- Natural Expire validation.
- Performance comparison.
- Rollback snapshot validation.

### Preserved

- No runtime code was changed in this closeout phase.
- Public APIs, public constructors, `IBuffSystem`, compressed gate / whitelist logic, EntityPerStack behavior, event Effect hot path, assets, scenes, prefabs, and `.meta` files were not modified.

## Phase 3G-4I - Compressed runtime creation layer write fix

### Fixed

- Fixed the production `WorldStates.Ticking` compressed runtime creation path where a newly added `CompressedParallelBuffRuntimeComponent` could be deferred through `StructuralChangeBuffer` as a zero-layer container.
- `ApplyCompressedParallelAdd` no longer depends on `TryGetComponent` reading back a component that was just added in the same ticking phase.
- New compressed runtimes are now initialized locally, appended with their requested layers, then written once through `World.SetComponent`.

### Preserved

- Public APIs, public constructors, `IBuffSystem`, compressed gate / whitelist logic, EntityPerStack behavior, event Effect hot path, assets, scenes, prefabs, and `.meta` files were not modified.

## Phase 3G-4C-Fix - Buff debug queued-command wording

### Changed

- Updated the original ECS Debugger `Buff 璋冭瘯` page so Add / Remove button logs are INFO queued messages instead of PASS validation records.
- Added `娣诲姞 Buff 骞?Tick 涓€甯, `娣诲姞 3 灞?Buff 骞?Tick 涓€甯, and `绉婚櫎 Buff 骞?Tick 涓€甯 convenience buttons.
- Added `娣诲姞 Buff 骞?Tick 涓ゅ抚` and `娣诲姞 3 灞?Buff 骞?Tick 涓ゅ抚` for the capture-after-command timing case.
- Tick-driven refresh now performs the compressed-path validation after the queued command has been consumed by `BuffSystemCore`.
- Split compressed runtime validation from ViewData visibility. A runtime state of `CompressedRuntime count == 1` and `EntityPerStack count == 0` is shown as compressed path `PASS`; if `TryGetBuff` is still false, the page reports that ViewData is waiting for the next capture frame instead of treating the whole check as failed.
- `娣诲姞 3 灞?Buff 骞?Tick 涓ゅ抚` is the recommended aggregate ViewData stack validation path for `Stack = 3`.
- Added a copyable plain-text debug snapshot area and `澶嶅埗鏃ュ織鍒板壀璐存澘` button using `EditorGUIUtility.systemCopyBuffer`.

### Preserved

- `BuffSystemCore`, public APIs, compressed gate / whitelist logic, runtime logic, assets, scenes, prefabs, and `.meta` files were not modified.
- The original `Window / ECSFrameWork / World Debugger` remains the main debug entry.

## Phase 3G-4C - ECS World Debugger Chinese layout pass

### Changed

- Returned the main debug entry to the original IMGUI `Window / ECSFrameWork / World Debugger`.
- Localized the original ECS debugger pages and toolbar with Chinese-first labels while preserving English technical terms.
- Kept all original pages and existing functionality: `鎬昏 Overview` / `瀹炰綋 Entities` / `绯荤粺 Systems` / `鍘熷瀷 ArcheTypes` / `缁勪欢浠撳簱 Stores` / `鍗曚緥 Singletons` / `涓栫晫浜嬩欢 Events` / `鍛戒护 Commands` / `Buff 璋冭瘯`.
- Disabled the previous Odin experiment window menu to avoid opening a reduced-function duplicate debugger.
- The original `Buff 璋冭瘯` page still keeps pilot `configId = 991001` as the default target and exposes Add / Remove / fixed-frame Tick / query refresh controls.

### Preserved

- `BuffSystemCore`, `IBuffSystem`, public constructors, compressed gate / whitelist logic, event Effect hot path, runtime Buff logic, assets, scenes, prefabs, and `.meta` files were not modified.
- The original `ECSWorldDebuggerWindow` remains the only recommended main entry.
- Debug Entity creation still uses the current `World.CreateEntity()`; no Unity `GameObject` is created as an ECS Entity.

### Manual validation focus

- Open `Window / ECSFrameWork / World Debugger`.
- Select the `Buff 璋冭瘯` page.
- For `configId = 991001`, compressed success means current ConfigId `CompressedRuntime count == 1` and current ConfigId `EntityPerStack count == 0`.

## Phase 3G-4B-Fix - ECS Debugger Buff debug page

### Changed

- Added a real `Buff 璋冭瘯` page to `ECSWorldDebuggerWindow` Pages.
- The page draws Chinese Buff debug controls in the ECS Debugger right-side content area.
- The page can create debug target/source ECS `Entity` values from the selected runtime `World`.
- The page queues Add / Remove through the production `BuffSystemCore`, steps fixed frames through the selected `SimulateRunner`, and shows aggregate query results.
- The page displays total/config counts for `CompressedParallelBuffRuntimeComponent` and `BuffRuntimeComponent`.

### Preserved

- `BuffSystemCore`, `IBuffSystem`, public constructors, compressed gate / whitelist logic, event Effect hot path, runtime Buff logic, assets, scenes, prefabs, and `.meta` files were not modified.
- The Odin Inspector and IMGUI helper panel in `LogicFrameDebugPanel` are kept as auxiliary fallback, but they are no longer considered the ECS Debugger integration point.
- The Buff debug page resolves the current production `BuffSystemCore` only inside the Editor window for debug use; no runtime public API was added.

### Manual validation focus

- Open `Window / ECSFrameWork / World Debugger`.
- Select the `Buff 璋冭瘯` page from the left Pages list.
- For `configId = 991001`, compressed success means current ConfigId `CompressedRuntime count == 1` and current ConfigId `EntityPerStack count == 0`.

## Phase 3G-4B - Odin Chinese Buff debug panel

### Changed

- Added Odin Inspector groups to `LogicFrameDebugPanel` for the `BuffSystem 鍘嬬缉 Buff 璋冭瘯闈㈡澘`.
- Added Chinese labels, buttons, read-only result fields, runtime type statistics, `GetBuffs(target)` table, and recent operation logs.
- Kept the existing IMGUI Buff debug panel as a runtime fallback.
- Extended `SimulationDebugProbe` to expose debug `GetBuffs(target)` rows for Odin TableList display.

### Preserved

- `BuffSystemCore`, `IBuffSystem`, public constructors, compressed gate / whitelist logic, event Effect hot path, runtime Buff logic, assets, scenes, prefabs, and `.meta` files were not modified.
- Debug Entity creation still uses the current `World.CreateEntity()` through `SimulationDebugProbe`; no GameObject is created as an ECS Entity.
- Source defaults to target, and Add / Remove / Query use the same target/source pair.

### Manual validation focus

- In Play Mode, select the object containing `LogicFrameDebugPanel` and use the Odin group `BuffSystem 鍘嬬缉 Buff 璋冭瘯闈㈡澘`.
- For `configId = 991001`, compressed success means current ConfigId `CompressedRuntime count == 1` and current ConfigId `EntityPerStack count == 0`.
- After adding three layers and ticking once, the aggregate ViewData should be found with `Stack = 3` and only one matching `GetBuffs(target)` row.

## Phase 3G-4A - BuffSystem debug panel pilot entry

### Changed

- Integrated a Buff / Compressed Buff debug area into the existing `LogicFrameDebugPanel`.
- Added View-layer debug helpers in `SimulationDebugProbe` for queuing Add / Remove commands, stepping fixed frames, reading aggregate `BuffViewData`, and counting runtime component types.
- The debug panel defaults to `configId = 991001` for `Debug_CompressedParallel_TickSmoke`.
- The panel displays compressed runtime count and EntityPerStack runtime count for the selected target/config pair.

### Preserved

- `BuffSystemCore`, `IBuffSystem`, public constructors, compressed gate / whitelist logic, event Effect hot path, runtime Buff logic, assets, scenes, prefabs, and `.meta` files were not modified.
- Debug controls use fixed-frame runner stepping; they do not use `Time.time` or `Time.deltaTime` as Buff runtime logic.
- The production pilot remains limited by the production whitelist and eligibility checks.

### Manual validation focus

- Add `991001` and advance one fixed frame: `ConfigCompressedRuntimeCount` should become `1`, while `ConfigEntityPerStackRuntimeCount` should stay `0`.
- Add `991001` x3 and advance one fixed frame: `TryGetBuff` / `GetBuffs` should expose one aggregate ViewData with `Stack = 3`.
- Tick until expiry or remove all stacks: `TryGetBuff` should become false and `GetBuffs` should no longer contain `991001`.

## Phase 3G-3D-A - Production factory and pilot whitelist skeleton

### Changed

- Added internal `BuffSystemCore.CreateForProduction(...)`.
- Added a production compressed parallel whitelist containing only `configId = 991001`.
- Updated `SimulationInitializer` to create `BuffSystemCore` through the production factory while still injecting `BuffConfigDataLoader` and `BuffEffectRegistry`.

### Preserved

- Public constructors still use `gate=false` and an empty whitelist.
- Validation factory behavior and runner whitelist remain unchanged.
- Non-whitelisted Buffs continue to use `EntityPerStack`.
- No `BuffConfigData asset`, Resources directory, `.asset`, `.unity`, `.prefab`, or `.meta` files were created or modified.
- `IBuffSystem`, public API, public constructor signatures, and event Effect hot path are unchanged.

### Pilot note

The reserved pilot Buff is `Debug_CompressedParallel_TickSmoke` with `configId = 991001` and `EffectId = 990101`. The project now uses the Resources path `Assets/Resources/BuffSystem/Buff/Debug_CompressedParallel_TickSmoke.asset`.

## Phase 3G-2F-C - SimulationInitializer loader guard

### Changed

- Added a `BuffConfigDataLoader.Instance` null guard in `SimulationInitializer` before creating `World`.
- Initialization now disables `SimulationInitializer` and returns when `TimeSimulator` or `BuffConfigDataLoader` is missing.

### Manual scene requirement

- `BuffConfigDataLoader` must still be explicitly mounted in the scene.
- Recommended placement: add `BuffConfigDataLoader` to the `Bootstrap` GameObject in `Assets/_Scenes/Scene.unity`.

### Preserved

- No `.unity`, prefab, `.meta`, or asset files were modified.
- No pilot `BuffConfigData asset` was created.
- No production whitelist entry was added.
- The compressed gate remains closed for production runtime.

## Phase 3G-2F-B - Production initializer dependency injection

### Changed

- `SimulationInitializer` now creates `BuffSystemCore` with explicit `BuffConfigDataLoader` and `BuffEffectRegistry` dependencies.
- `BuffConfigDataLoader` receives `_fixedDeltaTime` before `Init()`.
- `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)` is called before constructing `BuffSystemCore`.

### Preserved

- `BuffSystemCore`, `BuffConfigDataLoader`, `IBuffSystem`, public constructors, and public APIs are unchanged.
- No `BuffConfigData asset` or `BuffEffectCatalogData.asset` was created or modified.
- No production whitelist entry was added.
- The compressed gate remains closed for production runtime.
- Event Effect hot path is unchanged.

## Phase 3G-2F-A - Debug NoOp Tick Effect and bootstrap skeleton

### Added

- Added internal `DebugNoOpTickEffect` with `EffectId = 990101`.
- Added internal `BuffEffectRegistryBootstrap.RegisterProductionEffects(BuffEffectRegistry registry)`.
- The bootstrap registers `DebugNoOpTickEffect` through `BuffEffectRegistry.Register`.

### Preserved

- `BuffSystemCore`, `SimulationInitializer`, `IBuffSystem`, public constructors, and public APIs are unchanged.
- `BuffEffectCatalogData.asset` and all `BuffConfigData` assets are unchanged.
- No production whitelist entry was added.
- The compressed gate remains closed for production runtime.
- `DebugNoOpTickEffect` does not write gameplay state, does not use GameObject / MonoBehaviour / Time APIs, and does not implement event Effect interfaces.

### Note

Phase 3G-2F-B connected `RegisterProductionEffects` to `SimulationInitializer`, so production initialization now registers `DebugNoOpTickEffect`. No pilot `BuffConfigData asset`, production whitelist entry, or compressed gate enablement was added.

## Phase 3F-5A - Test-only compressed gate entry

### Added

- Added an internal validation-only factory for creating `BuffSystemCore` with compressed parallel runtime enabled.
- Added a private constructor used by public constructors and the validation factory.

### Preserved

- Public constructor signatures are unchanged.
- Public APIs and `IBuffSystem` are unchanged.
- The compressed feature gate remains closed for normal runtime construction.
- EntityPerStack remains the default runtime behavior.

## Phase 3F-3E - Compressed pending remove and destroy cleanup

### Added

- Added compressed runtime container pending remove helper.
- Compressed pending remove reuses `_pendingRemoveRuntimeSet` and `_pendingRemoveRuntimes`.
- Compressed pending remove uses `compressedRuntimeHandle` as the pending runtime handle.
- Compressed lookup is removed immediately on pending remove and defensively before destroy.

### Preserved

- The compressed feature gate still defaults to closed, so compressed runtime cleanup is unreachable by default.
- EntityPerStack pending remove and destroy semantics are unchanged.
- Compressed runtime container pending remove does not queue an additional lifecycle Remove Effect.
- Query and ViewCache remain read-only and do not trigger pending remove.
- Public APIs, event effects, `BuffEffectRequest`, and `BuffEffectContext` are unchanged.

## Phase 3F-3D - Compressed ViewCache gated block

### Added

- Added gate-protected compressed ViewData block inside `EnsureViewCache`.
- Added a compressed-only frame guard for dynamic compressed `RemainingFrames`.
- Compressed ViewData writes to `_viewByKey` and reuses existing `MergeViewData` when a key already exists.

### Preserved

- The compressed feature gate still defaults to closed, so compressed ViewData does not enter the cache by default.
- Public `TryGetBuff` and `GetBuffs` APIs are unchanged.
- Existing EntityPerStack `ToViewData` and `MergeViewData` semantics are unchanged.
- Tick, pending remove, destroy flows, public APIs, and event effects are unchanged.

### ViewData semantics

- ViewData forever uses `RemainingFrames = -1`.
- Duration compressed ViewData uses `Math.Max(0, layer.expireFrame - currentFrame)`.
- This ViewData convention is distinct from lifecycle Tick snapshot remaining-frame conventions.

### Warning

Compressed pending remove and destroy flows are still not connected. Do not enable the compressed feature gate until those phases are completed and validated.

## Phase 3F-3C - Compressed Tick / Expire gated entry

### Added

- Added independent `TickCompressedParallelRuntimes` entry after the existing EntityPerStack Tick path.
- The compressed entry iterates only `_compressedRuntimeEntitiesThisFrame`.
- The compressed entry is protected by `ShouldUseCompressedParallel`.

### Preserved

- The compressed feature gate still defaults to closed, so compressed Tick / Expire is unreachable by default.
- EntityPerStack `TickRuntimeBuffs` logic is unchanged.
- Query, ViewCache, pending remove, destroy flows, public APIs, and event effects are unchanged.

### Warning

Do not enable the compressed feature gate until compressed Query, ViewCache, pending remove, and destroy flows are connected and validated.

## Phase 3F-3B - Compressed query and lookup skeleton

### Added

- Added compressed runtime query and per-frame capture list for `CompressedParallelBuffRuntimeComponent`.
- Added compressed lookup rebuild for `_compressedRuntimeEntityByKey`.
- Added deterministic duplicate-key handling by smallest `Entity.ID -> Entity.Version`.

### Preserved

- The compressed feature gate still defaults to closed, so `CompressedExpiryFrameList` is not enabled.
- `_runtimeEntitiesByKey` remains EntityPerStack-only.
- Compressed lookup is a cache, not runtime truth.
- Tick, Expire, Query, ViewCache, pending remove, destroy flows, public APIs, and event effects are unchanged.

## Phase 3F-3A - Compressed Add / Remove gated branches

### Added

- Added gated compressed Add branch inside the parallel Add path.
- Added gated compressed Remove branch after resolving the command definition.

### Preserved

- The compressed feature gate still defaults to closed, so both branches are unreachable by default.
- Existing EntityPerStack Add / Remove behavior remains unchanged when the gate is false.
- Tick, Expire, Query, ViewCache, runtime lookup rebuild, pending remove, destroy flows, public APIs, and event effects are unchanged.

### Warning

Do not enable the compressed feature gate until the remaining runtime flows are connected and validated.

## Phase 3F-2 - Compressed feature gate skeleton

### Added

- Added a private compressed runtime feature gate field.
- Updated `ShouldUseCompressedParallel` to use `gate && IsCompressedParallelEligible(...)`.

### Preserved

- The gate defaults to closed, so `CompressedExpiryFrameList` is still not enabled.
- Add, Remove, Tick, Query, ViewCache, runtime lookup, pending remove, destroy flows, public APIs, and event effects are unchanged.
- EntityPerStack remains the only active runtime behavior.

## Phase 3E-2 - Compressed ViewData dormant helpers

### Added

- Added dormant compressed ViewData aggregation helpers for `CompressedExpiryFrameList`.
- Added helper rules that aggregate `stack`, `RemainingFrames`, and `RuntimeHandle` from active compressed layers.

### Preserved

- `ShouldUseCompressedParallel` still returns false, so `CompressedExpiryFrameList` is not enabled.
- `EnsureViewCache`, `TryGetBuff`, `GetBuffs`, `ToViewData`, `MergeViewData`, `BuffViewData`, and public APIs are unchanged.
- Current EntityPerStack Query and ViewData behavior is unchanged.

### ViewData aggregation rules

- Active compressed layers contribute to `Stack`.
- ViewData forever uses `RemainingFrames = -1`.
- Duration layers use `Math.Max(0, layer.expireFrame - currentFrame)`.
- Mixed duration and forever layers aggregate to `RemainingFrames = -1`, matching current `MergeViewData` compatibility behavior.
- The aggregate `RuntimeHandle` uses the minimum active `layerRuntimeHandle`, matching current `MergeViewData` semantics.

### Deferred

Compressed ViewData helpers are not connected to the query cache in this phase. A later phase must solve ViewCache frame invalidation or dirty marking before compressed Query is enabled.

## Phase 3D-2 - Compressed Tick / Expire dormant helpers

### Added

- Added dormant compressed Tick / Expire helpers for `CompressedExpiryFrameList`.
- Added fixed-frame compressed layer expiry comparison using `expired = currentFrame >= expireFrame`.
- Added dedicated Tick and Remove layer snapshot helpers for the future compressed runtime path.

### Preserved

- `ShouldUseCompressedParallel` still returns false, so `CompressedExpiryFrameList` is not enabled.
- `TickRuntimeBuffs` and current EntityPerStack Tick / Expire behavior are unchanged.
- `TryGetBuff`, `GetBuffs`, ViewData, event effects, `BuffEffectRequest`, `QueueLifecycleEffect`, `QueueRemoveRuntimeEntity`, and public APIs are unchanged.
- No `Time.time`, `Time.deltaTime`, or float expiry is used.

### Locked boundary

The compressed helpers follow the EntityPerStack boundary locked in Phase 3D-1.5:

```text
F1 create runtime -> F2 first Tick
same runtime frame: Tick before Expire
durationFrames = 1 -> F1 Apply, F2 Tick, F2 Remove
durationFrames = 2 -> F1 Apply, F2 Tick, F3 Tick, F3 Remove
```

Future compressed runtime enablement must keep:

```csharp
expireFrame = createFrame + durationFrames;
expired = currentFrame >= expireFrame;
tickSnapshot.remainingFrames = definition.IsForever
    ? 0
    : Math.Max(0, layer.expireFrame - currentFrame + 1);
removeSnapshot.remainingFrames = 0;
```

## Phase 3C-2 - Compressed Add / Refresh / Remove dormant helpers

### 鏂板

- 鏂板 `ApplyCompressedParallelAdd`銆乣ApplyCompressedParallelRemove`銆乣CreateCompressedParallelRuntime` 绛?compressed runtime dormant helper銆?
- 鏂板 compressed lookup helper锛歚TryGetCompressedRuntimeEntity`銆乣RegisterCompressedRuntimeLookup`銆乣RemoveCompressedRuntimeLookup`銆?
- 鏂板鍗曞眰 snapshot helper锛歚CreateCompressedLayerSnapshot`銆?
- 鏂板 Append銆丷efreshEarliest銆丷efreshAll銆丷eplaceEarliestWhenFull銆丷emoveEarliest銆丷emoveLatest銆丆learAll 瀵瑰簲鐨?compressed helper銆?

### 淇濇寔涓嶅彉

- `ShouldUseCompressedParallel` 浠嶈繑鍥?false锛宍CompressedExpiryFrameList` 涓嶄細瀹為檯鐢熸晥銆?
- 鏈慨鏀?`ApplyAddCommand` 鎴?`ApplyRemoveCommand`锛宑ompressed helper 涓嶄細琚幇鏈変富娴佺▼璋冪敤銆?
- 鏈帴鍏?Tick銆佽嚜鐒跺埌鏈?Expire銆乀ryGetBuff銆丟etBuffs 鎴?ViewData銆?
- 鏈墿灞?`BuffEffectRequest`锛屾湭淇敼 `BuffEffectContext` 鎴?public API銆?
- 褰撳墠 EntityPerStack 琛屼负涓嶅彉銆?

### Replace Effect 椤哄簭

鍘嬬缉 helper 涓?ReplaceEarliestWhenFull 鐨勭姸鎬佸彉鏇存寜绛栫暐鎵ц锛屼絾鐢熷懡鍛ㄦ湡 Effect Flush 浠嶇敱 Phase 2A phaseOrder 鍐冲畾銆傛枃妗ｅ拰娴嬭瘯涓嶅緱鍋囪鍚屽抚 Replace 涓?Remove Effect 涓€瀹氭棭浜?Apply Effect銆?

## Phase 3C-1 - Compressed parallel preparation helpers

### 鏂板

- `BuffSystemCore` 鏂板 `_compressedRuntimeEntityByKey` lookup cache 瀛楁锛屽苟鍙湪娓呯悊璺緞涓竻绌猴紱鏈樁娈典笉鍦ㄤ富娴佺▼璇诲啓瀹冦€?
- 鏂板 `ShouldUseCompressedParallel`锛屾湰闃舵淇濇寔杩斿洖 false锛岀‘淇?`CompressedExpiryFrameList` 涓嶄細瀹為檯鐢熸晥銆?
- 鏂板 `IsCompressedParallelEligible`锛岃鍒欎负 parallel buff銆乣CompressedExpiryFrameList`銆乀ick 瑙﹀彂銆侀潪 Unlimited銆乣MaxStack <= CompressedParallelBuffLayerBuffer.Capacity`銆?
- `CompressedParallelBuffLayerBuffer` 鏂板 `RemoveAt`銆乣FindEarliestIndex`銆乣FindLatestIndex`銆乣FindExpiredEarliestIndex`銆乣AppendLayer`銆乣RefreshLayer` helper銆?

### 淇濇寔涓嶅彉

- 涓嶆帴鍏?Add銆丷efresh銆丷emove銆乀ick銆丵uery 鎴?EffectRequest 涓绘祦绋嬨€?
- 涓嶆墿灞?`BuffEffectRequest` 鎴?`BuffEffectContext`銆?
- 涓嶄慨鏀?public BuffSystem API銆乣IBuffEffectExecutor` 鎴?`IBuffEventEffectExecutor<TEvent>`銆?
- 褰撳墠 EntityPerStack 琛屼负涓嶅彉銆?

### Tick / Expire 鍩哄噯

褰撳墠 EntityPerStack 鐨?`TickRuntimeBuffs` 椤哄簭鏄厛 Tick锛屽啀 Expire锛氬厛鎺ㄨ繘 `elapsedFrames` 骞跺湪婊¤冻闂撮殧鏃?Queue `OnTick`锛岄殢鍚庨潪姘镐箙 Buff 鎵嶆墸鍑?`remainingFrames` 骞跺鐞嗚嚜鐒跺埌鏈熴€傚悗缁帇缂╂ā寮忔寮忔帴鍏ユ椂蹇呴』瀵归綈璇ラ『搴忋€?

## Phase 3B - Parallel Buff compressed storage skeleton

### 鏂板

- 鏂板 `ParallelBuffStorageMode.EntityPerStack = 0`銆?
- 鏂板 `ParallelBuffStorageMode.CompressedExpiryFrameList = 1`銆?
- `BuffConfigData` 鏂板骞惰 Buff 瀛樺偍妯″紡閰嶇疆瀛楁锛岄粯璁ゅ€间负 `EntityPerStack`銆?
- `BuffDefinition` 鏂板 `ParallelStorageMode` 鍙瀛楁锛屽苟閫氳繃鏋勯€犲嚱鏁板熬閮ㄥ彲閫夊弬鏁颁繚鎸佹棫璋冪敤鍏煎銆?
- 鏂板 `CompressedParallelBuffLayer`銆乣CompressedParallelBuffRuntimeComponent` 鍜屽浐瀹氬閲忓€肩被鍨?`CompressedParallelBuffLayerBuffer`銆?

### 淇濇寔涓嶅彉

- Phase 3B 涓嶄慨鏀?`BuffSystemCore.cs`銆?
- Phase 3B 涓嶆帴鍏?Add銆丷efresh銆丷emove銆乀ick銆丒xpire銆乀ryGetBuff 鎴?GetBuffs 涓绘祦绋嬨€?
- 褰撳墠鎵€鏈夊苟琛?Buff 浠嶈蛋 EntityPerStack銆?
- 鍗充娇閰嶇疆閫夋嫨 `CompressedExpiryFrameList`锛屽綋鍓嶈繍琛屾椂涔熶笉浼氬惎鐢ㄥ帇缂╅€昏緫銆?
- Phase 2A 鐢熷懡鍛ㄦ湡 EffectRequest Pipeline 鍜屼簨浠跺瀷 Effect 鐑矾寰勪笉鍙樸€?
- 涓嶄娇鐢?`Time.time`銆乣Time.deltaTime`銆乣float expiry`銆丟ameObject runtime銆丮onoBehaviour runtime 鎴?runtime ScriptableObject Effect銆?

### 鍚庣画

Phase 3C 鎵嶄細鍗曠嫭璁捐 `CompressedExpiryFrameList` 濡備綍鎺ュ叆 Add銆丷efresh銆丷emove銆丒xpire銆丵uery 涓庣敓鍛藉懆鏈?EffectRequest Pipeline銆?

## Phase 2A - Lifecycle EffectRequest Pipeline

### 鏂板

- 鐢熷懡鍛ㄦ湡 Effect 璇锋眰闃熷垪锛岃鐩?`Apply / Refresh / StackChanged / Tick / Remove`銆?
- Remove 寤惰繜鐗╃悊閿€姣侊細Runtime 绔嬪嵆閫€鍑烘湁鏁?Buff 璇箟锛宍OnRemove` Flush 鍚庡啀 `DestroyEntity`銆?
- 鏄惧紡鐢熷懡鍛ㄦ湡 phase order锛歚Apply=0, Refresh=1, StackChanged=2, Tick=3, Remove=4`銆?

### 琛屼负鍙樺寲

鐢熷懡鍛ㄦ湡 Effect 鐢辩珛鍗虫墽琛屾敼涓烘湰甯ф湯灏?Flush銆傛帓搴忚鍒欑粺涓€涓猴細

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

Flush 鏈熼棿鏂板鐨?`AddBuff` / `RemoveBuff` 涓嶉€掑綊澶勭悊锛屼細杩涘叆 `_queuedCommands`锛岀敱涓嬩竴娆?`BuffSystemCore.Tick -> ConsumeQueuedCommands` 娑堣垂銆?

### 淇濇寔涓嶅彉

- `IBuffEffectExecutor` public API 涓嶅彉銆?
- `BuffEffectContext` public API 涓嶅彉銆?
- `IBuffEventEffectExecutor<TEvent>` 娉涘瀷浜嬩欢鐑矾寰勪笉鍙樸€?
- 涓嶅紩鍏?`GameObject`銆乣MonoBehaviour`銆乣Time.time`銆乣Time.deltaTime` 鎴?runtime `ScriptableObject Effect`銆?

## Phase 1.1 - Documentation strictness

### 鍙樻洿褰卞搷绀轰緥

`ResetDurationOnly` 鐢ㄤ簬閲嶅娣诲姞鏃跺彧鍒锋柊鎸佺画鏃堕棿锛屼笉鏀瑰彉褰撳墠灞傛暟銆備笅闈㈢殑绀轰緥涓紝鐩爣宸叉湁 2 灞?Buff锛屽啀娆℃坊鍔?1 灞傚悗浠嶄繚鎸?2 灞傦紝浣嗘寔缁抚涓?Tick 璁℃暟浼氶噸缃€?

```csharp
// before: stack = 2, remainingFrames = 40, elapsedFrames = 20, ticks = 1
definition.NormalStackPolicy = NormalBuffStackPolicy.ResetDurationOnly;
buffSystem.AddBuff(new AddBuffCommand(target, configId: 1001, source, stack: 1));

// after: stack = 2, remainingFrames = definition.DurationFrames,
//        elapsedFrames = 0, ticks = 0
```

`RefreshDuration` 淇濈暀鏃х殑鍔犲眰璇箟銆傞噸澶嶆坊鍔犳椂浠嶄細鎸夋棫瑙勫垯灏濊瘯澧炲姞灞傛暟锛屼絾鍒锋柊鎸佺画鏃堕棿鍚庝細鍚屾閲嶇疆 Tick 璁℃暟锛岄伩鍏嶅懆鏈熸晥鏋滄部鐢ㄥ埛鏂板墠鐨勮鏃剁姸鎬併€?

```csharp
// before: stack = 1, elapsedFrames = 29, ticks = 0
definition.NormalStackPolicy = NormalBuffStackPolicy.RefreshDuration;
definition.TickIntervalFrames = 30;
buffSystem.AddBuff(new AddBuffCommand(target, configId: 1002, source, stack: 1));

// after: stack = ClampStack(2), remainingFrames = definition.DurationFrames,
//        elapsedFrames = 0, ticks = 0
```

鏅€?Buff 鐨勯儴鍒嗗噺灞傝涓烘湰闃舵鏆傛湭鍙樻洿銆傚綋鍓?`RemoveBuffCommand` 鍙Щ闄ら儴鍒嗗眰鏁版椂锛屼粛浼氫繚鐣欐棦鏈夎涓猴細鍑忓皯 stack 鍚庡皢 `remainingFrames` 鍒锋柊涓哄綋鍓?`durationFrames`銆傚鏋滃悗缁鏀规垚鈥滃噺灞備笉鍒锋柊鍓╀綑鏃堕棿鈥濓紝闇€瑕佸崟鐙鏍搞€?

## Phase 1 - Low-risk semantic fixes

### 鏂板

- 鏂板 `NormalBuffStackPolicy.ResetDurationOnly = 5`銆?
- 鏂板鏍囧噯鏂囨。闆嗗悎锛岀敤浜庤褰?API銆佸彔灞傜瓥鐣ャ€丒ffect銆佷簨浠躲€佸苟琛?Buff銆佽縼绉昏鏄庛€佹牱渚嬪拰鍙樻洿鍘嗗彶銆?

### 琛屼负鍙樺寲

- `ResetDurationOnly` 閲嶅娣诲姞鏃朵笉鏀瑰彉褰撳墠灞傛暟锛屽彧閲嶇疆鎸佺画鏃堕棿涓?Tick 璁℃暟銆?
- `RefreshDuration` 鍒锋柊鎸佺画鏃堕棿鏃讹紝鐜板湪鍚屾閲嶇疆 `elapsedFrames` 鍜?`ticks`銆?
- `AddStackAndRefreshDuration` 鍒锋柊鎸佺画鏃堕棿鏃讹紝鐜板湪鍚屾閲嶇疆 `elapsedFrames` 鍜?`ticks`銆?
- 骞惰 Buff 鐨?`RefreshEarliest` 鍜?`RefreshAll` 鍒锋柊灞傛寔缁椂闂存椂锛岀幇鍦ㄥ悓姝ラ噸缃灞?`elapsedFrames` 鍜?`ticks`銆?

### 淇濇寔涓嶅彉

- 鏃ф灇涓惧€奸『搴忓拰鏁存暟鍊间繚鎸佷笉鍙樸€?
- `RefreshDuration` 鏄惁鍔犲眰鐨勬棫璇箟淇濇寔涓嶅彉銆?
- `RemoveBuffCommand` 鐨勬櫘閫?Buff 閮ㄥ垎鍑忓眰璇箟淇濇寔涓嶅彉銆?
- ViewCache dirty 琛屼负淇濇寔榛樿瀹夊叏璺緞锛涙湰闃舵鍙负 `WriteRuntimeComponent` 棰勭暀 `markViewDirty` 鍙傛暟锛岀幇鏈夎皟鐢ㄩ粯璁や粛鏍囪 dirty銆?

### 绂佹椤硅嚜鏌?

- 鏈紩鍏?`GameObject` 杩愯鏃朵緷璧栥€?
- 鏈紩鍏?`MonoBehaviour` 杩愯鏃朵緷璧栥€?
- 鏈紩鍏?`Time.time` 鎴?`Time.deltaTime`銆?
- 鏈紩鍏?`ScriptableObject` runtime effect銆?

### 杩佺Щ璇存槑

FrameWork2 鐨?`ResetRuntimeBuffStackUpStrategy` 杩佺Щ涓虹涓€濂?ECS BuffSystem 鐨?`NormalBuffStackPolicy.ResetDurationOnly`銆傝縼绉诲悗浣跨敤鍥哄畾甯у瓧娈?`elapsedFrames`銆乣ticks` 鍜?`remainingFrames` 琛ㄨ揪鍒锋柊璇箟銆?

## Phase 3F-4C - Compressed Parallel Validation Runner V1

### 鏂板

- 鏂板 `BuffSystemCompressedParallelValidationRunner` Unity Editor 鎵嬪姩楠岃瘉鑴氭湰銆?
- Runner 浣跨敤 `BuffSystemCore.CreateForCompressedParallelValidation(definitionRegistry, effectRegistry)` 鍒涘缓 gate=true 娴嬭瘯瀹炰緥锛屼笉浣跨敤鍙嶅皠锛屼笉鏂板 public gate 鍏ュ彛銆?

### 瑕嗙洊鑼冨洿

- gate=true + `CompressedExpiryFrameList` 鐨勫熀纭€ Append 璺緞銆?
- Append 澶氬眰鍚庣殑鑱氬悎 ViewData锛歚Stack`銆乣RemainingFrames`銆乣RuntimeHandle`銆?
- `EventTrigger`銆乣Unlimited`銆乣MaxStack > CompressedParallelBuffLayerBuffer.Capacity` fallback 鍒?`EntityPerStack`銆?
- gate=false 榛樿鏋勯€犺矾寰勫洖褰掞細鍗充娇閰嶇疆 `CompressedExpiryFrameList`锛屼粛璧?`EntityPerStack`銆?

### 鏆傛湭瑕嗙洊

- `RefreshEarliest`銆乣RefreshAll`銆乣ReplaceEarliestWhenFull`銆?
- `RemoveEarliest`銆乣RemoveLatest`銆乣ClearAll`銆?
- Tick / Expire銆?
- PendingRemove / Destroy銆?
- duration / forever 娣峰悎鍐呴儴鐘舵€併€?

### 淇濇寔涓嶅彉

- 姝ｅ紡杩愯鏃堕粯璁?gate 浠嶅叧闂€?
- public API 鍜?public 鏋勯€犲嚱鏁颁笉鍙樸€?
- `EntityPerStack` 榛樿璺緞涓嶅彉銆?
- 浜嬩欢鍨?Effect 鐑矾寰勪笉鍙樸€?

## Phase 3F-4D - Compressed Parallel Validation Runner V2

### 鏂板

- 鍦?`BuffSystemCompressedParallelValidationRunner` 涓拷鍔?Refresh / Remove policy 楠岃瘉銆?
- 鏂板 `RefreshEarliest`銆乣RefreshAll`銆乣RemoveEarliest`銆乣RemoveLatest`銆乣ClearAll` 娴嬭瘯缁勩€?

### 瑕嗙洊鑼冨洿

- `RefreshEarliest` 鍙埛鏂版渶鏃╁眰锛屽苟楠岃瘉 `layerId / layerRuntimeHandle` 淇濇寔涓嶅彉銆?
- `RefreshAll` 鍒锋柊鎵€鏈夊凡鏈夊眰锛屽苟楠岃瘉 `ViewData.Stack` 涓嶅彉銆?
- `RemoveEarliest` 绉婚櫎鏈€鏃╁眰鍚庯紝鍓╀綑灞備粛鍙煡璇€?
- `RemoveLatest` 绉婚櫎鏈€鏂板眰鍚庯紝鍓╀綑灞備粛鍙煡璇€?
- `ClearAll` 鍚?public 鏌ヨ涓嶅啀鏄剧ず璇?Buff銆?
- gate=false 榛樿鏋勯€犺矾寰勪粛鍥炲綊楠岃瘉涓?`EntityPerStack`銆?

### 鏆傛湭瑕嗙洊

- Tick / Expire銆?
- PendingRemove / Destroy 娣卞害楠岃瘉銆?
- `ReplaceEarliestWhenFull`銆?
- duration / forever 娣峰悎鍐呴儴鐘舵€併€?

### 淇濇寔涓嶅彉

- 鏈慨鏀?`BuffSystemCore.cs`銆?
- 鏈慨鏀?public API 鎴?public 鏋勯€犲嚱鏁般€?
- 鏈慨鏀规寮忚繍琛屾椂榛樿 gate銆?
- 鏈慨鏀逛簨浠跺瀷 Effect 鐑矾寰勩€?

## Phase 3F-8 - Compressed Parallel Docs Consolidation

### 鏂板

- 鏀舵暃 `CompressedExpiryFrameList` 姝ｅ紡鍚敤鍓嶅伐绋嬭鏄庛€?
- 鍦?ParallelBuff銆丄PI銆丒ffectPipeline銆丒xamples銆丮igration 鍜?Changelog 鏂囨。涓ˉ鍏?compressed parallel 褰撳墠鐘舵€併€乬ate 闄愬埗銆侀獙璇佺粨鏋滃拰椋庨櫓椤广€?

### 褰撳墠鐘舵€?

- 姝ｅ紡 public constructor 璺緞 gate 榛樿鍏抽棴銆?
- 姝ｅ紡杩愯鏃朵粛榛樿 `EntityPerStack`銆?
- `CreateForCompressedParallelValidation(...)` 鏄?internal test-only factory锛屽彧渚?validation runner 浣跨敤銆?
- Phase 3G 鍓嶄笉寤鸿涓氬姟浠ｇ爜鐩存帴浣跨敤 validation factory銆?
- Phase 3G 鍓嶄笉寤鸿鍏ㄩ」鐩洿鎺ユ墦寮€ compressed gate銆?

### eligibility 鏉′欢

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
compressed gate == enabled
```

### fallback 鏉′欢

```text
gate=false
EventTrigger parallel buff
Unlimited == true
MaxStack > CompressedParallelBuffLayerBuffer.Capacity
浠讳綍涓嶆弧瓒?eligibility 鐨勯厤缃?
```

fallback 鍚庝粛璧?`EntityPerStack`銆?

### 鍙ｅ緞璇存槑

ViewData 鍙ｅ緞锛?

```text
Stack = active layerCount
duration RemainingFrames = min(expireFrame - currentFrame)
forever RemainingFrames = -1
RuntimeHandle = min(active layerRuntimeHandle)
```

Tick / Effect snapshot 鍙ｅ緞锛?

```text
Tick snapshot RemainingFrames = expireFrame - currentFrame + 1
Remove snapshot RemainingFrames = 0
forever snapshot remainingFrames = 0
```

ViewData 鍙ｅ緞鍜?Tick snapshot 鍙ｅ緞涓嶈兘娣风敤銆?

### PendingRemove / Replace 璇存槑

- 鏈€鍚庝竴灞?Remove / Expire / ClearAll 鍚庯紝compressed runtime container 杩涘叆 pending remove銆?
- pending remove 浣跨敤 `compressedRuntimeHandle`銆?
- container pending remove 涓嶉澶栬Е鍙戣仛鍚?Remove Effect銆?
- layer Remove 浣跨敤 `layerRuntimeHandle`銆?
- pending remove 鍚?`TryGetBuff / GetBuffs` 涓嶆樉绀恒€?
- Destroy 鍓?defensive 娓呯悊 `_compressedRuntimeEntityByKey`銆?
- `ReplaceEarliestWhenFull` 鏈弧灞傛椂 Append锛屾弧灞傛椂绉婚櫎鏈€鏃╁眰骞惰拷鍔犳柊灞傘€?
- Replace 鏂板眰鐢熸垚鏂扮殑 `layerId / layerRuntimeHandle`锛屾湭鏇挎崲灞?identity 淇濇寔銆?
- 涓嶅亣璁?Remove callback 涓€瀹氭棭浜?Apply callback锛汦ffect Flush 椤哄簭浠嶇敱 Phase 2A phaseOrder 鍐冲畾銆?

### 宸查獙璇佹竻鍗?

- Append / ViewData / fallback / gate=false銆?
- RefreshEarliest / RefreshAll銆?
- RemoveEarliest / RemoveLatest / ClearAll銆?
- duration=1 / duration=2 Tick + Expire銆?
- forever layer銆?
- PendingRemove / Destroy銆?
- compressed lookup cleanup銆?
- Query 鍙璇箟銆?
- ReplaceEarliestWhenFull銆?
- `MaxStack == CompressedParallelBuffLayerBuffer.Capacity`銆?
- Phase 2A Runner 鍥炲綊 PASS銆?
- Compressed Parallel Validation V1/V2/V3/V4 PASS銆?

### 鏈鐩?/ 椋庨櫓椤?

- mixed duration / forever internal-state 灏氭湭涓撻」楠岃瘉銆?
- 鎬ц兘娴嬭瘯灏氭湭瀹屾垚銆?
- 鍥炴粴蹇収楠岃瘉灏氭湭瀹屾垚銆?
- 姝ｅ紡鍚敤鍓嶄粛闇€ Phase 3G 灏忚寖鍥村紑鍚瓥鐣ャ€?
- 涓嶅缓璁洿鎺ュ叏椤圭洰鎵撳紑 compressed gate銆?

### 鍚庣画

- Phase 3G锛氬皬鑼冨洿姝ｅ紡鍚敤 eligible Tick 鍨?parallel buff銆?
- Phase 3H锛氭€ц兘瀵规瘮銆佸洖婊氬揩鐓ч獙璇併€佽涓轰竴鑷存€ч獙璇併€?
- Phase 3I锛氳瘎浼版槸鍚︽墿澶у埌鏇村 parallel buff 绫诲瀷銆?

## Phase 3G-1 - Compressed whitelist gate skeleton

### 鏂板

- 鍦?compressed global gate 鍜?eligibility 涔嬮棿鏂板 configId whitelist 闂ㄧ銆?
- `ShouldUseCompressedParallel` 鐜板湪蹇呴』鍚屾椂婊¤冻锛?

```text
_enableCompressedParallelRuntime
&& IsCompressedParallelWhitelisted(definition.ConfigId)
&& IsCompressedParallelEligible(definition)
```

### 鐢熶骇榛樿琛屼负

- public constructor 浠嶄负 gate=false銆?
- public constructor 鐨?whitelist 涓虹┖銆?
- 涓嶅惎鐢ㄤ换浣曠敓浜?Buff銆?
- 鎵€鏈夌敓浜?Buff 榛樿浠嶈蛋 `EntityPerStack`銆?
- 闈炵櫧鍚嶅崟 Buff 鍗充娇 eligible锛屼篃 fallback 鍒?`EntityPerStack`銆?

### validation runner

- `CreateForCompressedParallelValidation(...)` 淇濇寔 internal test-only銆?
- validation factory 浣跨敤娴嬭瘯鐧藉悕鍗曪紝浠呰鐩?`BuffSystemCompressedParallelValidationRunner` 褰撳墠娴嬭瘯 configId銆?
- Runner 浠嶅彲楠岃瘉 compressed path銆?

### 淇濇寔涓嶅彉

- 鏈慨鏀?public API銆?
- 鏈慨鏀?public constructor 绛惧悕銆?
- 鏈慨鏀?`BuffConfigData`銆?
- 鏈慨鏀?`BuffDefinition` public 瀛楁銆?
- 鏈慨鏀逛簨浠跺瀷 Effect 鐑矾寰勩€?
- Phase 3G-2 鎵嶄細鍗曠嫭閫夋嫨绗竴涓敓浜ц瘯鐐?configId銆?

## Phase 3F-4F - Compressed Parallel Validation Runner V4

### 鏂板

- 鍦?`BuffSystemCompressedParallelValidationRunner` 涓拷鍔?`ReplaceEarliestWhenFull` 涓?capacity 杈圭晫楠岃瘉銆?

### 瑕嗙洊鑼冨洿

- `ReplaceEarliestWhenFull` 鏈弧灞傛椂杩藉姞鏂板眰锛屼笉鏇挎崲鏃у眰銆?
- `ReplaceEarliestWhenFull` 婊″眰鏃剁Щ闄ゆ渶鏃╁眰骞惰拷鍔犳柊灞傦紝鏈€缁?`layerCount` 淇濇寔 `MaxStack`銆?
- Replace 鍚庤褰曞苟楠岃瘉 layer identity锛氳鏇挎崲灞傛秷澶憋紝鏈浛鎹㈠眰淇濇寔锛屾柊灞傜敓鎴愭柊鐨?`layerId / layerRuntimeHandle`銆?
- Replace 鍚?public 鏌ヨ浠嶅彧鏈変竴涓?aggregate ViewData銆?
- Replace Effect 鍙獙璇佸繀瑕?`Apply / Remove` 瀛樺湪锛屼笉鍋囪 Remove 涓€瀹氭棭浜?Apply銆?
- `MaxStack == CompressedParallelBuffLayerBuffer.Capacity` 浠嶈蛋 compressed runtime銆?

### 鏆傛湭瑕嗙洊

- duration / forever 娣峰悎鍐呴儴鐘舵€併€?
- 鎬ц兘娴嬭瘯銆?
- 鍥炴粴蹇収娴嬭瘯銆?

### 淇濇寔涓嶅彉

- 鏈慨鏀?`BuffSystemCore.cs`銆?
- 鏈慨鏀?public API 鎴?public 鏋勯€犲嚱鏁般€?
- 鏈慨鏀规寮忚繍琛屾椂榛樿 gate銆?
- 鏈慨鏀逛簨浠跺瀷 Effect 鐑矾寰勩€?

## Phase 3F-4E - Compressed Parallel Validation Runner V3

### 鏂板

- 鍦?`BuffSystemCompressedParallelValidationRunner` 涓拷鍔?Tick / Expire / PendingRemove / Destroy 娣卞害楠岃瘉銆?
- 鏂板 `durationFrames = 1`銆乣durationFrames = 2`銆乫orever compressed layer 娴嬭瘯缁勩€?

### 瑕嗙洊鑼冨洿

- `durationFrames = 1`锛欶1 Apply锛孎2 Tick锛屽悓甯?Remove銆?
- `durationFrames = 2`锛欶2 Tick 涓?Remove锛孎3 Tick 鍚庡悓甯?Remove銆?
- Tick snapshot `RemainingFrames` 浣跨敤 `expireFrame - currentFrame + 1`銆?
- Remove snapshot `RemainingFrames = 0`銆?
- forever layer 鍙?Tick锛屼笉鑷劧 Expire锛沄iewData `RemainingFrames = -1`銆?
- 鏈€鍚庝竴灞?Expire 鍚庯紝`OnRemove` 涓?public 鏌ヨ涓嶅彲瑙併€?
- Destroy 鍚?compressed runtime 涓嶅啀瀛樺湪锛屽苟涓斿悓閰嶇疆鍙噸鏂?Add锛岄獙璇?lookup 娓呯悊銆?
- 涓嶉澶栬Е鍙?compressed runtime container 鑱氬悎 Remove Effect銆?

### 鏆傛湭瑕嗙洊

- `ReplaceEarliestWhenFull`銆?
- duration / forever 娣峰悎鍐呴儴鐘舵€併€?
- 鎬ц兘娴嬭瘯銆?
- 鍥炴粴蹇収娴嬭瘯銆?

### 淇濇寔涓嶅彉

- 鏈慨鏀?`BuffSystemCore.cs`銆?
- 鏈慨鏀?public API 鎴?public 鏋勯€犲嚱鏁般€?
- 鏈慨鏀规寮忚繍琛屾椂榛樿 gate銆?
- 鏈慨鏀逛簨浠跺瀷 Effect 鐑矾寰勩€?
