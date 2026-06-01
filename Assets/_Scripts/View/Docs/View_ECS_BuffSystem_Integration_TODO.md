# View 与 ECS / BuffSystem 对接待完善清单

## 1. 当前对接结论

当前项目已经具备基础 ECS View 桥接：实体可以通过 `PrefabViewRequestComponent` 创建 View，通过 `ViewComponent` 保存 `viewID`，再由 View 系统同步 Transform 和释放表现对象。

BuffSystem 已经提供正式只读查询路径：`IBuffSystem.GetBuffs`、`IBuffSystem.TryGetBuff` 和 `BuffViewData`。View 层后续应优先通过这些接口读取 Buff 状态。

当前 View 层作为基础桥接和 Debug 骨架可以接受，但正式 `BuffIcon`、`BuffBar`、`HUD`、`StatusView` 尚未真正完成。现有 `ViewBridge.SyncBuffUI` 只调用了 `GetBuffs`，还没有把结果渲染到 UI。

当前没有发现正式 View 直接修改 `BuffRuntimeComponent` 或长期持有 Buff Runtime Entity 的运行路径。后续正式 View 必须继续保持只读原则：View 可以使用 `MonoBehaviour` / `GameObject` 做表现，但 BuffSystem runtime 不能依赖 `MonoBehaviour` / `GameObject`。

当前主要缺口集中在正式 Buff UI、刷新点、表现数据解析、PendingRemove 安全查询、Entity/View 解绑安全性、并行 Buff 显示规则。后续实现优先只改 View 层；除非经过审核确认 BuffSystem / ECS 确实缺少必要接口，否则不要修改 BuffSystem / ECS。

当前 Phase 3B / Phase 3C-1 的 `CompressedExpiryFrameList` 尚未启用，View 层当前仍按 `EntityPerStack` 语义理解运行时。

## 2. 已检查到的相关文件

### 2.1 View 桥接层

- `Assets/_Scripts/View/EntityViewBinder.cs`
- `Assets/_Scripts/View/GameObjectPoolViewInstanceProvider.cs`
- `Assets/_Scripts/View/SimulationInitializer.cs`
- `Assets/_Scripts/View/ViewBridge.cs`
- `Assets/_Scripts/View/WorldViewEventConsumer.cs`
- `Assets/_Scripts/View/Debug/LogicFrameDebugPanel.cs`
- `Assets/_Scripts/View/Debug/SimulationDebugProbe.cs`

### 2.2 ECS View 系统 / Adapter

- `Assets/_Scripts/FrameWork/ECS/View/ViewSpawnSystem.cs`
- `Assets/_Scripts/FrameWork/ECS/View/EntityViewBindingSystem.cs`
- `Assets/_Scripts/FrameWork/ECS/Adapter/UnityAdapter/ViewSyncSystem.cs`
- `Assets/_Scripts/FrameWork/ECS/View/ViewDestroySystem.cs`
- `Assets/_Scripts/FrameWork/ECS/View/ViewManager.cs`
- `Assets/_Scripts/FrameWork/ECS/View/IViewInstanceProvider.cs`
- `Assets/_Scripts/FrameWork/ECS/View/DefaultViewInstanceProvider.cs`
- `Assets/_Scripts/FrameWork/ECS/View/PoolSystemViewInstanceProvider.cs`
- `Assets/_Scripts/FrameWork/ECS/Adapter/WorldViewReader.cs`
- `Assets/_Scripts/FrameWork/Contracts/ECSInterface/IWorldViewReader.cs`

### 2.3 Buff View / Contract

- `Assets/_Scripts/FrameWork/BuffSystem/Interface/IBuffSystem.cs`
- `Assets/_Scripts/FrameWork/BuffSystem/Interface/BuffViewData.cs`
- `Assets/_Scripts/FrameWork/Contracts/IViewBridge.cs`
- `Assets/_Scripts/FrameWork/Contracts/IDebugProbe.cs`
- `Assets/_Scripts/FrameWork/Contracts/ViewEffectCommand.cs`

当前未发现正式 `BuffIcon` / `BuffBar` / `HUD` / `StatusView` 脚本。

## 3. View 与 ECS Entity 绑定流程

当前流程如下：

1. `ViewSpawnSystem`
   - 根据 `PrefabViewRequestComponent + PositionComponent` 创建 View。
   - 写入 `ViewComponent(viewID)`。
