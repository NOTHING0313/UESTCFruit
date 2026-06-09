# BuffSystem Authoring Guide

## 1. 工具入口

BuffSystem authoring 工具统一入口为：

```text
Tools / BuffSystem / Authoring Hub
```

当前 Hub 包含三个 tab：

```text
Validator
Create Buff
Effect Template
```

- `Validator`：扫描 `Assets/Resources/BuffSystem/Buff` 下的 BuffConfigData，检查字段、Effect 注册状态、compressed eligibility 和候选分类。
- `Create Buff`：创建 BuffConfigData 草稿 asset，提供 ConfigId / EffectId / compressed eligibility 预检查。
- `Effect Template`：生成 Effect `.cs` 草稿模板，提供 EffectId 注册检查和 registry snippet 复制。

## xNode 候选图工作流

如果需要先做图形化候选设计和风险审查，可以使用：

```text
Assets / Create / BuffSystem / Buff Candidate Graph
```

然后在：

```text
Tools / BuffSystem / Authoring Hub
```

顶部的 `候选图联动 / Candidate Graph Link` 中选择该图。

推荐分工：

```text
BuffCandidateGraph：用于可视化设计、候选审查、风险提示。
Authoring Hub：用于把候选图字段导入 Create Buff / Effect Template 表单。
Validator：用于扫描真实 BuffConfigData asset。
```

详细流程见：

```text
Assets/_Scripts/FrameWork/BuffSystem/Docs/BuffSystem_xNodeAuthoringGraph.md
```

候选图不会自动创建 BuffConfigData，不会自动生成 Effect，不会自动注册 Effect，不会自动加入 whitelist，也不会证明 rollback-ready。

## 2. 推荐制作流程：从零制作一个 Buff

### Step 1：规划 Buff

制作前先确认以下信息：

```text
ConfigId
Buff Name
BuffType
TriggerType
ParallelStorageMode
Unlimited
MaxStack
Duration
TickTime
EffectId
是否需要 compressed storage
是否只是 Debug / Smoke
是否需要 View 表现
是否依赖 EventTrigger
是否需要 rollback 支持
```

注意：`rollback-ready` 不能由单个 Buff 或 Effect 自行声明。它依赖外部 RollBackSystem、World snapshot / restore 语义、Entity ID / Version 稳定性，以及 BuffSystem restore hook 对接结果。

### Step 2：用 Effect Template 生成 Effect 草稿

入口：

```text
Authoring Hub -> Effect Template
```

当前字段：

```text
EffectId
Effect Class Name
Effect Display Name / Note
Target Folder
Namespace
Target File
Callback Selection
OnApply
OnTick
OnRemove
OnRefresh
OnStackChanged
```

当前默认值：

```text
EffectId = 0
Effect Class Name = NewBuffEffect
Target Folder = Assets/_Scripts/FrameWork/BuffSystem/Effects
Namespace = BuffSystem
Target File = Assets/_Scripts/FrameWork/BuffSystem/Effects/NewBuffEffect.cs
OnApply = true
OnTick = true
OnRemove = true
OnRefresh = false
OnStackChanged = false
```

当前按钮 / 操作项：

```text
Validate
Generate Template
Copy Registry Snippet
Open Effect Folder
Clear
```

推荐流程：

1. 输入 `EffectId`。
2. 输入 `Effect Class Name`。
3. 勾选需要生成的 callbacks。
4. 点击 `Validate`。
5. 确认 `EffectId` 未注册，且可以生成。
6. 点击 `Generate Template`。
7. 手动实现 Effect 逻辑。
8. 使用 `Copy Registry Snippet` 复制注册代码。

边界：

```text
工具不会自动修改 BuffEffectRegistryBootstrap。
工具不会自动注册 Effect。
工具不会自动加入 whitelist。
工具不会创建 BuffConfigData asset。
生成 Effect 模板不代表 production 可用。
```

`Copy Registry Snippet` 只复制 `registry.Register(...)` 片段到剪贴板，不会修改任何代码。`Open Effect Folder` 只打开目标目录，不代表生成模板，不会注册 Effect，也不会修改 `BuffEffectRegistryBootstrap`。

