# BuffSystem Changelog

## Phase 3I-10D - BuffCandidateGraph 与 Authoring Hub 快捷编辑工作流集成

- Authoring Hub 顶部新增 `候选图联动 / Candidate Graph Link` 区域，可选择 `BuffCandidateGraph` 并查看候选摘要。
- 新增 Editor-only 桥接层 `BuffCandidateGraphBridge.cs`，用于从候选图构建摘要、Create Buff 草稿导入数据和 Effect Template 草稿导入数据。
- `Create Buff` 页面支持从候选图导入基础字段，并触发现有校验预览；该操作不会自动创建 `BuffConfigData`。
- `Effect Template` 页面支持从候选图导入 Effect 字段，并触发现有校验预览；该操作不会自动生成 Effect `.cs`。
- `Validator` 页面增加候选图 ConfigId 与真实 `BuffConfigData` 是否存在的对照提示；Validator 仍只扫描真实配置资源。
- 新增中文文档 `BuffSystem_xNodeAuthoringGraph.md`，说明 xNode 候选图与 Authoring Hub 的分工、推荐流程和边界。
- 更新 `BuffSystem_AuthoringGuide.md`，增加 xNode 候选图工作流入口说明。
- 本阶段未修改 runtime / registry / whitelist / compressed eligibility / xNode package / Packages manifest。
- 本阶段未创建 graph asset，未创建 `BuffConfigData`，未生成 Effect `.cs` 文件，未保存 scene。

## Phase 3I-10C-Polish - BuffCandidateGraph 节点 UI 可读性修复

- 新增 Editor-only 自定义 xNode 节点绘制文件 `BuffCandidateNodeEditors.cs`。
- 使用 xNode `NodeEditor.CustomNodeEditor`、`OnHeaderGUI`、`OnBodyGUI`、`GetWidth` 和 `NodeEditorGUILayout.PropertyField` 优化节点显示。
- 为 `BuffCandidateStartNode`、`BuffShapeNode`、`EffectBindingNode`、`CompressedEligibilityNode`、`RuntimeDependencyRiskNode`、`CandidateDecisionNode` 设置更宽的节点宽度。
- 将节点字段显示改为更短的中文 / 中英混合标签，减少长字段名遮挡。
- 长文本字段继续复用现有 `TextArea` 序列化显示，不重命名字段，不改变 graph asset 契约。
- 本阶段只修改 Editor UI 显示，不修改 `BuffCandidateGraph` 契约、evaluation 逻辑、runtime、registry 或 whitelist。
- 本阶段未创建 graph asset，未创建 `BuffConfigData`，未生成 Effect `.cs` 文件，未保存 scene。

## Phase 3I-10C-Fix - BuffCandidateGraph 创建菜单可见性修复

### Changed

- 为 `BuffCandidateGraph` 的 `CreateAssetMenu` 补充 `order = 5100`，用于提高 Unity Create 菜单排序稳定性。
- 新增 Editor-only 兜底菜单：
```text
Assets / Create / BuffSystem / Buff Candidate Graph
```
- 该菜单只在用户手动点击时创建 xNode 候选审查图，本阶段未自动创建 graph asset。
- 保留 `BuffCandidateGraph` 作为 Editor-only authoring / review 原型，不作为 production config source。

### Scope confirmation

- 本阶段未创建 `BuffCandidateGraph` asset。
- 本阶段未创建或修改 `BuffConfigData` asset。
- 本阶段未生成 Effect `.cs` 文件。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 registry。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未修改 xNode package、Packages manifest、ProjectSettings、scene、prefab 或 `.meta`。

## Phase 3I-10C - BuffCandidateGraph 最小 Editor-only 原型

### 新增

- 新增 xNode 候选审查图最小原型代码，目录：

```text
Assets/_Scripts/FrameWork/BuffSystem/Editor/AuthoringGraphs
```

- 新增 `BuffCandidateGraph`，用于真实 gameplay Buff 进入 production whitelist 前的可视化候选审查。
- 新增第一版节点类型：
  - `BuffCandidateStartNode`
  - `BuffShapeNode`
  - `EffectBindingNode`
  - `CompressedEligibilityNode`
  - `RuntimeDependencyRiskNode`
  - `CandidateDecisionNode`
- 新增最小 evaluation，仅检查节点数量完整性。

### 边界

- `BuffCandidateGraph` 只用于 Editor authoring / review。
- 图 asset 不应放入 `Assets/Resources/BuffSystem/Buff`。
- 图不参与 runtime 加载。
- 图不是 production config source。
- 本阶段不生成 `BuffConfigData`。
- 本阶段不生成 Effect `.cs`。
- 本阶段不注册 Effect。
- 本阶段不修改 registry。
- 本阶段不修改 whitelist。
- 本阶段不修改 BuffSystem runtime。
- 本阶段不修改 xNode package、Packages manifest 或 ProjectSettings。
- 本阶段未创建 graph asset，未保存 scene。

### 后续

- 后续可进入 `Phase 3I-10D`，为 `BuffCandidateGraph` 设计中文 Markdown 审查报告导出。
- 后续如需连接路径校验，应在 evaluation 中补充 `Start -> Decision` 的端口遍历逻辑。
- `BuffCandidateGraph` 不能替代 `BuffAuthoringValidator`、Runner 或 Unity 手动验证。

## Phase 3I-9C-Cleanup - Remove standalone Odin prototype entry

### Changed

- 用户确认不需要保留独立 Odin Hub prototype。
- 移除 `Tools / BuffSystem / Authoring Hub Odin Prototype` 对应的 Editor-only prototype 文件。
- 移除 `BuffAuthoringOdinHubWindow` 以及仅服务该独立窗口的 prototype page / view model。
- 保留原 `Tools / BuffSystem / Authoring Hub` 作为当前唯一主入口。
- Odin 后续只作为原 Authoring Hub 内局部增强方向。

### Scope confirmation

- 本阶段未修改原 IMGUI Authoring Hub 逻辑。
- 本阶段未修改 `BuffAuthoringHubWindow.cs`。
- 本阶段未修改 `BuffAuthoringValidatorWindow.cs`。
- 本阶段未修改 `BuffCreateWizardWindow.cs`。
- 本阶段未修改 `EffectTemplateGeneratorPanel.cs`。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 registry。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未修改 `BuffConfigData.cs`。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未保存 scene。
- 本阶段未安装、升级或删除 Odin / Sirenix 插件。

## Phase 3I-9C-Fix - Validator layout readability fix

### Changed

- 修复 Authoring Hub 的 Validator 扫描结果中长字段被截断的问题。
- `BuffType / TriggerType / ParallelStorageMode / EffectRegistered / CompressedEligibility / Category` 改为多行只读字段显示。
- 结果项从多个短列横向挤压布局，调整为基础信息、行为配置、Effect / Eligibility 分块布局。
- 长字段值使用可选中文本显示，避免中文 label 挤压 value 区域。

### Scope confirmation

- 本阶段只修改 Editor UI 显示布局。
- 本阶段未修改扫描逻辑。
- 本阶段未修改 validation 逻辑。
- 本阶段未修改分类逻辑。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 registry。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未保存 scene。
- 本阶段未新增独立 Odin Hub。
- Odin 后续仍作为原 Authoring Hub 内局部增强方向。

## Phase 3I-9C - Odin Authoring Hub prototype

### Added

- 新增 Editor-only Odin 原型窗口：`BuffAuthoringOdinHubWindow.cs`。
- 新增菜单入口：

```text
Tools / BuffSystem / Authoring Hub Odin Prototype
```

- 旧 IMGUI Authoring Hub 保留为 fallback：

```text
Tools / BuffSystem / Authoring Hub
```

### Prototype pages

- Odin prototype 当前包含：
  - `配置检查器 / Validator`
  - `创建 Buff / Create Buff`
  - `Effect 模板 / Effect Template`
- `Validator` page 仅做只读扫描，复用 `BuffAuthoringValidationUtility`，不修改 asset / whitelist / runtime。
- `Create Buff` page 仅做表单和校验预览，不创建 `BuffConfigData` asset。
- `Effect Template` page 仅做表单、校验和 registry snippet 复制，不生成 `.cs` 文件。

