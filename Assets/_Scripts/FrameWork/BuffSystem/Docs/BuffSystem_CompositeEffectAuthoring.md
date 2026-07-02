# BuffSystem CompositeEffect 图形化生成指南

本文档用于归档 Phase 3I-11V 后的 CompositeEffect 图形化制作流程。它只描述 Editor authoring 工作流，不代表 runtime、whitelist、rollback 或 production 验证已经自动完成。

## 1. 适用场景

CompositeEffect 适用于一个 Buff 需要组合多个 EffectNode / ScriptActionNode 的情况。当前 runtime 仍保持单 `EffectId` 模型，因此多个 EffectNode 最终会由 Editor 工具合成为一个普通 `BuffEffectExecutorBase` 派生类。

适合使用 CompositeEffect 的场景：

- 一个 Buff 在 `OnApply`、`OnTick`、`OnRemove`、`OnRefresh` 或 `OnStackChanged` 中需要调用多个功能节点。
- 需要在图中表达多个 EffectNode 的顺序和生命周期分组。
- 希望最终 `BuffConfigData.EffectId` 仍只指向一个可注册 Effect。

不适合用 CompositeEffect 直接证明的内容：

- production whitelist 准入。
- rollback-ready。
- 真实 gameplay Buff 已经完整回归。
- runtime 行为已经自动通过。

## 2. 推荐图结构

推荐候选图结构：

```text
CandidateStart
 ├─ BuffShapeNode
 ├─ CompressedEligibilityNode
 ├─ RuntimeDependencyRiskNode
 ├─ CandidateDecisionNode
 └─ EffectCompositionRootNode
      ├─ EffectNode order=0
      │    ├─ OnApply -> ScriptActionNode order=0
      │    └─ OnTick  -> ScriptActionNode order=0
      ├─ EffectNode order=1
      │    └─ OnTick  -> ScriptActionNode order=0
      └─ EffectNode order=2
           └─ OnRemove -> ScriptActionNode order=0
```

`EffectCompositionRootNode.Effects` 表示成员关系，不表示执行顺序。执行顺序由 `EffectNode.Next` 或 `EffectNode.ExecutionOrder` 决定。

## 3. 节点职责

- `CandidateStart`：候选 Buff 的图入口。
- `BuffShapeNode`：`BuffConfigData` 行为字段来源，例如 BuffType、TriggerType、StorageMode、MaxStack、Duration、TickTime、Stack policy。
- `CompressedEligibilityNode`：压缩并行资格审查维度，不直接修改 whitelist。
- `RuntimeDependencyRiskNode`：runtime 风险审查维度，用于提示逐层 Entity、View 枚举、EventTrigger 等风险。
- `CandidateDecisionNode`：候选决策审查维度，用于记录是否可进入后续候选验证。
- `EffectCompositionRootNode`：最终 CompositeEffectId / CompositeEffectClassName / CompositeEffectName 的优先来源。
- `EffectNode`：生命周期分组，用于挂载 `OnApply` / `OnTick` / `OnRemove` / `OnRefresh` / `OnStackChanged` 的功能节点。
- `ScriptActionNode`：真正参与代码生成的功能节点。Action 脚本必须实现 `IBuffGraphAction`，并提供 public parameterless constructor。

## 4. EffectNode / ScriptActionNode 顺序规则

跨 EffectNode 顺序：

```text
1. 如果 EffectNode.Next 形成完整链，则使用 Next 链顺序。
2. 如果没有 Next 链，则使用 EffectNode.ExecutionOrder 升序。
3. 如果 Next 链与 ExecutionOrder 冲突，则 Graph Generate 报 Error。
4. 分叉、环、多个起点、重复 order 等问题会阻止 CompositeEffect codegen。
```

同一生命周期内 ScriptActionNode 顺序：

```text
1. 如果 ScriptActionNode.Next 形成完整链，则使用 Next 链顺序。
2. 如果没有 Next 链，则使用 ScriptActionNode.ExecutionOrder 升序。
3. 如果 Next 链与 ExecutionOrder 冲突，则 Graph Generate 报 Error。
4. Action 未实现 IBuffGraphAction 或缺少 public parameterless constructor 会阻止生成。
```

`OnStackChanged` 第一版只调用 `Execute(in context)`，不会向 Action 传入 delta；如需 delta，需要后续单独扩展接口。

## 5. CompositeEffect 预览

入口：

```text
Tools / BuffSystem / Authoring Hub
-> 图形化编辑
-> Graph Generate / CompositeEffect 区域
-> 预览 CompositeEffect 代码
```

预览只执行只读链路：

```text
BuffCandidateGraph
-> BuffGraphGeneratePlan
-> BuffGraphCompositeEffectPlan
-> BuffGraphCompositeEffectEmitter
-> 代码文本预览
```

