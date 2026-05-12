# BuffSystem 性能说明

## 当前优化点

- `BuffSystemCore.Tick` 每帧只做一次 Runtime Query 快照。
- Runtime Lookup 复用 `Dictionary<BuffRuntimeKey, List<Entity>>` 内部列表。
- ViewCache 延迟到外部读取时构建。
- `GetBuffs(target)` 按目标懒构建并复用列表。
- Runtime 移除排序使用复用 comparer，避免闭包分配。
- `MatchAnySource` 移除路径复用本帧 Runtime 快照，不再额外全量查询。

## Runtime Query 缓存策略

Tick 开头调用一次 `CaptureRuntimeEntities`，把当前运行中的 Buff Runtime Entity 保存到 `_runtimeEntitiesThisFrame`。

随后：

- `RebuildRuntimeLookup` 复用这份快照。
- `TickRuntimeBuffs` 复用这份快照。
- `EnsureViewCache` 复用这份快照。

本帧新增的 Runtime Entity 会记录到 `_createdRuntimeEntitiesThisFrame` 和 `_createdRuntimeComponentsThisFrame`。这样 ViewCache 可以在结构变更播放前看到新 Buff，但新 Buff 不会在创建当帧提前扣持续时间。

## ViewCache 缓存策略

`TryGetBuff` 会在 dirty 时重建 `_viewByKey`，保持按 key 查询接近 O(1)。

`GetBuffs(target)` 不会每帧为所有目标构建列表，只在外部查询指定目标时从 `_viewByKey` 懒构建该目标列表，并复用旧列表。

任意 Runtime 写入、移除或新建都会标记 ViewCache dirty，避免 UI 读到过期层数或剩余帧。

## 并行 Buff 性能风险

当前并行 Buff 是每层一个 Runtime Entity。优点是：

- 每层可独立到期。
- 回滚快照直观。
- 移除最早或最晚层的顺序稳定。

风险是：

- 高层数会增加 Entity 数量。
- 高层数会增加排序和查询成本。
- 回滚快照体积会随层数增长。

当前阶段保留该设计，避免改变功能语义。

## 后续优化方向

如果后续并行 Buff 层数非常高，可以评估把同目标、同来源、同配置的并行层压缩进一个 RuntimeComponent 的固定数组或定长缓冲。

该优化需要同时保证：

- 每层独立剩余帧。
- 移除顺序完全确定。
- 快照和重放结果一致。
- View 合并语义不变。