2. `EntityViewBindingSystem`
   - 在 `spawn + 1` 阶段读取 `ViewComponent`。
   - 调用 `EntityViewBinder.Bind(entity, viewID)`。
   - 建立 `Entity -> viewID -> GameObject` 映射。
3. `ViewSyncSystem`
   - 在 `view` 阶段读取 `PositionComponent + ViewComponent`。
   - 同步 Unity `Transform.position`。
4. `ViewDestroySystem`
   - 在 `viewCleanup` 阶段处理 `ViewDestroyRequestComponent`。
   - 释放 View 并移除 `ViewComponent`。

### TODO-VIEW-001：修正 Entity/View 解绑时序或增加存活校验

- 问题编号：TODO-VIEW-001
- 当前状态：基础绑定流程已存在，但解绑检查时序存在一帧窗口。
- 问题描述：`EntityViewBindingSystem` 的解绑检查早于 `ViewDestroySystem`。`ViewDestroySystem` 本帧移除 `ViewComponent` 后，`EntityViewBinder` 中的绑定可能到下一固定帧才清理。`TryGetView` 当前不校验 `World.IsAlive(entity)`，存在最多一帧陈旧 Entity/View 映射窗口。
- 风险等级：P1
- 影响范围：一次性表现播放、View 查找、Entity 销毁后的表现安全性。
- 不建议做法：不建议让 BuffSystem 或 ECS 核心承担 View 解绑职责；不建议让 View 长期缓存 `GameObject` 并绕过 Entity 存活检查。
- 推荐修复方向：优先在 View 层补齐存活校验或调整 View 系统内的解绑时机；可考虑让 ViewBridge 在播放表现前通过只读接口确认 Entity 仍存活且仍拥有有效 View。
- AI 执行提示：后续实现前先输出 View 侧方案；优先只改 View 层。不要修改 BuffSystemCore，不要修改 ECS 核心调度，除非先经过接口契约审核。
- 验收标准：Entity 销毁后一帧内不会播放错误表现；被释放的 GameObject 不会被旧 Entity 找回；ViewBridge 对死亡 Entity 有安全保护。

## 4. Buff UI 尚未真正实现

当前 `ViewBridge.SyncBuffUI(Entity target, IBuffSystem buffSystem)` 已调用 `buffSystem.GetBuffs(target)`，但结果尚未用于 UI 渲染。正式 `BuffIcon`、`BuffBar`、`HUD`、`StatusView` 缺失。

### TODO-VIEW-002：实现正式 Buff UI 渲染入口

- 问题编号：TODO-VIEW-002
- 当前状态：存在 `SyncBuffUI` 方法骨架，但没有正式 UI 渲染逻辑。
- 问题描述：View 层尚未把 `BuffViewData` 转换为可见 Buff 图标、层数、剩余时间或状态栏。当前只能认为 Buff UI 仍处于 TODO 状态。
- 风险等级：P1
- 影响范围：玩家无法看到正式 Buff 状态；Debug 骨架不能替代正式 UI。
- 不建议做法：不建议直接读取或修改 `BuffRuntimeComponent`；不建议长期持有 Buff Runtime Entity；不建议让 UI 反向驱动 Buff runtime。
- 推荐修复方向：在 View 层建立正式 Buff UI 入口，通过 `IBuffSystem.GetBuffs(target)` 获取只读 `BuffViewData`，View 只持有角色 / 单位 Entity，不持有 Buff Runtime Entity。
- AI 执行提示：View 可以使用 `MonoBehaviour` / `GameObject` 实现 UI，但 BuffSystem runtime 不能依赖 `MonoBehaviour` / `GameObject`。所有运行时 Buff 修改应通过 Gameplay / Command / ECS System 进入。
- 验收标准：能显示目标 Entity 当前 Buff 列表；Buff 添加后 UI 可刷新；Buff 移除后 UI 不再显示；PendingRemove Buff 不会显示；正式 UI 不直接访问 `BuffRuntimeComponent`。

## 5. Buff UI 刷新点未明确

Phase 2A 后，生命周期 Effect 通过 EffectRequest Pipeline 在本帧末尾 Flush。Add / Refresh / StackChanged / Tick / Remove 会进入生命周期 EffectRequest 队列。Flush 中产生的新 Add / Remove 会进入 `_queuedCommands`，下一次 Tick 消费。

