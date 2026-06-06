# BuffSystem Changelog

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