### Scope confirmation

- 本阶段未修改现有 IMGUI Authoring Hub 菜单行为。
- 本阶段未修改 `BuffConfigData.cs`。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 `BuffEffectRegistryBootstrap.cs` 或 production registry。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未修改 scene / prefab / `.meta`。
- 本阶段未安装、升级或删除 Odin。

### Manual verification plan

- 打开 `Tools / BuffSystem / Authoring Hub Odin Prototype`。
- 在 `配置检查器 / Validator` 执行 `扫描 / 刷新`，确认 `991001 Debug_CompressedParallel_TickSmoke` 被识别为 smoke/debug，且 effect registered / compressed eligibility 均为 true。
- 在 `创建 Buff / Create Buff` 验证默认值、`ConfigId=991001` duplicate、`EffectId=990101` registered、`EffectId=0` warning、compressed eligibility 和 `Unlimited=true` warning。
- 在 `Effect 模板 / Effect Template` 验证 `EffectId=990101` 不可生成、`EffectId=100001 + PoisonTickEffect` 可通过校验，并确认 registry snippet 为 `registry.Register(100001, new PoisonTickEffect());`。

## Phase 3I-9B - Authoring UI localization text foundation

### Added

- 新增 Editor-only 文案集中管理类：`BuffAuthoringText.cs`。
- `BuffAuthoringText` 当前集中管理 Authoring Hub 的主要 UI 文案，供现有 IMGUI 工具和未来 Odin 工具复用。

### Localized UI scope

- `BuffAuthoringHubWindow`：
  - window title / header 改为 `Buff 制作工具 / Authoring Hub`。
  - tabs 改为 `配置检查器 / 创建 Buff / Effect 模板`。
  - HelpBox 使用集中中文文案。
- `BuffAuthoringValidatorWindow`：
  - 扫描按钮、统计项、字段名、问题标题、Category 显示文案改为中文或中英混合。
  - `EffectRegistered / CompressedEligibility` 的显示结果改为 `是 / 否 / 未知`。
- `BuffCreateWizardWindow`：
  - 主要分组、字段、按钮、校验预览、错误 / 警告 / 建议标题改为中文或中英混合。
  - Category 显示复用统一中文文案。
- `EffectTemplateGeneratorPanel`：
  - 主要分组、字段、按钮、校验预览、错误 / 警告 / 建议标题改为中文或中英混合。
  - 保留 callback 方法名、registry snippet 和生成类结构。

### Preserved technical terms

- 本阶段保留以下技术术语或中英混合显示：
  - `Buff`
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

- 本阶段未修改 validation 逻辑。
- 本阶段未修改 asset 创建逻辑。
- 本阶段未修改 Effect 模板生成逻辑。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 `BuffEffectRegistryBootstrap.cs` 或 production registry。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未修改 scene / prefab / `.meta`。
- Odin 已检测存在，但本阶段尚未进行 Odin Hub 重构。

### Next

- 后续可进入 `Phase 3I-9C：Odin Authoring Hub prototype`。
- Odin prototype 应优先复用 `BuffAuthoringText` 和 `BuffAuthoringValidationUtility`，并继续保持 runtime 零依赖。

## Phase 3I-8 - Authoring Toolkit first-loop closeout

### Completed first-loop capabilities

- BuffSystem Authoring Toolkit 第一轮闭环已完成。
- 当前统一入口：

```text
Tools / BuffSystem / Authoring Hub
```

- 当前 Hub 已包含：
  - `Validator`
  - `Create Buff`
  - `Effect Template`
- `BuffAuthoringValidationUtility` 已完成轻量抽取，并接入 `Validator / Create Buff / Effect Template`。
- `BuffSystem_AuthoringGuide.md` 已完成，并已与当前 UI 字段 / 按钮对齐。
- Phase 3I-7B 对照复核结果全部 PASS：
  - `Create Buff` 对照 PASS。
  - `Effect Template` 对照 PASS。
  - Changelog 复核 PASS。

### Confirmed current state

- `Validator` 可识别 `991001 Debug_CompressedParallel_TickSmoke`。
- `991001 Debug_CompressedParallel_TickSmoke` 当前仍是 smoke/debug pilot，不是正式 gameplay Buff。
- `990101` 当前仍是 `DebugNoOpTickEffectId`。
- production whitelist 未扩大。
- 当前无真实 gameplay Buff 候选进入 compressed whitelist。

### Authoring boundaries

- Authoring Toolkit 不自动注册 Effect。
- Authoring Toolkit 不自动修改 `BuffEffectRegistryBootstrap`。
- Authoring Toolkit 不自动加入 whitelist。
- Authoring Toolkit 不自动修改 runtime。
- Authoring Toolkit 不自动保存 scene。
- Authoring Toolkit 不证明 rollback-ready。
- `Validator` 是 authoring 辅助，不是 runtime 安全证明。
- EffectId const 静态扫描只是辅助检查，不能覆盖所有动态注册来源。
- 满足 compressed eligibility 不等于进入 production whitelist。

### UX / feature backlog

- [Backlog] `Create Buff` 增加真正的 Reset / Clear 按钮。
- [Backlog] `Create Buff` 支持从现有 BuffConfigData clone draft。
- [Backlog] `Create Buff` 支持自动建议下一个可用 ConfigId。
- [Backlog] `Effect Template` 支持 Event Effect 模板，但需要事件类型选择机制。
- [Backlog] `Effect Template` 支持打开生成后的 `.cs` 文件。
- [Backlog] `Validator` 支持导出扫描报告。
- [Backlog] `Validator` 支持按 Category / EffectRegistered / Eligibility 过滤。
- [Backlog] Candidate workflow：真实 gameplay Buff 候选审查面板或 Runner。
- [Backlog] 正式 ID 分段规范待负责人确认。
- [Backlog] Odin / UI Toolkit 优化可作为后续体验增强，不作为当前硬依赖。

### Next

- Phase 3I Authoring Toolkit 可暂时封版。
- 下一步优先等待真实 gameplay Buff 候选提交。
- 如有候选，进入 `Phase 3H-8A / Production Candidate Review`。
- 如继续工具线，进入 `Phase 3I-9 UX Backlog 实现设计`。

### Scope confirmation

- 本阶段只修改 BuffSystem Changelog。
- 本阶段未新增独立 backlog 文档。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 `BuffEffectRegistryBootstrap.cs` 或 production registry。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未修改 Editor 工具代码。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未修改 scene / prefab / `.meta`。

## Phase 3I-7A - AuthoringGuide UI field alignment

### Documentation alignment

- 修正 `BuffSystem_AuthoringGuide.md` 与当前 Editor UI 的字段 / 按钮对照。
- 补全 `Create Buff` 当前字段、默认值和按钮：
  - `ConfigId`
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
- 明确当前 `Create Buff` 工具没有重置字段的 `Clear` 按钮；如未来需要，应另开 UX 改进阶段。
- 补全 `Effect Template` 当前字段、默认值和按钮：
  - `EffectId`
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
- 补充 `Open Effect Folder` 边界：只打开目标目录，不代表生成模板，不注册 Effect，也不修改 `BuffEffectRegistryBootstrap`。

### Scope confirmation

- 本阶段只修改 BuffSystem 文档。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 `BuffEffectRegistryBootstrap.cs` 或 production registry。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未修改 Editor 工具代码。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未修改 scene / prefab / `.meta`。

## Phase 3I-6 - Authoring Guide added

### Added documentation

- 新增 `BuffSystem_AuthoringGuide.md`，用于归档 Buff / Effect authoring 工具链的推荐使用流程。
- 文档覆盖以下内容：
  - `Tools / BuffSystem / Authoring Hub` 统一入口。
  - `Validator / Create Buff / Effect Template` 三个 tab 的用途。
  - 从零制作 Buff 的推荐流程。
  - Effect 模板生成流程。
  - 人工注册 Effect 的边界。
  - Create Buff 创建 BuffConfigData 草稿的流程。
  - Validator 检查项。
  - compressed whitelist 候选标准。
  - Effect 编写约束。
  - ID 使用建议。
  - 工具不会自动做什么。
  - 常见错误与处理建议。
  - 当前已知边界。
  - 最小示例流程。