如果 View 在 BuffSystem Tick 前刷新，可能晚一帧看到 Buff 状态变化。View 应在 BuffSystem Tick 完成后读取 `IBuffSystem.GetBuffs` 的结果。

### TODO-VIEW-003：明确 Buff UI 刷新时机

- 问题编号：TODO-VIEW-003
- 当前状态：BuffSystem Tick 已接入 ECS 逻辑阶段，但正式 Buff UI 刷新系统尚未定义。
- 问题描述：当前没有明确的 Buff UI 刷新点。若未来 UI 在 BuffSystem Tick 前读取，会造成表现晚一帧；若在任意 `Update()` 中混合 runtime 查询和表现逻辑，也容易形成时序不一致。
- 风险等级：P1
- 影响范围：Buff 添加、刷新、层数变化、移除后的 UI 表现时序。
- 不建议做法：不建议在任意 `Update()` 中直接混合 Runtime 查询和表现修改；不建议让 UI 通过 `Time.deltaTime` 推进 Buff 逻辑。
- 推荐修复方向：设计 `BuffUIViewPresenter` 或 `BuffUIRefreshSystem`，在 BuffSystem Tick 完成后从 `IBuffSystem.GetBuffs(target)` 拉取快照并刷新 UI。
- AI 执行提示：刷新系统应属于 View 层后续工作。不要改 Phase 2A EffectRequest Pipeline，不要让 Flush 中产生的新命令本帧强行可见。
- 验收标准：BuffSystem Tick 后 UI 刷新；Phase 2A 非递归 Flush 不导致 UI 显示错误；OnRemove 后不显示已逻辑删除 Buff；AddBuff 后最近下一次 UI 刷新周期显示。

## 6. PendingRemove 显示风险

当前 BuffSystem 通过 `TryGetBuff / GetBuffs` 路径过滤 PendingRemove runtime。风险来自 View 层如果绕过 `IBuffSystem` 查询，直接读取 ECS Component，可能显示已经逻辑删除但尚未物理 Destroy 的 Buff。

### TODO-VIEW-004：View 层禁止绕过 BuffSystem 查询 Buff runtime

- 问题编号：TODO-VIEW-004
- 当前状态：现有正式 View 未发现直接访问 `BuffRuntimeComponent`，但后续正式 UI 需要明确禁止该路径。
- 问题描述：`IBuffSystem.GetBuffs / TryGetBuff` 承担 ViewCache、PendingRemove 过滤和并行聚合语义。View 如果直接查 `BuffRuntimeComponent`，会绕过这些语义。
- 风险等级：P1
- 影响范围：PendingRemove 显示、Buff 移除表现、并行 Buff 聚合、缓存一致性。
- 不建议做法：不建议正式 View 直接 `World.Query().With<BuffRuntimeComponent>()`；不建议缓存 Buff Runtime Entity；不建议通过 ECS component 自行判断 Buff 是否显示。
- 推荐修复方向：正式 View 只使用 `IBuffSystem.TryGetBuff / GetBuffs`。Debug 工具如需直接读 ECS，必须标注为 Debug Only。
- AI 执行提示：实现 Buff UI 时先搜索 View 正式路径，确保没有直接访问 `BuffRuntimeComponent`。需要额外字段时先提出接口契约，不要绕过 BuffSystem。
- 验收标准：正式 View 路径不存在直接访问 `BuffRuntimeComponent`；PendingRemove Buff 不会显示；Remove Effect 执行前后 UI 语义一致。

## 7. BuffViewData 字段可能不足

当前 `BuffViewData` 包含 `Target`、`Source`、`ConfigId`、`Stack`、`RemainingFrames`、`RuntimeHandle`。这些字段足够做基础列表和倒计时，但不足以独立完成正式 Buff UI。

View 显示 BuffIcon、名称、描述、颜色、分组、排序时，可能需要 authoring 数据。表现数据不应放入 runtime component，推荐通过 `BuffConfigData`、Catalog 或 ViewConfig 读取。

### TODO-VIEW-005：补全 Buff 表现数据读取方案

