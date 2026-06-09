# BuffSystem xNode 候选图使用说明

## 1. 这个功能解决什么问题

`BuffCandidateGraph` 用于把真实 gameplay Buff 候选的设计、风险和准入判断画成可视化审查图。它解决的是“先把设计说清楚，再落地为 BuffConfigData / Effect 草稿”的问题。

它不是 production 配置源，也不会进入 runtime 加载流程。

## 2. 图形化编辑与快捷编辑的分工

```text
BuffCandidateGraph：负责可视化设计、候选审查、风险提示。
Authoring Hub：负责快速创建 BuffConfigData 草稿、生成 Effect 模板、扫描真实配置。
Validator：只扫描真实 BuffConfigData asset，帮助确认落地配置状态。
```

Graph 和 Authoring Hub 当前只做单向联动：

```text
BuffCandidateGraph -> Authoring Hub 表单
```

Authoring Hub 不会自动把表单改动写回 Graph，避免出现双源漂移。

## 3. 推荐工作流

1. 先用 `BuffCandidateGraph` 画出候选 Buff 设计和风险。
2. 在 `Tools / BuffSystem / Authoring Hub` 顶部选择候选图。
3. 查看候选摘要、拒绝原因、警告和下一步建议。
4. 将图中的字段导入 `Create Buff` 表单。
5. 在 `Create Buff` 中校验并由人工点击创建 BuffConfigData 草稿。
6. 将图中的 Effect 字段导入 `Effect Template` 表单。
7. 在 `Effect Template` 中校验并由人工点击生成 Effect 模板。
8. 回到 `Validator` 扫描真实 BuffConfigData。
9. 通过 Runner / Unity 手动验证后，再由负责人决定是否申请进入 whitelist。

## 4. 如何创建 BuffCandidateGraph

在 Project 窗口中使用：

```text
Assets / Create / BuffSystem / Buff Candidate Graph
```

建议不要把候选图放入：

```text
Assets/Resources/BuffSystem/Buff
```

该目录只用于真实 `BuffConfigData` 资源扫描。

## 5. 如何添加节点并连接

第一版建议包含以下节点：

```text
Candidate Start
Buff Shape
Effect Binding
Compressed Eligibility
Runtime Dependency Risk
Candidate Decision
```

节点可以在 xNode 图中创建、编辑和连接。当前最小 evaluation 只检查节点数量完整性，尚不证明连接路径完整。

## 6. 如何在 Authoring Hub 中选择候选图

打开：

```text
Tools / BuffSystem / Authoring Hub
```

在顶部区域：

```text
候选图联动 / Candidate Graph Link
```

选择一个 `BuffCandidateGraph`。该区域会显示：

```text
GraphVersion
ConfigId
BuffName
EffectId
图完整
可提交审查
拒绝原因
警告
下一步
```

也可以使用 `打开图`、`Ping 图`、`刷新候选摘要` 辅助查看。

## 7. 如何把候选图导入 Create Buff 表单

在 Authoring Hub 顶部选择候选图后，进入：

```text
创建 Buff
```

点击：

```text
从候选图导入基础字段
```

会导入：

```text
ConfigId
BuffName
Description / DesignPurpose
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
```

导入后会触发现有校验预览。

该按钮不会创建 `BuffConfigData` asset，不会加入 whitelist，也不会修改 runtime。

## 8. 如何把候选图导入 Effect Template 表单

在 Authoring Hub 顶部选择候选图后，进入：

```text
Effect 模板
```

点击：

```text
从候选图导入 Effect 字段
```

会导入：

```text
EffectId
EffectClassName
Effect Note / EffectRiskNotes
```

导入后会触发现有校验预览。

该按钮不会生成 `.cs` 文件，不会修改 `BuffEffectRegistryBootstrap`，也不会注册 Effect。

## 9. 如何用 Validator 检查真实 BuffConfigData

进入：

```text
配置检查器
```

Validator 仍然只扫描真实 `BuffConfigData` 资源。候选图只提供对照提示：

```text
当前候选图 ConfigId
真实 BuffConfigData 是否存在
```

如果同 ConfigId 已存在真实配置，可以用 Validator 对照检查。若不存在，则表示候选图尚未落地为 BuffConfigData。

## 10. 哪些事情不会自动发生

```text
不会自动创建 production Buff。
不会自动创建 BuffConfigData，除非用户在 Create Buff 中手动点击创建。
不会自动生成 Effect 代码，除非用户在 Effect Template 中手动点击生成。
不会自动注册 Effect。
不会自动加入 whitelist。
不会自动修改 runtime。
不会自动保存 scene。
不会证明 rollback-ready。
不会替代 Runner。
不会替代 Unity 手动验证。
```

## 11. 常见问题

### 候选图完整是否等于可以进 whitelist

不是。候选图完整只说明图中的必要节点存在。进入 whitelist 仍需真实 BuffConfigData、Effect 注册、行为一致性验证、场景验证、性能观察和负责人审批。

### 导入字段是否会创建资源

不会。导入只填充 Authoring Hub 表单。真正创建 BuffConfigData 或 Effect `.cs` 必须由用户手动点击对应按钮。

### 候选图能否替代 Validator

不能。候选图是设计和审查入口，Validator 扫描的真实 BuffConfigData 才代表落地配置状态。

### 候选图能否替代 Runner

不能。Runner 和 Unity 场景验证仍是行为正确性和集成状态的验证入口。

## 12. 当前限制

```text
当前只支持 Graph -> Authoring Hub 单向导入。
当前不支持 Authoring Hub 表单自动写回 Graph。
当前 evaluation 只检查节点数量完整性，不校验完整连接路径。
当前不自动生成候选审查报告。
当前不声明 rollback-ready。
当前不扩大 production whitelist。
```