### Authoring boundaries

- `Effect Template` 只生成 Effect 草稿模板，不自动注册 Effect。
- `Create Buff` 只创建 BuffConfigData 草稿，不自动加入 whitelist。
- `Validator` 是 authoring 辅助，不替代 Runner / 场景验证 / 人工审批。
- 满足 compressed eligibility 不等于自动进入 production whitelist。
- EventTrigger / Unlimited / 依赖逐层 runtime entity 的 Buff 当前不进入 compressed whitelist。
- `991001 Debug_CompressedParallel_TickSmoke` 仍是 smoke/debug pilot，不是正式 gameplay Buff。
- 正式 ID 分段规范仍待项目负责人确认。
- BuffSystem 仍不能宣称 rollback-ready。

### Scope confirmation

- 本阶段只修改 BuffSystem 文档。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 `BuffEffectRegistryBootstrap.cs` 或 production registry。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未修改 Editor 工具代码。
- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未修改 scene / prefab / `.meta`。

## Phase 3I-5B - BuffAuthoringValidationUtility closeout

### Implemented utility

- 已新增 Editor-only shared validation utility：`BuffAuthoringValidationUtility.cs`。
- 该 utility 当前集中复用以下只读 authoring 检查能力：
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

- `BuffAuthoringValidationUtility` 已接入以下 Editor 工具：
  - `BuffAuthoringValidatorWindow`
  - `BuffCreateWizardWindow`
  - `EffectTemplateGeneratorPanel`