- 问题编号：TODO-VIEW-005
- 当前状态：runtime 只提供基础 `BuffViewData`；表现资源解析方案尚未建立。
- 问题描述：正式 UI 需要图标、显示名、描述、颜色、标签或排序规则时，不能把 Unity `Sprite` / `Material` / `GameObject` 引用放入 BuffSystem runtime，也不能要求 View 直接读 `BuffRuntimeComponent`。
- 风险等级：P1
- 影响范围：BuffIcon 显示、BuffBar 排序、Tooltip 文案、表现资源加载。
- 不建议做法：不建议扩展 runtime component 去保存 Unity 资源引用；不建议在 BuffSystemCore 内处理 UI 图标或文字；不建议 View 为了取显示数据读取 runtime component。
- 推荐修复方向：Runtime `BuffViewData` 只提供运行时状态；图标、名称、描述、颜色、层级显示样式从配置表 / Catalog / ViewConfig 读取。可建立 `BuffUIViewConfigResolver` 或类似 View Adapter。
- AI 执行提示：如发现 `BuffViewData` 缺字段，先判断是运行时状态字段还是表现字段。表现字段放 View / Config / Authoring 层；运行时字段需要接口契约审核。
- 验收标准：UI 可以根据 configId 找到图标和显示名；Runtime 仍然保持纯数据；表现资源引用只存在 View / Config / Authoring 层。

## 8. 并行 Buff 显示语义未最终确定

当前运行时仍是 `EntityPerStack`。Phase 3B / Phase 3C-1 的 `CompressedExpiryFrameList` 只是预留，没有启用。当前 `GetBuffs` 语义应作为 View 的主要依据，而不是 Buff Runtime Entity 数量。

### TODO-VIEW-006：确定并行 Buff 的 UI 聚合规则

- 问题编号：TODO-VIEW-006
- 当前状态：运行时仍按 `EntityPerStack` 执行；压缩并行 Buff 未启用；正式 UI 聚合规则尚未最终确认。
- 问题描述：并行 Buff 可以显示为多条，也可以聚合为一个图标 + stack 数。当前 View 层需要明确采用哪种语义，避免后续 `CompressedExpiryFrameList` 启用时 UI 依赖 runtime entity 数量而失效。
- 风险等级：P1
- 影响范围：并行 Buff 图标数量、层数显示、剩余时间显示、后续压缩模式兼容性。
- 不建议做法：不建议 UI 用 Buff Runtime Entity 数量判断层数；不建议 UI 假设每一层永远对应一个 runtime entity；不建议本阶段启用 `CompressedExpiryFrameList`。
- 推荐修复方向：短期按 `IBuffSystem.GetBuffs` 当前返回语义显示。若返回聚合后的 `BuffViewData`，UI 显示一个图标 + stack；后续压缩模式启用后继续依赖 `BuffViewData.Stack / RemainingFrames`。
- AI 执行提示：本任务只属于 View 显示规则确认，不进入 Phase 3C-2。不要修改 `ShouldUseCompressedParallel`，不要启用 `CompressedExpiryFrameList`。
- 验收标准：`EntityPerStack` 模式下显示正确；后续 `CompressedExpiryFrameList` 启用后不需要大改 UI；UI 不依赖 Buff Runtime Entity 数量判断层数。

## 9. Entity/View 陈旧绑定风险

当前 Entity/View 绑定由 View 系统维护。由于解绑时序可能晚于 View 销毁，后续表现播放或 UI 定位需要增加安全边界。

### TODO-VIEW-007：降低 Entity/View 陈旧引用窗口

- 问题编号：TODO-VIEW-007
- 当前状态：`EntityViewBinder` 维护双向映射，但不持有 World，无法自行校验 Entity 存活。
- 问题描述：如果 Entity 已销毁或 View 已释放，旧映射可能短时间内仍可被 `TryGetView` 命中。一次性表现可能被播放到错误或已回收的 View 上。
- 风险等级：P1
- 影响范围：特效播放、Buff 图标定位、血条 / 状态条跟随、对象池复用安全性。
- 不建议做法：不建议让 Binder 隐式拥有完整 World 修改权限；不建议让 BuffSystem runtime 引用 GameObject；不建议 View 层长期持有已解析出的 GameObject 而不重新校验。
- 推荐修复方向：候选方案 A：调整 View 系统顺序，让 ViewDestroy 后再清理 Binder。候选方案 B：ViewBridge 查询前校验 Entity alive 且仍有 `ViewComponent`。候选方案 C：ViewDestroySystem 释放 View 时主动通知 Binder 解绑。
- AI 执行提示：本文档只提出方案，不直接决定实现。后续由 View 同学根据当前 ECS 系统顺序选择最小改动方案。
- 验收标准：Entity 销毁后一帧内不会播放错误表现；被释放的 GameObject 不会被旧 Entity 找回；ViewBridge 对死亡 Entity 有安全保护。