### Step 3：人工注册 Effect

Effect 模板生成并实现后，需要人工将 registry snippet 加入：

```text
BuffEffectRegistryBootstrap.RegisterProductionEffects(...)
```

注册后需要重新编译，并重新运行 `Authoring Hub -> Validator` 检查 Effect 注册状态。

### Step 4：用 Create Buff 创建 BuffConfigData 草稿

入口：

```text
Authoring Hub -> Create Buff
```

当前字段：

```text
ConfigId
Buff Name
Description
Save Path
Target Asset
BuffType
TriggerType
ParallelStorageMode
Unlimited
MaxStack
Duration
TickTime
StackUpPolicy
StackDownPolicy
EffectId
EffectRegistered
```

当前默认值：

```text
ConfigId = 100001
Buff Name = NewBuff
Save Path = Assets/Resources/BuffSystem/Buff
Target Asset = Assets/Resources/BuffSystem/Buff/100001_NewBuff.asset
BuffType = parallel
TriggerType = Tick
ParallelStorageMode = EntityPerStack
Unlimited = false
MaxStack = 1
Duration = 1
TickTime = 1
EffectId = 0
EffectRegistered = Unknown
```

当前按钮 / 操作项：

```text
Validate
Create Draft Asset
Open Authoring Validator
Cancel / Close
```

当前工具实际按钮是 `Cancel / Close`，没有重置字段的 `Clear` 按钮。若未来需要真正的重置按钮，应另开 UX 改进阶段。

推荐流程：

1. 输入 `ConfigId`。
2. 输入 `Buff Name / Description`。
3. 设置 Buff 行为字段。
4. 填写 `EffectId`。
5. 点击 `Validate`。
6. 确认没有 blocking error。
7. 创建 BuffConfigData asset。

边界：

```text
工具不会自动加入 whitelist。
工具不会自动把 Debug / Smoke Buff 变成正式 Buff。
工具不会自动注册 Effect。
工具不会修改 runtime。
工具不会保存 scene。
```

### Step 5：用 Validator 检查

入口：

```text
Authoring Hub -> Validator
```

检查重点：

```text
ConfigId 是否重复
Effect 是否已注册
compressed eligibility 是否满足
是否 Smoke / Debug
是否 Eligible Candidate
是否 Invalid
```

Validator 是 authoring 辅助工具，不替代 Runner、场景验证或负责人审批。

## 3. Compressed Parallel Buff 候选标准