- compressed eligibility Editor 检查口径保持不变：

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
```

### Manual verification

- Unity Console 手动确认无 error。
- `Validator` tab 仍能扫描到 `991001 Debug_CompressedParallel_TickSmoke`。
- Validator 统计仍为：

```text
Total=1
Eligible=0
Smoke=1
Invalid=0
```

- `991001 Debug_CompressedParallel_TickSmoke` 仍显示：

```text
EffectRegistered=True
CompressedEligibility=True
Category=Smoke / Debug Only
```

- `Create Buff` tab 默认字段正常显示：
  - `ConfigId = 100001`
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
- `Effect Template` tab 正常显示：
  - `EffectId = 0`
  - `Effect Class Name = NewBuffEffect`
  - `Target Folder = Assets/_Scripts/FrameWork/BuffSystem/Effects`
  - `Namespace = BuffSystem`
  - 默认 callbacks：`OnApply / OnTick / OnRemove` enabled，`OnRefresh / OnStackChanged` disabled。

### Scope confirmation

- 本阶段未创建 Buff asset。
- 本阶段未生成 Effect 模板文件。
- 本阶段未创建或修改 `.meta`。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 public API。
- 本阶段未修改 production registry 或 `BuffEffectRegistryBootstrap.cs`。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility runtime 逻辑。
- 本阶段未修改 scene / prefab。

### Boundaries

- `BuffAuthoringValidationUtility` 仅为 Editor authoring 工具服务。
- `BuffAuthoringValidationUtility` 不进入 runtime 设计。
- `BuffAuthoringValidationUtility` 不替代 `BuffDefinition` 或 runtime validator。
- EffectId const 静态扫描仍只是辅助静态检查，不代表覆盖所有动态注册来源。
- 本阶段不证明 BuffSystem rollback-ready。

### Next

- `BuffAuthoringValidationUtility` 当前可视为轻量抽取完成。
- 下一步建议进入 `Phase 3I-6：Authoring 工具链文档与使用流程归档`，或继续做 `Create Buff` / `Effect Template` 的细项体验验证。

## Phase 3I-4C - EffectTemplateGenerator closeout

### Implemented tool

- `Buff Authoring Hub -> Effect Template` tab 已替换 placeholder。
- 已新增 Editor-only 面板 `EffectTemplateGeneratorPanel.cs`。
- 面板当前支持：
  - `EffectId`
  - `Effect Class Name`
  - `Effect Display Name / Note`
  - `Target Folder`
  - `Namespace`
  - callback 勾选
  - `Validate`
  - `Generate Template`
  - `Copy Registry Snippet`
  - `Open Effect Folder`
  - `Clear`

### Manual verification

- 已确认 `EffectId = 990101` 会被识别为 production registry 已注册，并禁止生成重复模板。
- 已确认 `EffectId = 100001` + `PoisonTickEffect` 可通过校验。
- 已确认 `Copy Registry Snippet` 输出格式：

```csharp
registry.Register(100001, new PoisonTickEffect());
```

- 已临时生成并检查 `TempGeneratedEffect_DeleteMe.cs`。
- 生成模板包含：
  - 正确 class name
  - `internal const int EffectId`
  - `BuffEffectExecutorBase`
  - 已选 callbacks
- 已删除临时 `.cs` 文件。
- 临时 `.meta` 未生成，最终不存在。

### Template wording fix

- 已修正模板注释禁用词。
- 后续生成模板不再包含：
  - `Time.time`
  - `Time.deltaTime`
  - `GameObject`
  - `MonoBehaviour`
- 原提示语义保留为：
  - 不要使用 Unity 帧时间 API 作为 Buff runtime 逻辑时间。
  - 不要直接依赖 View 或 Unity 对象组件。
  - Effect 应优先写 ECS 状态。
  - production 使用前仍需手动注册。

### Scope confirmation

- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 `BuffEffectRegistryBootstrap.cs` 或 production registry。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility。
- 本阶段未修改 Buff asset。
- 本阶段未修改 scene / prefab / `.meta`。
- 本阶段未创建或保留临时模板文件。

### Boundaries

- `EffectTemplateGenerator` 只生成草稿模板，不代表 Effect 已进入 production registry。
- `EffectTemplateGenerator` 不自动修改 `BuffEffectRegistryBootstrap`。
- `EffectTemplateGenerator` 不自动创建正式 gameplay Effect。
- `EffectTemplateGenerator` 不修改 whitelist。
- `EffectTemplateGenerator` 不证明 rollback-ready。
- 生成的 Effect 仍必须由人工实现逻辑，并手动提交注册审批。
- production 使用前仍需运行 `BuffAuthoringValidator` 和相关验证。

### Next

- `EffectTemplateGenerator` 当前可视为最小实现完成。
- 下一步可进入 `Phase 3I-5：Authoring 工具链体验打磨 / shared validation utility 抽取`。

## Phase 3I-3C - Buff Authoring Hub manual verification closeout

### Implemented tool integration

- 已新增统一 Editor 工具入口 `Tools / BuffSystem / Authoring Hub`。
- Hub 当前包含三个 tab：
  - `Validator`
  - `Create Buff`
  - `Effect Template`
- 旧菜单入口仍保留：
  - `Tools / BuffSystem / Authoring Validator`
  - `Tools / BuffSystem / Buff Create Wizard`
- 旧菜单入口已改为打开 `Buff Authoring Hub` 并跳转到对应 tab。

### Manual verification

- Unity Editor 中已确认 `Buff Authoring Hub` 窗口可打开。
- `Validator / Create Buff / Effect Template` 三个 tab 均显示正常。
- `Validator` tab 点击 `Scan / Refresh` 后，扫描结果符合预期：
  - `Total = 1`
  - `Eligible = 0`
  - `Smoke = 1`
  - `Invalid = 0`
- 当前扫描到 `991001 Debug_CompressedParallel_TickSmoke`。
- `EffectRegistered = True`。
- `CompressedEligibility = True`。
- `Category = Smoke / Debug Only`。
- `Create Buff` tab 可显示默认字段：
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
- `Effect Template` tab 当前仅显示 Phase 3I-4 placeholder，没有实现代码生成。

### Pending manual checks

以下 `Create Buff` 细项尚未在本次 closeout 中归档为已验证，后续可进入 `Phase 3I-3D` 单独补测：

- `ConfigId = 991001` duplicate error。
- `EffectId = 990101` registered validation。
- `EffectId = 0` warning。
- `CompressedExpiryFrameList` eligibility preview。
- `Unlimited = true` warning。
- `Open Authoring Validator` button switching back to `Validator` tab。

### Scope confirmation

- 本阶段未创建任何 Buff asset。
- 本阶段未修改 BuffSystem runtime。
- 本阶段未修改 public API。
- 本阶段未修改 production whitelist、validation whitelist 或 compressed eligibility。
- 本阶段未修改已有 Buff asset。
- `Effect Template` tab 仍只是占位，不生成 Effect 代码、不注册 Effect。

### Next

- 下一步可进入 `Phase 3I-4：EffectTemplateGenerator 设计阶段`。
- 如果需要补齐 Create Buff 细项验收，可先进入 `Phase 3I-3D：Create Buff validation 细项手动验证`。

## Phase 3I-2A - BuffAuthoringValidator manual verification closeout

### Implemented tool

- 已新增 Editor-only 工具 `BuffAuthoringValidatorWindow.cs`。
- 菜单入口为 `Tools / BuffSystem / Authoring Validator`。
- 默认扫描路径为 `Assets/Resources/BuffSystem/Buff`。
- 工具只读扫描 `BuffConfigData` asset，显示关键字段摘要、Effect 注册状态、compressed eligibility、配置问题和候选分类。
- 工具不修改 asset、runtime、production whitelist、validation whitelist 或 compressed eligibility。

### Manual verification

- Unity Editor 中打开 `Tools / BuffSystem / Authoring Validator` 并点击 `Scan / Refresh` 后，扫描结果符合预期：
  - `Total = 1`
  - `Eligible = 0`
  - `Smoke = 1`
  - `Invalid = 0`
- 当前扫描到 `991001 Debug_CompressedParallel_TickSmoke`。
- `EffectRegistered = True`。
- `CompressedEligibility = True`。
- `Category = Smoke / Debug Only`。

### Conclusion

- `991001` 满足 compressed eligibility，且 `990101` Effect 已注册。
- `991001` 是 Debug / Smoke asset，不应作为正式玩法 Buff 候选。
- 当前没有真实 production Buff candidate。
- 当前不扩大 production whitelist。
- 当前不新增正式生产 Buff。

### Next

- 下一步建议进入 `Phase 3I-3：BuffCreateWizard 设计 / 合同阶段`。

## Phase 3H-8 - Production candidate intake plan closeout

### Current state

- 当前没有真实生产 Buff 候选。
- 当前 `Assets/Resources/BuffSystem/Buff` 下唯一 Resources production buff asset 是 `991001 Debug_CompressedParallel_TickSmoke`。
- `991001` 已在当前 View production path 中作为 smoke pilot 生效，但它是 smoke/debug pilot，不是正式玩法 Buff。
- 当前 production whitelist 继续保持单点 `991001`。
- 当前不扩大 production whitelist。
- 当前不实现 `BuffSystemProductionCandidateValidationRunner`。
- 只有负责人 / 策划提交真实 gameplay Buff 候选后，才进入候选审查。

### Candidate intake requirements

真实 gameplay Buff 候选进入 compressed production whitelist 前必须满足：

- `BuffType == parallel`
- `TriggerType == Tick`
- `ParallelStorageMode == CompressedExpiryFrameList`
- `Unlimited == false`
- `MaxStack <= CompressedParallelBuffLayerBuffer.Capacity`
- `EffectId` 已注册到 production registry
- 不依赖 EventTrigger compressed
- 不依赖逐层 runtime entity
- 不依赖 rollback-ready 结论

### Required validation before whitelist

候选进入 whitelist 前必须通过：

- asset 字段审查
- effect 注册审查
- EntityPerStack vs Compressed 行为一致性验证
- Add / Tick / Remove / Expire 验证
- TryGetBuff / GetBuffs 验证
- Source 匹配验证
- Stack policy 验证
- View production path 手动验证
- 性能观察
- 回退方案确认

### Rejection rules

- EventTrigger Buff 不进入 compressed whitelist。
- Unlimited Buff 不进入 compressed whitelist。
- `MaxStack` 超过 `CompressedParallelBuffLayerBuffer.Capacity` 的 Buff 不进入 compressed whitelist。
- 非 Tick Buff 不进入 compressed whitelist。
- 非 parallel Buff 不进入 compressed whitelist。
- Effect 未注册、依赖逐层 runtime entity、依赖 View 层直接枚举 runtime entity、缺少行为一致性验证或缺少回退方案的 Buff 不进入 compressed whitelist。

### Boundary

- 当前仍不能宣称 BuffSystem rollback-ready。
- 当前仍不能宣称更多生产 Buff 可以进入 compressed whitelist。
- 当前仍不能宣称 `991001` 是正式玩法 Buff。
- 当前仍不能宣称所有真实生产场景均已完整回归。
- 本阶段不修改 `BuffSystemCore.cs`、BuffSystem runtime、public API、production whitelist、validation whitelist、compressed eligibility、BuffConfigData asset、Runner、`SimulationInitializer.cs`、ECS Core、RollBackSystem、ViewSpawnSystem、Scene、Prefab 或 `.meta`。

### Closeout

- Compressed parallel production pilot 当前进入稳定等待状态。
- `991001` 单点 smoke pilot 保持。
- production whitelist 暂不扩大。
- 等待真实 gameplay Buff 候选提交后，再进入 Phase 3H-8A 候选审查。

## Phase 3H-6C - View production smoke pilot closeout

### Validated

- `SimulationInitializer.cs` 已完成最小 production composition path 接入，当前 View production path 使用 `BuffConfigDataLoader.Instance`、`BuffEffectRegistryBootstrap.RegisterProductionEffects(...)` 与 `BuffSystemCore.CreateForProduction(...)`。
- 接入后五个 BuffSystem Unity Editor 手动 Runner 均保持 PASS：
  - `BuffSystemPhase2AValidationRunner`
  - `BuffSystemCompressedParallelValidationRunner`
  - `BuffSystemRestoreHookValidationRunner`
  - `BuffSystemStorageBehaviorConsistencyRunner`
  - `BuffSystemStoragePerformanceRunner`
- `BuffConfigDataLoader` Root Path 为 `BuffSystem/Buff`，loader 成功加载 1 个 Buff definition。
- 当前加载到的 configId 为 `991001`，`TryGetDefinition(991001) = true`。
- `991001 Debug_CompressedParallel_TickSmoke` 的关键 definition 字段已确认：
  - `BuffType = parallel`
  - `TriggerType = Tick`
  - `ParallelStorageMode = CompressedExpiryFrameList`
  - `Unlimited = false`
  - `MaxStack = 3`
  - `DurationFrames = 120`
  - `TickIntervalFrames = 60`
  - `EffectId = 990101`
- `BuffEffectRegistryBootstrap` 注册的 `990101 DebugNoOpTickEffect` 已在 View production path 中可用，`EffectRegistered = true`。
- `991001` 的 eligibility、compressed gate、production whitelist 均通过：
  - `Eligibility = true`
  - `CompressedGate = true`
  - `WhitelistHit = true`
  - `WhitelistConfigIds = 991001`
  - `ShouldUseCompressedParallelExpected = true`
- 手动 Add `991001` 并 Tick 后，View production path 创建 compressed runtime：
  - `CompressedRuntime count = 1`
  - `EntityPerStackRuntime count = 0`
  - `Compressed Path = PASS`
- public query 结果已确认：
  - `TryGetBuff = true`
  - `GetBuffs count = 1`
  - `Current ConfigId View count = 1`
  - aggregate `BuffViewData` 可见，`Stack = 1`，`RemainingFrames = 119`。

### Conclusion

- 当前可以宣称：`991001 Debug_CompressedParallel_TickSmoke` 已在当前 View production path 中作为 smoke pilot 生效。
- 当前可以宣称：`991001` 命中 compressed production whitelist，并创建 `CompressedRuntime = 1`、`EntityPerStackRuntime = 0`。
- 当前可以宣称：接入后 BuffSystem 测试路径与 View production smoke pilot 均未发现 BuffSystem runtime 回归。

### Boundary

- 本阶段不修改 `BuffSystemCore.cs`、BuffSystem runtime、Runner、production whitelist、validation whitelist、compressed eligibility、ECS Core、RollBackSystem、Scene、Prefab、`.meta` 或 BuffConfigData asset。
- 当前不扩大 production whitelist。
- 当前不新增正式生产 Buff。
- `991001` 仍只作为 production pilot smoke asset，不视为正式玩法 Buff。
- 当前仍不能宣称 BuffSystem rollback-ready。
- 当前仍不能宣称更多生产 Buff 可以进入 compressed whitelist。
- 当前仍不能宣称所有真实生产场景均已回归。

### Known non-blocking warning

- Console 中存在 `[ViewSpawnSystem] Failed to spawn view. PrefabID = 1`。
- 该问题归类为 `ViewSpawnSystem / Prefab 映射问题`，本阶段不处理。
- 该 warning 不影响本阶段 BuffSystem compressed path 验证结论，因为 provider、definition、effect、whitelist、binding、runtime count 与 public query 均已验证通过。

### Meta note

- `BuffSystem_Changelog.md.meta` 曾出现 invalid GUID，导致 Unity 忽略对应文档 asset。
- 已删除 malformed `.meta`，并由 Unity 刷新后重新生成；当前 Console 不再显示该 invalid GUID 报错。
- 该问题只影响 BuffSystem 文档 asset 导入，不影响 BuffSystem runtime、Runner、production whitelist、compressed eligibility 或 `991001` View production smoke pilot 验证结论。
- 本阶段不手写 GUID，不处理其他 `.meta` 文件。

## Phase 3H-5A - Storage performance validation closeout

### Validated

- Phase 3H-5A 已完成，五个 BuffSystem Unity Editor 手动 Runner 均 PASS：
  - `BuffSystemPhase2AValidationRunner`：`========== Result: PASS ==========`
  - `BuffSystemCompressedParallelValidationRunner`：`========== Compressed Parallel Validation Result: PASS ==========`
  - `BuffSystemRestoreHookValidationRunner`：`========== Result: PASS ==========`
  - `BuffSystemStorageBehaviorConsistencyRunner`：`========== EntityPerStack vs Compressed Strategy Behavior Result: PASS ==========`
  - `BuffSystemStoragePerformanceRunner`：`========== EntityPerStack vs Compressed Performance Result: PASS ==========`
- `BuffSystemStoragePerformanceRunner` 的 PASS 只表示性能测量流程完成，不表示 Compressed 必须在所有场景都更快。

### Performance summary

- AddBuff + Tick 消费：Compressed / EntityPerStack 倍率分别为 `0.658`、`0.489`、`0.823`。
- Tick：Compressed / EntityPerStack 倍率分别为 `0.607`、`0.615`、`0.673`。
- RemoveEarliest / RemoveLatest / ClearAll 均显示 Compressed 更快；典型倍率包括 `0.393`、`0.505`、`0.636`。
- TryGetBuff 收益明显，典型倍率包括 `0.213`、`0.360`、`0.773`。
- GetBuffs(target) 在大规模场景下收益接近 0，但未见明显退化；典型倍率包括 `0.979`、`0.980`。
- EventTrigger 配置下 CompressedParallel 按设计 fallback EntityPerStack；Raise 结果仅用于确认测量流程，不作为 compressed runtime 性能收益依据。
- 本轮性能测量报告的所有测量项 `GCBytes = 0`。

### Conclusion

- BuffSystem 测试路径和 compressed parallel runtime 行为稳定。
- CompressedParallel 在 Add / Tick / Remove / TryGetBuff 上整体稳定优于 EntityPerStack。
- 本轮没有发现需要修改 `BuffSystemCore` 的 runtime 问题。

### Boundary

- 本阶段只归档验证结果，不修改 runtime、Runner、public API、`IBuffSystem`、compressed gate / whitelist / eligibility、asset、scene、prefab、`.meta`、View、ECS 或 RollBackSystem。
- `SimulationInitializer.cs` 仍未接入 `BuffConfigDataLoader + BuffEffectRegistryBootstrap + BuffSystemCore.CreateForProduction(...)`；该文件属于 View composition root，本阶段未修改，需要负责人批准后单独处理。
- 因此，不能因为本次 Runner PASS 宣称 `991001` 已在当前 View production path 中生效，也不能宣称当前 View production pilot 已验证通过。

## Phase 3G-State-Reconcile-Fix-A - BuffConfigDataLoader default Resources path

### Changed

- `BuffConfigDataLoader` 默认 Resources Root Path 已从 `_Scripts/FrameWork/BuffSystem/BuffConfigDataCollection` 收敛为 `BuffSystem/Buff`。
- 当前 production pilot asset `Debug_CompressedParallel_TickSmoke.asset` 位于 `Assets/Resources/BuffSystem/Buff`，`Resources.LoadAll<BuffConfigData>("BuffSystem/Buff")` 可覆盖该默认路径。

### Boundary

- 本阶段只修复 BuffSystem 侧默认路径，不修改 View composition root、ECS、RollBackSystem、scene、prefab 或 `.meta`。
- `SimulationInitializer.cs` 仍未接入 `BuffConfigDataLoader + BuffEffectRegistryBootstrap + BuffSystemCore.CreateForProduction(...)`；该文件属于 View composition root，本阶段未修改，需要负责人批准后单独处理。
- 因此，不能因为本次默认路径修复就宣称 `991001` 已在当前 View production path 中生效。
- 未修改 public API、public constructor、`IBuffSystem`、compressed gate / whitelist / eligibility，也未新增 production Buff。

## Phase 3H-3C - Restore hook validation runner

### 新增

- 新增 `BuffSystemRestoreHookValidationRunner`，作为 `BuffSystemCore.OnWorldRestored(World world)` 的 Unity Editor 手动验证入口。
- Runner 验证 EntityPerStack runtime 在 `OnWorldRestored` 前后 `TryGetBuff` / `GetBuffs` 查询结果一致。
- Runner 验证 compressed runtime 在 `OnWorldRestored` 后仍可通过 aggregate ViewData 查询，且 `layerCount` 与 `Stack` 一致。
- Runner 验证 EventTrigger Buff 在 `OnWorldRestored` 后仍可通过 `Raise<TEvent>` 触发对应事件 Effect。
- Runner 通过手动修改 runtime component 模拟 World restore 后的组件真状态变化，验证 ViewCache 不返回 stale data。
- Runner 验证 `OnWorldRestored` 本身不会触发 `OnApply` / `OnTick` / `OnRemove` / `OnEvent`。

### 保持不变

- 未修改 RollBackSystem、ECS、Contracts、Utility、PoolSystem、public API、`IBuffSystem`、compressed gate / whitelist、asset、scene、prefab 和 `.meta` 文件。
- 本阶段不接入 RollBackSystem，不实现 snapshot restore，不宣称 BuffSystem rollback-ready。

## Phase 3H-3B - Rollback restore transient cache hook

### 新增

- 新增 internal `BuffSystemCore.OnWorldRestored(World world)`，用于后续 RollBackSystem 完成 World restore 后整理 BuffSystem 派生缓存。
- 该 hook 只清理 BuffSystem transient state，并从恢复后的 ECS World 重新捕获 runtime entity、重建 lookup。
- ECS Component 仍是唯一运行时真状态。该 hook 不 AddBuff、不 RemoveBuff、不 DestroyEntity、不 SetComponent 修改业务状态、不执行生命周期 Effect、不触发事件、不执行 Tick。
- 调用方必须保证外部 RollBackSystem 已在稳定帧边界完成 World restore。
- Entity ID / Version 的稳定性必须由外部 snapshot restore 实现保证。

### 清理 / 重建

- 清空 command queue、lifecycle effect queue、pending remove 状态、runtime frame snapshot、compressed runtime frame snapshot、请求临时列表、event candidate、runtime lookup、compressed runtime lookup、ViewCache、EventRuntimeIndex 和 frame guard。
- 从恢复后的 World 重新捕获 `BuffRuntimeComponent` 与 `CompressedParallelBuffRuntimeComponent` entity。
- 重建 EntityPerStack 与 compressed runtime lookup。
- 标记 ViewCache 与 EventRuntimeIndex dirty。

### 保持不变

- 未修改 RollBackSystem、ECS snapshot、`WorldRollbackAdapter`、`RollbackCoordinator`、Demo `WorldSnapshot`、public API、`IBuffSystem`、compressed gate / whitelist、asset、scene、prefab 和 `.meta` 文件。
- 本阶段不宣称 BuffSystem rollback-ready。Demo `WorldSnapshot` 仍不能作为 Buff runtime rollback-ready 依据，因为它不能保证稳定的 Entity ID / Version。

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

- Updated the original ECS Debugger `Buff 调试` page so Add / Remove button logs are INFO queued messages instead of PASS validation records.
- Added `添加 Buff 并 Tick 一帧`, `添加 3 层 Buff 并 Tick 一帧`, and `移除 Buff 并 Tick 一帧` convenience buttons.
- Added `添加 Buff 并 Tick 两帧` and `添加 3 层 Buff 并 Tick 两帧` for the capture-after-command timing case.
- Tick-driven refresh now performs the compressed-path validation after the queued command has been consumed by `BuffSystemCore`.
- Split compressed runtime validation from ViewData visibility. A runtime state of `CompressedRuntime count == 1` and `EntityPerStack count == 0` is shown as compressed path `PASS`; if `TryGetBuff` is still false, the page reports that ViewData is waiting for the next capture frame instead of treating the whole check as failed.
- `添加 3 层 Buff 并 Tick 两帧` is the recommended aggregate ViewData stack validation path for `Stack = 3`.
- Added a copyable plain-text debug snapshot area and `复制日志到剪贴板` button using `EditorGUIUtility.systemCopyBuffer`.

### Preserved

- `BuffSystemCore`, public APIs, compressed gate / whitelist logic, runtime logic, assets, scenes, prefabs, and `.meta` files were not modified.
- The original `Window / ECSFrameWork / World Debugger` remains the main debug entry.

## Phase 3G-4C - ECS World Debugger Chinese layout pass

### Changed

- Returned the main debug entry to the original IMGUI `Window / ECSFrameWork / World Debugger`.
- Localized the original ECS debugger pages and toolbar with Chinese-first labels while preserving English technical terms.
- Kept all original pages and existing functionality: `总览 Overview` / `实体 Entities` / `系统 Systems` / `原型 ArcheTypes` / `组件仓库 Stores` / `单例 Singletons` / `世界事件 Events` / `命令 Commands` / `Buff 调试`.
- Disabled the previous Odin experiment window menu to avoid opening a reduced-function duplicate debugger.
- The original `Buff 调试` page still keeps pilot `configId = 991001` as the default target and exposes Add / Remove / fixed-frame Tick / query refresh controls.

### Preserved

- `BuffSystemCore`, `IBuffSystem`, public constructors, compressed gate / whitelist logic, event Effect hot path, runtime Buff logic, assets, scenes, prefabs, and `.meta` files were not modified.
- The original `ECSWorldDebuggerWindow` remains the only recommended main entry.
- Debug Entity creation still uses the current `World.CreateEntity()`; no Unity `GameObject` is created as an ECS Entity.

### Manual validation focus

- Open `Window / ECSFrameWork / World Debugger`.
- Select the `Buff 调试` page.
- For `configId = 991001`, compressed success means current ConfigId `CompressedRuntime count == 1` and current ConfigId `EntityPerStack count == 0`.

## Phase 3G-4B-Fix - ECS Debugger Buff debug page

### Changed

- Added a real `Buff 调试` page to `ECSWorldDebuggerWindow` Pages.
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
- Select the `Buff 调试` page from the left Pages list.
- For `configId = 991001`, compressed success means current ConfigId `CompressedRuntime count == 1` and current ConfigId `EntityPerStack count == 0`.

## Phase 3G-4B - Odin Chinese Buff debug panel

### Changed

- Added Odin Inspector groups to `LogicFrameDebugPanel` for the `BuffSystem 压缩 Buff 调试面板`.
- Added Chinese labels, buttons, read-only result fields, runtime type statistics, `GetBuffs(target)` table, and recent operation logs.
- Kept the existing IMGUI Buff debug panel as a runtime fallback.
- Extended `SimulationDebugProbe` to expose debug `GetBuffs(target)` rows for Odin TableList display.

### Preserved

- `BuffSystemCore`, `IBuffSystem`, public constructors, compressed gate / whitelist logic, event Effect hot path, runtime Buff logic, assets, scenes, prefabs, and `.meta` files were not modified.
- Debug Entity creation still uses the current `World.CreateEntity()` through `SimulationDebugProbe`; no GameObject is created as an ECS Entity.
- Source defaults to target, and Add / Remove / Query use the same target/source pair.

### Manual validation focus

- In Play Mode, select the object containing `LogicFrameDebugPanel` and use the Odin group `BuffSystem 压缩 Buff 调试面板`.
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

### 新增

- 新增 `ApplyCompressedParallelAdd`、`ApplyCompressedParallelRemove`、`CreateCompressedParallelRuntime` 等 compressed runtime dormant helper。
- 新增 compressed lookup helper：`TryGetCompressedRuntimeEntity`、`RegisterCompressedRuntimeLookup`、`RemoveCompressedRuntimeLookup`。
- 新增单层 snapshot helper：`CreateCompressedLayerSnapshot`。
- 新增 Append、RefreshEarliest、RefreshAll、ReplaceEarliestWhenFull、RemoveEarliest、RemoveLatest、ClearAll 对应的 compressed helper。

### 保持不变

- `ShouldUseCompressedParallel` 仍返回 false，`CompressedExpiryFrameList` 不会实际生效。
- 未修改 `ApplyAddCommand` 或 `ApplyRemoveCommand`，compressed helper 不会被现有主流程调用。
- 未接入 Tick、自然到期 Expire、TryGetBuff、GetBuffs 或 ViewData。
- 未扩展 `BuffEffectRequest`，未修改 `BuffEffectContext` 或 public API。
- 当前 EntityPerStack 行为不变。

### Replace Effect 顺序

压缩 helper 中 ReplaceEarliestWhenFull 的状态变更按策略执行，但生命周期 Effect Flush 仍由 Phase 2A phaseOrder 决定。文档和测试不得假设同帧 Replace 中 Remove Effect 一定早于 Apply Effect。

## Phase 3C-1 - Compressed parallel preparation helpers

### 新增

- `BuffSystemCore` 新增 `_compressedRuntimeEntityByKey` lookup cache 字段，并只在清理路径中清空；本阶段不在主流程读写它。
- 新增 `ShouldUseCompressedParallel`，本阶段保持返回 false，确保 `CompressedExpiryFrameList` 不会实际生效。
- 新增 `IsCompressedParallelEligible`，规则为 parallel buff、`CompressedExpiryFrameList`、Tick 触发、非 Unlimited、`MaxStack <= CompressedParallelBuffLayerBuffer.Capacity`。
- `CompressedParallelBuffLayerBuffer` 新增 `RemoveAt`、`FindEarliestIndex`、`FindLatestIndex`、`FindExpiredEarliestIndex`、`AppendLayer`、`RefreshLayer` helper。

### 保持不变

- 不接入 Add、Refresh、Remove、Tick、Query 或 EffectRequest 主流程。
- 不扩展 `BuffEffectRequest` 或 `BuffEffectContext`。
- 不修改 public BuffSystem API、`IBuffEffectExecutor` 或 `IBuffEventEffectExecutor<TEvent>`。
- 当前 EntityPerStack 行为不变。

### Tick / Expire 基准

当前 EntityPerStack 的 `TickRuntimeBuffs` 顺序是先 Tick，再 Expire：先推进 `elapsedFrames` 并在满足间隔时 Queue `OnTick`，随后非永久 Buff 才扣减 `remainingFrames` 并处理自然到期。后续压缩模式正式接入时必须对齐该顺序。

## Phase 3B - Parallel Buff compressed storage skeleton

### 新增

- 新增 `ParallelBuffStorageMode.EntityPerStack = 0`。
- 新增 `ParallelBuffStorageMode.CompressedExpiryFrameList = 1`。
- `BuffConfigData` 新增并行 Buff 存储模式配置字段，默认值为 `EntityPerStack`。
- `BuffDefinition` 新增 `ParallelStorageMode` 只读字段，并通过构造函数尾部可选参数保持旧调用兼容。
- 新增 `CompressedParallelBuffLayer`、`CompressedParallelBuffRuntimeComponent` 和固定容量值类型 `CompressedParallelBuffLayerBuffer`。

### 保持不变

- Phase 3B 不修改 `BuffSystemCore.cs`。
- Phase 3B 不接入 Add、Refresh、Remove、Tick、Expire、TryGetBuff 或 GetBuffs 主流程。
- 当前所有并行 Buff 仍走 EntityPerStack。
- 即使配置选择 `CompressedExpiryFrameList`，当前运行时也不会启用压缩逻辑。
- Phase 2A 生命周期 EffectRequest Pipeline 和事件型 Effect 热路径不变。
- 不使用 `Time.time`、`Time.deltaTime`、`float expiry`、GameObject runtime、MonoBehaviour runtime 或 runtime ScriptableObject Effect。

### 后续

Phase 3C 才会单独设计 `CompressedExpiryFrameList` 如何接入 Add、Refresh、Remove、Expire、Query 与生命周期 EffectRequest Pipeline。

## Phase 2A - Lifecycle EffectRequest Pipeline

### 新增

- 生命周期 Effect 请求队列，覆盖 `Apply / Refresh / StackChanged / Tick / Remove`。
- Remove 延迟物理销毁：Runtime 立即退出有效 Buff 语义，`OnRemove` Flush 后再 `DestroyEntity`。
- 显式生命周期 phase order：`Apply=0, Refresh=1, StackChanged=2, Tick=3, Remove=4`。

### 行为变化

生命周期 Effect 由立即执行改为本帧末尾 Flush。排序规则统一为：

```text
frameNumber -> phaseOrder -> priority -> runtimeHandle -> Entity.ID -> Entity.Version -> sequence
```

Flush 期间新增的 `AddBuff` / `RemoveBuff` 不递归处理，会进入 `_queuedCommands`，由下一次 `BuffSystemCore.Tick -> ConsumeQueuedCommands` 消费。

### 保持不变

- `IBuffEffectExecutor` public API 不变。
- `BuffEffectContext` public API 不变。
- `IBuffEventEffectExecutor<TEvent>` 泛型事件热路径不变。
- 不引入 `GameObject`、`MonoBehaviour`、`Time.time`、`Time.deltaTime` 或 runtime `ScriptableObject Effect`。

## Phase 1.1 - Documentation strictness

### 变更影响示例

`ResetDurationOnly` 用于重复添加时只刷新持续时间，不改变当前层数。下面的示例中，目标已有 2 层 Buff，再次添加 1 层后仍保持 2 层，但持续帧与 Tick 计数会重置。

```csharp
// before: stack = 2, remainingFrames = 40, elapsedFrames = 20, ticks = 1
definition.NormalStackPolicy = NormalBuffStackPolicy.ResetDurationOnly;
buffSystem.AddBuff(new AddBuffCommand(target, configId: 1001, source, stack: 1));