## 10. View 层与 Runtime 修改边界

正式 View 原则上只读 BuffSystem。View 负责显示 `BuffViewData` 和播放表现，不负责修改 gameplay runtime。

### TODO-VIEW-008：明确 View 只读原则

- 问题编号：TODO-VIEW-008
- 当前状态：当前正式 View 未发现直接调用 `AddBuff / RemoveBuff / Raise` 的运行路径，但后续 UI 交互需要明确边界。
- 问题描述：如果正式 View 直接调用 BuffSystem 修改接口，会让 UI 与 gameplay 逻辑耦合，破坏命令、回滚、固定帧和生命周期边界。
- 风险等级：P1
- 影响范围：Buff 添加 / 移除来源、回滚一致性、Debug 与正式逻辑隔离。
- 不建议做法：不建议正式 View 脚本直接调用 `AddBuff / RemoveBuff / Raise`；不建议 UI 把表现状态反写为 gameplay 状态。
- 推荐修复方向：正式 gameplay 修改应通过 Command / Gameplay System / ECS System 进入 BuffSystem。Debug 面板或工具按钮如果需要触发 Buff，必须明确命名和标注 Debug / Tooling。
- AI 执行提示：实现 UI 按钮前先区分正式玩法入口和 Debug 工具入口。正式 View 只读，Debug 入口必须隔离。
- 验收标准：正式 View 脚本不直接写 Buff runtime；Debug 入口命名清晰，例如 `DebugAddBuffButton`；View 层没有把表现状态反写为 gameplay 状态。

## 11. Time.deltaTime 使用边界

View 可以使用 `Time.deltaTime` 做 UI 动画、平滑过渡、倒计时插值。Buff runtime 的剩余时间必须来自固定帧数据，不能由 View 用 `Time.deltaTime` 推进。

### TODO-VIEW-009：限制 View 层倒计时显示逻辑

- 问题编号：TODO-VIEW-009
- 当前状态：当前未发现 BuffSystem runtime 直接依赖 `Time.deltaTime`；`TimeSimulator` 只负责把真实时间转换为固定帧推进。
- 问题描述：未来正式 Buff UI 如果自行用 `Time.deltaTime` 扣减 Buff 剩余时间并回写 runtime，会破坏固定帧语义。
- 风险等级：P1
- 影响范围：倒计时显示、Buff 过期时机、回滚 / 同步一致性。
- 不建议做法：不建议 View 用 `Time.deltaTime` 调用 BuffSystem Tick；不建议 View 修改 `remainingFrames`；不建议用 UI 倒计时结果作为 Buff 逻辑依据。
- 推荐修复方向：View 可以用 `Time.deltaTime` 做表现平滑，但 Buff runtime 剩余时间必须来自 `BuffViewData.RemainingFrames`。显示秒数应由固定帧换算，例如 `remainingFrames / fixedFrameRate`。
- AI 执行提示：任何 `Time.deltaTime` 使用都只能影响表现，不得影响 BuffSystem runtime。需要 fixed tickLength 时从已有运行配置或 View 初始化参数传入。
- 验收标准：View 中使用 `Time.deltaTime` 的地方只影响表现；不存在用 `Time.deltaTime` 调用 BuffSystem Tick 或修改 `remainingFrames` 的逻辑。

## 12. 旧 BuffHandler 残留检查

当前检查未发现正式 View 层引用旧 `BuffHandler`、`BuffRuntimeData`、`ParallelBuffRunTimeData`、`GameObject Target`、`GameObject Sourse` 等第二套结构。但后续新增 View 代码仍需持续避免混用旧路径。

### TODO-VIEW-010：清理或隔离旧 BuffHandler View 依赖