预览不会写 `.cs` 文件，不会写 ID Registry，不会修改 `BuffEffectRegistryBootstrap`，不会创建 `BuffConfigData`，不会加入 whitelist，也不会修改 runtime。

## 6. 从图创建 CompositeEffect 草稿

入口：

```text
从图创建 CompositeEffect 草稿
```

真实写入内容：

```text
CompositeEffect.cs
Effect ID Registry
BuffEffectRegistryBootstrap auto 区块（仅 Settings 开启自动注册）
```

该流程不会创建 `BuffConfigData`，不会写 Buff ID Registry，不会注册 child EffectNode，不会加入 whitelist。

## 7. 从图一键创建 Buff + CompositeEffect 草稿

入口：

```text
从图一键创建 Buff + CompositeEffect 草稿
```

一键流程写入顺序：

```text
1. CompositeEffect preflight
2. Buff preflight
3. 写 CompositeEffect.cs
4. 写 Effect ID Registry
5. Bootstrap auto 注册 CompositeEffect
6. 创建 BuffConfigData asset
7. BuffConfigData.EffectId = CompositeEffectId
8. 写 Buff ID Registry
9. AssetDatabase Save / Refresh
```

关键保证：

```text
BuffConfigData.EffectId 指向 CompositeEffectId。
只注册最终 CompositeEffect，不注册 child EffectNode。
EffectBindingNode 是 legacy，一键 CompositeEffect 流程会忽略它。
```

## 8. 自动注册开关行为

`AutoRegisterEffectsToBootstrap = true` 且自动注册成功：

```text
生成 CompositeEffect.cs
写 Effect ID Registry
维护 BuffEffectRegistryBootstrap auto 区块
创建 BuffConfigData
写 Buff ID Registry
```

`AutoRegisterEffectsToBootstrap = false`：

```text
可以生成 CompositeEffect.cs
可以写 Effect ID Registry
不修改 Bootstrap
显示 registry.Register(...) snippet
一键流程不会创建 BuffConfigData
```

自动注册失败：

```text
保留 CompositeEffect.cs
保留 Effect ID Registry
不创建 BuffConfigData
报告 Error / Warning / 手动注册片段
不做复杂回滚
```

## 9. 失败与清理策略

会阻止所有写入的典型 Error：

```text
未选择 BuffCandidateGraph
图中缺少必要节点
图中没有 EffectNode
CompositeEffectId 无效 / 已占用 / 位于 990000+ 保留段
CompositeEffectClassName 非法
目标 CompositeEffect.cs 已存在
EffectNode 顺序冲突
ScriptActionNode 顺序冲突
ScriptActionNode 无效
Action 未实现 IBuffGraphAction
Action 无 public parameterless constructor
Buff ConfigId 无效 / 已占用 / 位于 990000+ 保留段
Buff asset 目标路径已存在
```

测试清理策略：

```text
删除测试 CompositeEffect.cs
删除测试 CompositeEffect.cs.meta
删除测试 BuffConfigData asset
删除测试 BuffConfigData asset.meta
删除 Bootstrap auto block 中测试注册行
删除 ID Registry 中测试 Effect entry
删除 ID Registry 中测试 Buff entry
等待 Unity 重新编译
确认 Console 无 error
```

如果 CompositeEffect 和 Registry 已写入，但 Buff 创建失败，当前策略是不自动回滚；报告会提示手动清理。

## 10. 验证清单

生成后至少检查：

```text
Unity Console 无 error
CompositeEffect.cs 编译通过
Bootstrap auto block 注册的是 CompositeEffectId
没有注册 child EffectNode
BuffConfigData.EffectId == CompositeEffectId
Effect ID Registry 有 CompositeEffect 记录
Buff ID Registry 有 Buff 记录
没有加入 whitelist
没有修改 runtime
Validator 通过
Runner 通过
场景验证通过
```

建议回归：

```text
BuffSystemPhase2AValidationRunner
BuffSystemCompressedParallelValidationRunner
BuffSystemRestoreHookValidationRunner
BuffSystemStorageBehaviorConsistencyRunner
BuffSystemStoragePerformanceRunner
```

## 11. 禁止事项与 production 边界

必须保留以下边界：

```text
生成 CompositeEffect 不等于 production-ready。
创建 BuffConfigData 不等于进入 whitelist。
自动注册 Effect 不等于 rollback-ready。
Graph 只是 authoring artifact，不是 runtime truth。
BuffConfigData asset 仍是 production 配置来源。
最终是否进入 production whitelist 必须人工批准。
```

禁止把以下内容视为自动完成：

```text
正式 gameplay Buff 创建
production whitelist 扩大
rollback-ready 宣称
所有真实生产场景完整回归
```