// after: stack = 2, remainingFrames = definition.DurationFrames,
//        elapsedFrames = 0, ticks = 0
```

`RefreshDuration` 保留旧的加层语义。重复添加时仍会按旧规则尝试增加层数，但刷新持续时间后会同步重置 Tick 计数，避免周期效果沿用刷新前的计时状态。

```csharp
// before: stack = 1, elapsedFrames = 29, ticks = 0
definition.NormalStackPolicy = NormalBuffStackPolicy.RefreshDuration;
definition.TickIntervalFrames = 30;
buffSystem.AddBuff(new AddBuffCommand(target, configId: 1002, source, stack: 1));

// after: stack = ClampStack(2), remainingFrames = definition.DurationFrames,
//        elapsedFrames = 0, ticks = 0
```

普通 Buff 的部分减层行为本阶段暂未变更。当前 `RemoveBuffCommand` 只移除部分层数时，仍会保留既有行为：减少 stack 后将 `remainingFrames` 刷新为当前 `durationFrames`。如果后续要改成“减层不刷新剩余时间”，需要单独审核。

## Phase 1 - Low-risk semantic fixes

### 新增

- 新增 `NormalBuffStackPolicy.ResetDurationOnly = 5`。
- 新增标准文档集合，用于记录 API、叠层策略、Effect、事件、并行 Buff、迁移说明、样例和变更历史。

### 行为变化

- `ResetDurationOnly` 重复添加时不改变当前层数，只重置持续时间与 Tick 计数。
- `RefreshDuration` 刷新持续时间时，现在同步重置 `elapsedFrames` 和 `ticks`。
- `AddStackAndRefreshDuration` 刷新持续时间时，现在同步重置 `elapsedFrames` 和 `ticks`。
- 并行 Buff 的 `RefreshEarliest` 和 `RefreshAll` 刷新层持续时间时，现在同步重置该层 `elapsedFrames` 和 `ticks`。

### 保持不变

- 旧枚举值顺序和整数值保持不变。
- `RefreshDuration` 是否加层的旧语义保持不变。
- `RemoveBuffCommand` 的普通 Buff 部分减层语义保持不变。
- ViewCache dirty 行为保持默认安全路径；本阶段只为 `WriteRuntimeComponent` 预留 `markViewDirty` 参数，现有调用默认仍标记 dirty。

### 禁止项自查

- 未引入 `GameObject` 运行时依赖。
- 未引入 `MonoBehaviour` 运行时依赖。
- 未引入 `Time.time` 或 `Time.deltaTime`。
- 未引入 `ScriptableObject` runtime effect。

### 迁移说明

FrameWork2 的 `ResetRuntimeBuffStackUpStrategy` 迁移为第一套 ECS BuffSystem 的 `NormalBuffStackPolicy.ResetDurationOnly`。迁移后使用固定帧字段 `elapsedFrames`、`ticks` 和 `remainingFrames` 表达刷新语义。

## Phase 3F-4C - Compressed Parallel Validation Runner V1

### 新增

- 新增 `BuffSystemCompressedParallelValidationRunner` Unity Editor 手动验证脚本。
- Runner 使用 `BuffSystemCore.CreateForCompressedParallelValidation(definitionRegistry, effectRegistry)` 创建 gate=true 测试实例，不使用反射，不新增 public gate 入口。

### 覆盖范围

- gate=true + `CompressedExpiryFrameList` 的基础 Append 路径。
- Append 多层后的聚合 ViewData：`Stack`、`RemainingFrames`、`RuntimeHandle`。
- `EventTrigger`、`Unlimited`、`MaxStack > CompressedParallelBuffLayerBuffer.Capacity` fallback 到 `EntityPerStack`。
- gate=false 默认构造路径回归：即使配置 `CompressedExpiryFrameList`，仍走 `EntityPerStack`。

### 暂未覆盖

- `RefreshEarliest`、`RefreshAll`、`ReplaceEarliestWhenFull`。
- `RemoveEarliest`、`RemoveLatest`、`ClearAll`。
- Tick / Expire。
- PendingRemove / Destroy。
- duration / forever 混合内部状态。

### 保持不变

- 正式运行时默认 gate 仍关闭。
- public API 和 public 构造函数不变。
- `EntityPerStack` 默认路径不变。
- 事件型 Effect 热路径不变。

## Phase 3F-4D - Compressed Parallel Validation Runner V2

### 新增

- 在 `BuffSystemCompressedParallelValidationRunner` 中追加 Refresh / Remove policy 验证。
- 新增 `RefreshEarliest`、`RefreshAll`、`RemoveEarliest`、`RemoveLatest`、`ClearAll` 测试组。

### 覆盖范围

- `RefreshEarliest` 只刷新最早层，并验证 `layerId / layerRuntimeHandle` 保持不变。
- `RefreshAll` 刷新所有已有层，并验证 `ViewData.Stack` 不变。
- `RemoveEarliest` 移除最早层后，剩余层仍可查询。
- `RemoveLatest` 移除最新层后，剩余层仍可查询。
- `ClearAll` 后 public 查询不再显示该 Buff。
- gate=false 默认构造路径仍回归验证为 `EntityPerStack`。

### 暂未覆盖

- Tick / Expire。
- PendingRemove / Destroy 深度验证。
- `ReplaceEarliestWhenFull`。
- duration / forever 混合内部状态。

### 保持不变

- 未修改 `BuffSystemCore.cs`。
- 未修改 public API 或 public 构造函数。
- 未修改正式运行时默认 gate。
- 未修改事件型 Effect 热路径。

## Phase 3F-8 - Compressed Parallel Docs Consolidation

### 新增

- 收敛 `CompressedExpiryFrameList` 正式启用前工程说明。
- 在 ParallelBuff、API、EffectPipeline、Examples、Migration 和 Changelog 文档中补充 compressed parallel 当前状态、gate 限制、验证结果和风险项。

### 当前状态

- 正式 public constructor 路径 gate 默认关闭。
- 正式运行时仍默认 `EntityPerStack`。
- `CreateForCompressedParallelValidation(...)` 是 internal test-only factory，只供 validation runner 使用。
- Phase 3G 前不建议业务代码直接使用 validation factory。
- Phase 3G 前不建议全项目直接打开 compressed gate。

### eligibility 条件

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
compressed gate == enabled
```

