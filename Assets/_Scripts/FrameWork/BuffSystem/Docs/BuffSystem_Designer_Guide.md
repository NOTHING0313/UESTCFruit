# BuffSystem 策划配置指南

## 创建 BuffConfigData

在 Project 窗口中创建：

```text
Create > BuffSystem > BuffConfigData
```

推荐按以下顺序填写：

1. 基础信息：`ID`、名称、描述、图标、优先级、标签。
2. 生命周期：是否永久、持续时间、触发类型、Tick 间隔。
3. 堆叠规则：Buff 类型、最大层数、叠层策略、移除策略。
4. 效果配置：EffectId、事件触发列表。
5. 调试信息：查看帧数预览和校验结果。

`ID` 必须唯一且大于 0。名称不能为空。

## TriggerType.Tick

Tick Buff 会按固定帧间隔触发 `OnTick`。

需要填写：

- 持续时间，除非是永久 Buff。
- Tick 间隔，必须大于 0。
- EffectId，选择实现了 Tick 逻辑的 Effect。

适合中毒、灼烧、周期回血等效果。

## TriggerType.EventTrigger

EventTrigger Buff 不会按时间自动触发事件逻辑，而是在收到匹配 `EventId` 的 `IGameEvent` 时触发。

需要填写：

- 事件触发列表，至少选择一个 EventId。
- EffectId，选择实现了对应事件接口的 Effect。

适合反伤、命中后触发、击杀后触发、受击后触发等效果。

## EffectId 如何选择

如果工程里存在 `BuffEffectCatalogData`，可以通过 Effect 显示名下拉选择。

如果没有 Catalog，仍允许手动填写 EffectId，但面板会显示中文警告。运行时只读取整数 EffectId，不读取显示名或说明。

常见问题：

- EffectId 小于等于 0：无效。
- Effect 不支持当前 TriggerType：会显示警告，需要确认是否选错。
- 注册表中没有对应 Effect：运行时不会执行效果。

## EventId 如何选择

如果工程里存在 `BuffEventCatalogData`，可以通过事件显示名下拉选择。

如果没有 Catalog，仍可手动维护 EventId 列表。运行时只读取整数数组 `EventIds`。

EventTrigger Buff 只有收到匹配 EventId 的事件才会响应。

## 常见配置错误

- ID 为空或重复：加载时会失败或覆盖预期。
- 名称为空：不利于调试和策划沟通。
- 非永久 Buff 持续时间为 0：会立刻失效。
- Tick Buff 的 Tick 间隔为 0：不会得到合理的周期触发。
- EventTrigger Buff 没有 EventId：永远不会响应事件。
- EffectId 为 0：不会找到有效 Effect。
- 并行 Buff 最大层数过高：可能增加 Runtime Entity、排序和回滚快照成本。

## 配置不会进入 Runtime 的内容

以下内容只用于编辑器和表现层，不影响 ECS 确定性：

- 图标。
- 描述。
- Effect 显示名。
- Event 显示名。
- Catalog 中的说明和开发备注。
- Inspector 中的帧数预览。