- 问题编号：TODO-VIEW-010
- 当前状态：本轮未发现正式 View 依赖旧 BuffHandler runtime。
- 问题描述：如果后续 View 层重新引用旧 BuffHandler 或旧 GameObject Target/Sourse 结构，会与 ECS BuffSystem 形成第二套 runtime，导致显示和逻辑不一致。
- 风险等级：P2
- 影响范围：旧代码迁移、Debug 示例、正式 UI 数据来源一致性。
- 不建议做法：不建议正式 View 使用旧 `BuffHandler`、`BuffRuntimeData`、`ParallelBuffRunTimeData`；不建议旧 GameObject Target/Sourse 路径与 ECS BuffSystem 混用。
- 推荐修复方向：如发现旧代码仍在运行路径中，应迁移到 `IBuffSystem.GetBuffs / BuffViewData`。如只是废弃代码，应移动到 Legacy / Deprecated 或在文档中明确标注。
- AI 执行提示：后续每次实现 Buff UI 前先搜索旧关键词，确认正式路径不依赖第二套 runtime。
- 验收标准：正式 View 不依赖旧 BuffHandler；旧代码不与 ECS BuffSystem 混用；Debug / Legacy 代码有明确标注。

## 13. 推荐后续实施顺序

### Milestone V1：只读显示闭环

- 实现 `BuffUIViewPresenter` 或等价 View 侧组件。
- 从 `IBuffSystem.GetBuffs(target)` 读取 `BuffViewData`。
- 显示基础列表：`configId`、`stack`、`remainingFrames`。
- 不接入图标资源。

### Milestone V2：表现数据接入

- 通过 `configId` 解析图标、名称、描述、颜色。
- 不污染 Runtime Component。
- 添加基础 `BuffIcon` / `BuffBar` UI。

### Milestone V3：刷新时机与生命周期对齐

- 明确在 BuffSystem Tick 后刷新 UI。
- 处理 PendingRemove 查询语义。
- 处理 Entity 销毁与 View 解绑安全性。

### Milestone V4：并行 Buff 显示策略

- 确定聚合显示还是多层显示。
- 与后续 `CompressedExpiryFrameList` 保持兼容。
- 不依赖 Buff Runtime Entity 数量。

### Milestone V5：Debug 与正式 View 分离

- Debug 面板可以提供测试入口，但必须清楚标注。
- 正式 View 只读 BuffSystem。
- 旧 BuffHandler 依赖隔离或迁移。

## 14. AI 执行总规则

后续 AI / Codex 在处理 View 与 BuffSystem 对接时必须遵守以下规则：

- 修改前必须先输出方案。
- 优先只改 View 层。
- 不允许修改 `BuffSystemCore.cs`，除非明确发现接口缺失并经过审核。
- 不允许修改 ECS 核心调度，除非是 View 生命周期 bug 且经过审核。
- 不允许让 BuffSystem runtime 依赖 `MonoBehaviour` / `GameObject`。
- View 可以使用 `MonoBehaviour` / `GameObject` 做 UI 和表现。
- 不允许让 UI 使用 `Time.deltaTime` 推进 gameplay Buff。
- 不允许正式 View 直接读取或修改 `BuffRuntimeComponent`。
- 不允许正式 View 长期持有 Buff Runtime Entity。
- View 应只持有角色 / 单位 Entity。
- View 应通过 `IBuffSystem` 和 `BuffViewData` 工作。
- 当前 `CompressedExpiryFrameList` 尚未启用，不要进入 Phase 3C-2。
- 当前 View 仍按 `EntityPerStack` 语义理解运行时。
- 所有新增 View 文档放在 `Assets/_Scripts/View/Docs`。
- 所有新增 View 代码要有简洁中文注释，复杂字段写明作用。

## 15. 最终总结

当前 View 与 ECS / BuffSystem 已具备基础桥接，但正式 Buff UI 还未完成。后续重点不是修改 BuffSystem 或 ECS，而是补齐 View 侧 Presenter、刷新时机、Buff 表现数据解析、PendingRemove 安全查询、Entity/View 解绑安全性和并行 Buff 显示规则。

正式 View 应保持只读：通过 `IBuffSystem.GetBuffs / TryGetBuff` 获取 `BuffViewData`，只持有角色 / 单位 Entity，不直接修改 `BuffRuntimeComponent`，不长期持有 Buff Runtime Entity。BuffSystem runtime 必须继续保持纯 ECS / 纯数据边界，不能引入 `MonoBehaviour` / `GameObject` 依赖。