### fallback 条件

```text
gate=false
EventTrigger parallel buff
Unlimited == true
MaxStack > CompressedParallelBuffLayerBuffer.Capacity
任何不满足 eligibility 的配置
```

fallback 后仍走 `EntityPerStack`。

### 口径说明

ViewData 口径：

```text
Stack = active layerCount
duration RemainingFrames = min(expireFrame - currentFrame)
forever RemainingFrames = -1
RuntimeHandle = min(active layerRuntimeHandle)
```

Tick / Effect snapshot 口径：

```text
Tick snapshot RemainingFrames = expireFrame - currentFrame + 1
Remove snapshot RemainingFrames = 0
forever snapshot remainingFrames = 0
```

ViewData 口径和 Tick snapshot 口径不能混用。

### PendingRemove / Replace 说明

- 最后一层 Remove / Expire / ClearAll 后，compressed runtime container 进入 pending remove。
- pending remove 使用 `compressedRuntimeHandle`。
- container pending remove 不额外触发聚合 Remove Effect。
- layer Remove 使用 `layerRuntimeHandle`。
- pending remove 后 `TryGetBuff / GetBuffs` 不显示。
- Destroy 前 defensive 清理 `_compressedRuntimeEntityByKey`。
- `ReplaceEarliestWhenFull` 未满层时 Append，满层时移除最早层并追加新层。
- Replace 新层生成新的 `layerId / layerRuntimeHandle`，未替换层 identity 保持。
- 不假设 Remove callback 一定早于 Apply callback；Effect Flush 顺序仍由 Phase 2A phaseOrder 决定。