当前 Editor 检查 compressed eligibility 的口径为：

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
```

满足 eligibility 不等于自动进入 production whitelist。进入 whitelist 前仍需候选审查、Runner、真实 View production path 场景验证和人工批准。

以下类型当前不应进入 compressed whitelist：

```text
EventTrigger Buff
Unlimited Buff
MaxStack 超过 compressed capacity 的 Buff
非 Tick Buff
非 parallel Buff
依赖逐层 runtime entity 的 Buff
依赖 View 层直接枚举 runtime entity 的 Buff
```

## 4. Effect 编写约束

Effect 编写应遵守：

```text
使用 Buff runtime / SimulationContext 的帧信息作为逻辑时间依据
优先写 ECS 状态
不要直接依赖 View 表现层
不要直接依赖 Unity 对象组件
不要在 Effect 中宣称 rollback-ready
不要把 Debug / Smoke Effect 当作正式玩法 Effect
```

Effect 进入 production 前，需要完成实现审查、registry 注册审批、Validator 检查和相关 Runner / 场景验证。

## 5. ID 建议

当前已知 ID：

```text
991001 当前是 production smoke pilot Buff
990101 当前是 DebugNoOpTickEffect
```

注意：

```text
不要复用已有 ConfigId
不要复用已注册 EffectId
Debug / Smoke ID 不应直接作为正式 gameplay ID
```

正式 ID 分段规范待项目负责人确认。当前文档不擅自发明 production ID 规则。

## 6. 工具不会自动做什么

Authoring 工具不会自动执行以下操作：

```text
不会自动注册 Effect
不会自动修改 BuffEffectRegistryBootstrap
不会自动加入 whitelist
不会自动修改 runtime
不会自动保存 scene
不会自动创建正式玩法 Buff
不会证明 rollback-ready
不会替代 Runner / 场景验证
```

## 7. 常见错误与处理

### ConfigId duplicate

说明：目标 ConfigId 已被现有 BuffConfigData 使用。

处理：更换 ConfigId，或确认旧 asset 是否应废弃。不要直接覆盖已有生产 ID。

### EffectId <= 0

说明：EffectId 未填写或无效。

处理：填写有效 EffectId，并确认该 Effect 已实现或准备生成模板。

### EffectId 未注册

说明：Buff 引用了 EffectId，但 production registry 中未发现注册。

处理：实现 Effect 后，人工将 registry snippet 加入 `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)`，重新编译并重新运行 Validator。

### EffectId 已注册但类名不同

说明：EffectId 可能已经绑定到其他 Effect 类，继续生成同 ID 模板会造成语义冲突。

处理：不要复用该 EffectId。需要负责人确认是否改用新 EffectId，或是否复用已有 Effect。

### Buff 被识别为 Smoke / Debug

说明：ConfigId 或名称显示它是调试 / smoke 用资产。

处理：不要将该 asset 当作正式 gameplay Buff。正式 Buff 需要独立候选审查。

### CompressedEligibility = false

说明：当前字段组合不满足 compressed storage 条件。

处理：查看 Validator 输出的不满足原因。不要为了进入 whitelist 盲目修改玩法语义字段。

### EventTrigger 想进入 compressed whitelist

说明：EventTrigger 当前按设计 fallback EntityPerStack。

处理：保持 EntityPerStack，不进入 compressed whitelist。

### Unlimited Buff 想进入 compressed whitelist

说明：Unlimited 与当前 compressed eligibility 不兼容。

处理：保持 EntityPerStack，或另开设计阶段评估语义，不要直接加入 whitelist。

### 生成 Effect 模板后忘记注册

说明：Effect `.cs` 存在不代表 production registry 已注册。

处理：手动注册到 `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)`，重新编译并运行 Validator。

### 注册 Effect 后忘记重新运行 Validator

说明：Authoring 状态可能仍是旧结果。

处理：重新打开或刷新 `Authoring Hub -> Validator`，确认 `EffectRegistered=True`。

## 8. 当前已知边界

```text
当前 Resources Buff 扫描路径：Assets/Resources/BuffSystem/Buff
当前 production smoke pilot：991001
当前 DebugNoOpTickEffectId：990101
EffectId const 静态扫描只是辅助检查，不能覆盖所有动态注册来源
Validator 是 authoring 辅助，不是 runtime 安全证明
BuffSystem 仍不能宣称 rollback-ready
```

当前唯一已确认 View production smoke pilot 是 `991001 Debug_CompressedParallel_TickSmoke`。它不是正式玩法 Buff，也不代表 production whitelist 可以直接扩大。

## 9. 最小示例流程

以下示例只说明流程，不代表当前已经创建对应 Buff 或 Effect。

目标：制作一个 `PoisonTickEffect` + `Poison Buff` 草稿。

1. 在 `Effect Template` 输入 `EffectId=100001`，`ClassName=PoisonTickEffect`。
2. 点击 `Validate`。
3. 点击 `Generate Template`。
4. 手动实现 `OnTick` 等需要的 Effect 逻辑。
5. 点击 `Copy Registry Snippet`。
6. 人工将 snippet 加入 `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)`。
7. 重新编译 Unity。
8. 在 `Create Buff` 输入 `ConfigId=100001`，`EffectId=100001`。
9. 点击 `Validate`。
10. 点击 `Create Draft Asset`。
11. 运行 `Validator Scan`。
12. 如果希望进入 compressed whitelist，另开候选审查阶段。

再次强调：创建草稿 asset、生成 Effect 模板、满足 eligibility，都不等于进入 production whitelist。