### 已验证清单

- Append / ViewData / fallback / gate=false。
- RefreshEarliest / RefreshAll。
- RemoveEarliest / RemoveLatest / ClearAll。
- duration=1 / duration=2 Tick + Expire。
- forever layer。
- PendingRemove / Destroy。
- compressed lookup cleanup。
- Query 只读语义。
- ReplaceEarliestWhenFull。
- `MaxStack == CompressedParallelBuffLayerBuffer.Capacity`。
- Phase 2A Runner 回归 PASS。
- Compressed Parallel Validation V1/V2/V3/V4 PASS。

### 未覆盖 / 风险项

- mixed duration / forever internal-state 尚未专项验证。
- 性能测试尚未完成。
- 回滚快照验证尚未完成。
- 正式启用前仍需 Phase 3G 小范围开启策略。
- 不建议直接全项目打开 compressed gate。

### 后续

- Phase 3G：小范围正式启用 eligible Tick 型 parallel buff。
- Phase 3H：性能对比、回滚快照验证、行为一致性验证。
- Phase 3I：评估是否扩大到更多 parallel buff 类型。

## Phase 3G-1 - Compressed whitelist gate skeleton

### 新增

- 在 compressed global gate 和 eligibility 之间新增 configId whitelist 门禁。
- `ShouldUseCompressedParallel` 现在必须同时满足：

```text
_enableCompressedParallelRuntime
&& IsCompressedParallelWhitelisted(definition.ConfigId)
&& IsCompressedParallelEligible(definition)
```

### 生产默认行为

- public constructor 仍为 gate=false。
- public constructor 的 whitelist 为空。
- 不启用任何生产 Buff。
- 所有生产 Buff 默认仍走 `EntityPerStack`。
- 非白名单 Buff 即使 eligible，也 fallback 到 `EntityPerStack`。

### validation runner

- `CreateForCompressedParallelValidation(...)` 保持 internal test-only。
- validation factory 使用测试白名单，仅覆盖 `BuffSystemCompressedParallelValidationRunner` 当前测试 configId。
- Runner 仍可验证 compressed path。

### 保持不变

- 未修改 public API。
- 未修改 public constructor 签名。
- 未修改 `BuffConfigData`。
- 未修改 `BuffDefinition` public 字段。
- 未修改事件型 Effect 热路径。
- Phase 3G-2 才会单独选择第一个生产试点 configId。

## Phase 3F-4F - Compressed Parallel Validation Runner V4

### 新增

- 在 `BuffSystemCompressedParallelValidationRunner` 中追加 `ReplaceEarliestWhenFull` 与 capacity 边界验证。

### 覆盖范围

- `ReplaceEarliestWhenFull` 未满层时追加新层，不替换旧层。
- `ReplaceEarliestWhenFull` 满层时移除最早层并追加新层，最终 `layerCount` 保持 `MaxStack`。
- Replace 后记录并验证 layer identity：被替换层消失，未替换层保持，新层生成新的 `layerId / layerRuntimeHandle`。
- Replace 后 public 查询仍只有一个 aggregate ViewData。
- Replace Effect 只验证必要 `Apply / Remove` 存在，不假设 Remove 一定早于 Apply。
- `MaxStack == CompressedParallelBuffLayerBuffer.Capacity` 仍走 compressed runtime。

### 暂未覆盖

- duration / forever 混合内部状态。
- 性能测试。
- 回滚快照测试。

### 保持不变

- 未修改 `BuffSystemCore.cs`。
- 未修改 public API 或 public 构造函数。
- 未修改正式运行时默认 gate。
- 未修改事件型 Effect 热路径。

## Phase 3F-4E - Compressed Parallel Validation Runner V3

### 新增

- 在 `BuffSystemCompressedParallelValidationRunner` 中追加 Tick / Expire / PendingRemove / Destroy 深度验证。
- 新增 `durationFrames = 1`、`durationFrames = 2`、forever compressed layer 测试组。

### 覆盖范围

- `durationFrames = 1`：F1 Apply，F2 Tick，同帧 Remove。
- `durationFrames = 2`：F2 Tick 不 Remove，F3 Tick 后同帧 Remove。
- Tick snapshot `RemainingFrames` 使用 `expireFrame - currentFrame + 1`。
- Remove snapshot `RemainingFrames = 0`。
- forever layer 可 Tick，不自然 Expire；ViewData `RemainingFrames = -1`。
- 最后一层 Expire 后，`OnRemove` 中 public 查询不可见。
- Destroy 后 compressed runtime 不再存在，并且同配置可重新 Add，验证 lookup 清理。
- 不额外触发 compressed runtime container 聚合 Remove Effect。

### 暂未覆盖

- `ReplaceEarliestWhenFull`。
- duration / forever 混合内部状态。
- 性能测试。
- 回滚快照测试。

### 保持不变

- 未修改 `BuffSystemCore.cs`。
- 未修改 public API 或 public 构造函数。
- 未修改正式运行时默认 gate。
- 未修改事件型 Effect 热路径。
