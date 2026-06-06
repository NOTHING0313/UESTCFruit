# BuffSystem Parallel Buff

## Phase 3G-4J - Production pilot 991001 validation closeout

`Debug_CompressedParallel_TickSmoke` (`configId = 991001`) is the current single production compressed-pilot Buff. It uses:

```text
BuffType = parallel
ParallelStorageMode = CompressedExpiryFrameList
TriggerType = Tick
Unlimited = false
MaxStack = 3
EffectId = 990101
```

The production compressed path remains gated by:

```csharp
gate && whitelist.Contains(configId) && IsCompressedParallelEligible(definition)
```

The production whitelist is intentionally limited to `991001`. Non-whitelisted Buffs continue to use `EntityPerStack`.

### Phase 3G-4I creation fix

Before the fix, production `WorldStates.Ticking` could defer a newly added `CompressedParallelBuffRuntimeComponent` through `StructuralChangeBuffer`. `ApplyCompressedParallelAdd` then tried to read the just-created component back immediately with `TryGetComponent`, which could fail. The result was a zero-layer compressed runtime container:

```text
AfterTick1 RuntimeExists = True
AfterTick1 LayerCount = 0
AfterTick2 RuntimeExists = False
```

After the fix, new compressed runtimes are initialized locally, layers are appended locally, and the final component is written once through `World.SetComponent`.

### Verified pilot result

The current production debug trace verifies:

```text
AfterAdd AddQueueCount = 1
AfterTick1 RuntimeCompressed = 1
AfterTick1 LayerCount = 1
AfterTick1 RemainingFrames = 120
AfterTick2 RuntimeCompressed = 1
AfterTick2 TryGetBuffFound = True
GetBuffsCount = 1
CurrentConfigViewCount = 1
Current ConfigId EntityPerStackRuntime count = 0
Compressed Path = PASS
```

This confirms that `991001` creates a compressed runtime, does not fall back to EntityPerStack, and becomes visible through aggregate ViewData after capture.

### Still pending before Phase 3H closure

- `stack = 3` aggregate ViewData validation.
- Explicit Remove validation.
- Natural Expire validation.
- Performance comparison.
- Rollback snapshot validation.

## Phase 3G-3D-A - Production whitelist smoke-test gate

Phase 3G-3D-A adds a production creation path for `BuffSystemCore` that opens the compressed parallel runtime gate only with a production whitelist. Public constructors remain unchanged and still use `gate=false` with an empty whitelist.

The production whitelist currently contains only `configId = 991001`, reserved for `Debug_CompressedParallel_TickSmoke`. The whitelist is still combined with the existing eligibility checks:

```csharp
gate && whitelist.Contains(configId) && IsCompressedParallelEligible(definition)
```

Non-whitelisted Buffs continue to use `EntityPerStack`, even when their authoring data declares `CompressedExpiryFrameList`. The validation whitelist for runner config IDs `9301-9315` is unchanged.

This phase does not create the pilot `BuffConfigData asset`, does not create the Resources directory, and does not modify `.asset`, `.unity`, `.prefab`, or `.meta` files. The pilot asset must be created later through Unity Editor at:

`Assets/Resources/_Scripts/FrameWork/BuffSystem/BuffConfigDataCollection/Debug_CompressedParallel_TickSmoke.asset`

## Phase 3F-5A - Test-only compressed gate entry

Phase 3F-5A adds an internal validation-only factory for creating a `BuffSystemCore` with compressed parallel runtime enabled. Public constructors remain unchanged and still keep the compressed feature gate closed by default.

The intended validation entry is `CreateForCompressedParallelValidation(...)`. It exists only for controlled compressed validation runners. Production code should keep using the public constructors, which preserve EntityPerStack as the default runtime behavior.

This phase does not enable `CompressedExpiryFrameList` in normal runtime, does not modify `IBuffSystem`, and does not add a public setter or public constructor parameter.

## Phase 3F-3E - Compressed pending remove and destroy cleanup

Phase 3F-3E completes compressed runtime container cleanup. When compressed ClearAll, the last explicit layer Remove, or the last Expire leaves `layerCount == 0`, the compressed runtime container is queued for logical removal and later physical destroy. The feature gate still defaults to closed, so current runtime behavior remains EntityPerStack.

Compressed pending remove reuses `_pendingRemoveRuntimeSet` and `_pendingRemoveRuntimes`. The pending runtime handle is `compressedRuntimeHandle`, because the removed entity is the compressed runtime container, not an individual layer. Pending remove immediately removes the `target/source/configId` key from `_compressedRuntimeEntityByKey` and marks ViewCache dirty.

The compressed runtime container pending remove does not queue an additional lifecycle Remove Effect. Single-layer `StackChanged` and `Remove` effects remain the responsibility of compressed layer remove / expire helpers, avoiding duplicate notifications.

Query and ViewCache remain read-only: if compressed ViewCache sees pending remove, empty layers, or dead targets, it skips the runtime and does not call pending remove. Destroy performs defensive compressed lookup cleanup before destroying the entity. EntityPerStack pending remove and destroy semantics remain unchanged.

## Phase 3F-3D - Compressed ViewCache gated block

Phase 3F-3D connects the compressed ViewData aggregation helper to `EnsureViewCache` behind the compressed feature gate. Public `TryGetBuff` and `GetBuffs` APIs are unchanged, and the feature gate still defaults to closed, so current runtime behavior remains EntityPerStack.

The compressed ViewCache block runs after the existing EntityPerStack ViewData build and only iterates `_compressedRuntimeEntitiesThisFrame`. It filters pending remove runtimes, missing components, missing definitions, disabled gate, empty layers, dead targets, and failed compressed ViewData builds. Successful compressed views are written to `_viewByKey`; if the key already exists, the existing `MergeViewData` behavior is reused. The block does not write `_validTargetViewCache` directly.

Compressed ViewData uses the ViewData remaining-frame convention:

- forever is `RemainingFrames = -1`;
- duration is `Math.Max(0, layer.expireFrame - currentFrame)`;
- mixed duration and forever resolves to `-1`.

This is intentionally different from lifecycle Tick snapshots, which use the internal tick snapshot convention with `expireFrame - currentFrame + 1`. These conventions must not be mixed.

The added frame guard is only considered when compressed query can be active. With the gate closed, `EnsureViewCache` remains equivalent to the previous dirty-only behavior. When the gate is opened later, the frame guard prevents compressed dynamic `RemainingFrames` from becoming stale across frames.

Pending remove and destroy handling for compressed runtime are still not connected. Do not enable the compressed feature gate until those phases are completed and validated.

## Phase 3F-3C - Compressed Tick / Expire gated entry

Phase 3F-3C connects an independent compressed Tick / Expire entry after the existing EntityPerStack Tick path. The EntityPerStack `TickRuntimeBuffs` logic is unchanged.

The compressed entry only iterates `_compressedRuntimeEntitiesThisFrame` and is protected by `ShouldUseCompressedParallel`. Because the feature gate still defaults to closed, compressed Tick / Expire is unreachable in normal runtime.

When the gate is eventually opened, compressed runtime processing keeps the locked order: Tick layers first, then Expire layers. Expired layer removal still uses the prebuilt single-layer snapshots and does not change `BuffEffectRequest`.

This phase does not connect compressed Query, ViewCache, pending remove, or destroy flows. Do not enable the compressed feature gate until those phases are completed and validated.

## Phase 3F-3B - Compressed query and lookup skeleton

Phase 3F-3B adds only the query, capture list, and lookup rebuild skeleton for `CompressedParallelBuffRuntimeComponent`. The feature gate still defaults to closed, so `CompressedExpiryFrameList` is not enabled and EntityPerStack remains the active runtime behavior.

Lookup separation remains strict:

- `_runtimeEntitiesByKey` is only for EntityPerStack `BuffRuntimeComponent`.
- `_compressedRuntimeEntityByKey` is only for compressed runtime entities.
- Compressed runtime is not written into `_runtimeEntitiesByKey`.
- `_compressedRuntimeEntityByKey` is a rebuildable cache, not rollback truth.
- The compressed rollback truth remains `CompressedParallelBuffRuntimeComponent` and its fixed-capacity layer buffer.

The compressed lookup rebuild skips pending remove runtimes, missing components, empty `layerCount`, and dead targets. Duplicate keys are treated as abnormal state and resolved deterministically by the smallest `Entity.ID -> Entity.Version`.

This phase does not connect compressed Tick, Expire, ViewCache, Query, pending remove, or destroy flows.

## Phase 3F-3A - Add / Remove gated branches

Phase 3F-3A only wires the compressed Add / Remove branch checks into the main entry points. Both branches are protected by `ShouldUseCompressedParallel`, and the feature gate still defaults to closed, so these branches are unreachable in normal runtime.

- Parallel Add calls compressed Add only when `ShouldUseCompressedParallel(in definition)` is true.
- Remove calls compressed Remove only after the command definition is resolved and `ShouldUseCompressedParallel(in definition)` is true.
- When the gate is false, the existing EntityPerStack Add / Remove paths continue unchanged.
- Tick, Expire, Query, ViewCache, runtime lookup rebuild, pending remove, and destroy flows are still not connected to compressed runtime.

Do not open the compressed feature gate until the later phases connect and validate the remaining runtime flows.

## Phase 3F-2 - Feature gate skeleton

Phase 3F-2 adds only the private feature gate skeleton for `CompressedExpiryFrameList`. The gate defaults to closed, so `ShouldUseCompressedParallel` still returns false in current runtime use and compressed storage is not enabled.

The intended structure is:

```csharp
return _enableCompressedParallelRuntime && IsCompressedParallelEligible(in definition);
```

This phase does not connect compressed helpers to Add, Remove, Tick, Query, ViewCache, runtime lookup, pending remove, or destroy flows. EntityPerStack remains the default and only active runtime behavior.

## Phase 3E-2 - ViewData dormant helpers

Phase 3E-2 only adds dormant compressed ViewData aggregation helpers. `CompressedExpiryFrameList` is still not enabled: `ShouldUseCompressedParallel` continues to return false, and the helpers are not connected to `EnsureViewCache`, `TryGetBuff`, or `GetBuffs`.

The compressed ViewData helper follows the current EntityPerStack aggregation semantics:

- `stack = activeLayerCount`; only active layers are counted.
- ViewData forever uses `RemainingFrames = -1`.
- If `definition.IsForever == true`, the aggregated `RemainingFrames` is `-1`.
- If any active layer has `expireFrame == int.MaxValue`, the aggregated `RemainingFrames` is `-1`.
- Otherwise `RemainingFrames` is the minimum `Math.Max(0, layer.expireFrame - currentFrame)` across active duration layers.
- `RuntimeHandle` is the minimum `layerRuntimeHandle` across active layers, not `compressedRuntimeHandle`.

ViewData uses `expireFrame - currentFrame`. Runtime or Effect snapshots may use a different internal remaining-frame convention; these two conventions must not be mixed. Public `TryGetBuff` and `GetBuffs` behavior is unchanged in this phase.

When compressed Query is enabled in a later phase, ViewCache must also solve the current lack of a frameNumber guard or use an explicit dirty strategy, otherwise dynamically computed compressed `RemainingFrames` could become stale.

## Phase 3D-2 - Tick / Expire dormant helpers

Phase 3D-2 only adds dormant helpers for `CompressedExpiryFrameList` Tick and natural Expire. The compressed storage mode is still not enabled: `ShouldUseCompressedParallel` continues to return false, and the helpers are not called by `TickRuntimeBuffs` or any runtime main flow.

The locked EntityPerStack boundary is:

- A runtime created on F1 does not Tick on F1.
- The first Tick happens on F2.
- In the same runtime frame, Tick is processed before Expire.
- `durationFrames = 1`: F1 Apply, F2 Tick, F2 Remove.
- `durationFrames = 2`: F1 Apply, F2 Tick, F3 Tick, F3 Remove.

Compressed layers must use the same fixed-frame formula when they are enabled in a later phase:

```csharp
expireFrame = createFrame + durationFrames;
expired = currentFrame >= expireFrame;
tickSnapshot.remainingFrames = definition.IsForever
    ? 0
    : Math.Max(0, layer.expireFrame - currentFrame + 1);
removeSnapshot.remainingFrames = 0;
```

Forever layers use `expireFrame = int.MaxValue`, never enter natural Expire, and may still Tick when their tick interval is satisfied. Phase 3D-2 does not connect compressed Tick / Expire to Query, ViewData, pending remove, or runtime destruction.

## Phase 3C-2 - Add / Refresh / Remove dormant helpers

Phase 3C-2 只预埋 `CompressedExpiryFrameList` 的 Add、Refresh、Remove 内部 helper，不启用压缩主流程。`ShouldUseCompressedParallel` 仍返回 false，因此配置为 `CompressedExpiryFrameList` 的 Buff 当前仍不会实际进入压缩路径，EntityPerStack 行为不变。

本阶段不接入 Tick、自然到期 Expire、TryGetBuff、GetBuffs 或 ViewData 聚合，也不扩展 `BuffEffectRequest`。新增 helper 只用于后续阶段接入前的代码准备。

ReplaceEarliestWhenFull 的压缩 helper 采用“状态按策略执行，Effect 按 Phase 2A phaseOrder Flush”的语义：状态层面先移除旧层再追加新层，但生命周期 Effect Flush 仍按 `Apply -> Refresh -> StackChanged -> Tick -> Remove` 排序，因此不承诺同帧 Replace 中 Remove Effect 一定早于 Apply Effect。

## Phase 3C-1 - 压缩接入准备

Phase 3C-1 只补充内部 helper、eligibility 判断和 compressed lookup cache，仍不启用 `CompressedExpiryFrameList` 主流程。`ShouldUseCompressedParallel` 在本阶段保持返回 false，`CompressedExpiryFrameList` 配置不会影响任何现有 Buff 行为。

本阶段不接入 Add、Refresh、Remove、Tick、Query 或 EffectRequest。`_compressedRuntimeEntityByKey` 只作为后续 compressed runtime lookup cache 预留，不是回滚真状态；Phase 3C-1 不在主流程中写入或读取它。

当前 EntityPerStack 的 Tick / Expire 基准为：先推进 `elapsedFrames` 并判断 Tick，满足 Tick 条件时先 Queue `OnTick`；随后非永久 Buff 才扣减 `remainingFrames` 并处理自然到期。后续压缩模式接入时必须保持“先 Tick，再 Expire”的同帧顺序。

## Phase 3B - 压缩存储预留入口

Phase 3B 新增 `ParallelBuffStorageMode`：

```csharp
public enum ParallelBuffStorageMode
{
    EntityPerStack = 0,
    CompressedExpiryFrameList = 1
}
```

当前默认模式仍是 `EntityPerStack`，并且 Phase 3B 不改变任何现有并行 Buff 运行时行为。即使配置中选择了 `CompressedExpiryFrameList`，当前运行时也不会启用压缩逻辑，所有并行 Buff 仍然走每层一个 Runtime Entity 的旧路径。

`CompressedExpiryFrameList` 在 Phase 3B 只包含配置入口和数据结构骨架：

- `CompressedParallelBuffLayer`
- `CompressedParallelBuffRuntimeComponent`
- `CompressedParallelBuffLayerBuffer`

这些结构当前不创建、不写入 World、不参与 Tick、不参与查询，也不接入 Phase 2A 生命周期 EffectRequest Pipeline。后续 Phase 3C 才会单独设计 Add、Refresh、Remove、Expire 和查询接入。

压缩模式预期使用固定帧字段，例如 `expireFrame`、`elapsedFrames`、`ticks` 和 `layerRuntimeHandle`。禁止使用 `Time.time`、`Time.deltaTime` 或 `float expiry` 作为模拟时间依据。

## 作用

并行 Buff 用于表示多层 Buff 独立存在、独立到期的场景。当前第一套 ECS BuffSystem 仍采用“每层一个 Runtime Entity”的设计。

## 当前运行时模型

每个并行层都会创建一个带 `BuffRuntimeComponent` 的 Runtime Entity。

优点：

- 每层有独立 `remainingFrames`。
- 移除最早或最晚层的顺序清晰。
- 回滚快照可以直接捕获每层状态。

成本：

- 层数高时会增加 Entity 数量。
- 查询、排序和快照体积会随层数增加。

## ParallelBuffStackUpPolicy

```csharp
Append = 0
RefreshEarliest = 1
RefreshAll = 2
ReplaceEarliestWhenFull = 3
```

### Append

追加新层。每个新层使用新的 Runtime Entity。

### RefreshEarliest

刷新最早到期的若干层，然后把剩余 incoming 层追加为新层。

Phase 1 行为：被刷新的层会同步重置：

- `durationFrames`
- `remainingFrames`
- `tickIntervalFrames`
- `elapsedFrames = 0`
- `ticks = 0`

### RefreshAll

刷新当前匹配的全部并行层，然后追加 incoming 层。

Phase 1 行为：所有被刷新的层都会同步重置 `elapsedFrames` 和 `ticks`。

### ReplaceEarliestWhenFull

未满时追加新层；满层时移除最早到期层并创建新层。

说明：当前实现通过移除旧层再创建新层表达替换，因此新层天然从完整持续时间和 0 Tick 计数开始。它不直接刷新旧层。

## ParallelBuffStackDownPolicy

```csharp
RemoveEarliest = 0
RemoveLatest = 1
ClearAll = 2
```

排序依据：

- `remainingFrames`
- `runtimeHandle`
- `Entity.ID`
- `Entity.Version`

该排序不依赖 Dictionary 遍历顺序。

## 使用样例

```csharp
BuffDefinition poison = new BuffDefinition(
    configId: 2001,
    name: "Parallel Poison",
    priority: 0,
    maxStack: 5,
    unlimited: false,
    isForever: false,
    durationFrames: 120,
    tickIntervalFrames: 30,
    durationExtendFramesPerStack: 0,
    triggerType: BuffTriggerType.Tick,
    buffType: BuffInstanceType.parallel,
    normalStackPolicy: NormalBuffStackPolicy.RefreshDuration,
    parallelStackUpPolicy: ParallelBuffStackUpPolicy.RefreshEarliest,
    parallelStackDownPolicy: ParallelBuffStackDownPolicy.RemoveEarliest,
    effectId: 2001);
```

## 迁移说明

FrameWork2 使用 `ParallelBuffRunTimeData` 和到期时间队列表达并行层。第一套不迁移该运行时对象，也不使用 `Time.time`。其“独立层到期”和“刷新最早层”的语义已经映射到 ECS Runtime Entity 和固定帧字段上。

## Phase 3F-4C 验证入口 V1

`Assets/_Scripts/FrameWork/BuffSystem/Test/BuffSystemCompressedParallelValidationRunner.cs` 提供 Unity Editor 手动验证入口：

```csharp
[ContextMenu("Run Compressed Parallel Validation")]
public void RunCompressedParallelValidation()
```

Runner V1 只在测试环境中通过 `BuffSystemCore.CreateForCompressedParallelValidation(definitionRegistry, effectRegistry)` 创建 gate=true 的 `BuffSystemCore`，用于验证压缩并行 Buff 的最小闭环。正式运行时 public 构造函数不变，默认 gate 仍关闭，当前项目运行时默认仍是 `EntityPerStack`。

Runner V1 覆盖范围：

- gate=true 时，满足 eligibility 的 `CompressedExpiryFrameList` + parallel + Tick + 非 Unlimited + `MaxStack <= Capacity` Buff 会创建 `CompressedParallelBuffRuntimeComponent`。
- Append 多层后，`TryGetBuff / GetBuffs` 可以通过现有 ViewCache 读取聚合 ViewData。
- 聚合 ViewData 中 `Stack = layerCount`。
- 聚合 ViewData 中 `RemainingFrames = min(layer.expireFrame - currentFrame)`，不使用 Tick snapshot 的 `+1` 口径。
- 聚合 ViewData 中 `RuntimeHandle = min(layer.layerRuntimeHandle)`，不使用 `compressedRuntimeHandle`。
- `EventTrigger`、`Unlimited`、`MaxStack > CompressedParallelBuffLayerBuffer.Capacity` 仍 fallback 到 `EntityPerStack`。
- gate=false 的默认 `BuffSystemCore` 即使配置 `CompressedExpiryFrameList`，仍走 `EntityPerStack`。

Runner V1 暂不覆盖：

- `RefreshEarliest`
- `RefreshAll`
- `ReplaceEarliestWhenFull`
- `RemoveEarliest`
- `RemoveLatest`
- `ClearAll`
- Tick / Expire 行为
- PendingRemove / Destroy 清理
- duration / forever 混合内部状态

这些内容留到后续 Runner V2 / V3 / V4 分阶段验证。在这些阶段完成前，正式运行时不应打开 compressed gate。

## Phase 3F-4D 验证入口 V2

Runner V2 在同一个 `BuffSystemCompressedParallelValidationRunner` 中追加 Refresh / Remove policy 验证。它仍只修改测试入口，不修改 `BuffSystemCore`、public API、public 构造函数或默认 gate。

Runner V2 新增覆盖范围：

- `RefreshEarliest`：多层 compressed layer 创建后，重复 Add 只刷新最早层；被刷新层保持 `layerId` 和 `layerRuntimeHandle`，刷新 `expireFrame`，并在本帧 compressed Tick 后表现为较低的 `elapsedFrames / ticks`。
- `RefreshAll`：多层 compressed layer 创建后，重复 Add 刷新所有已有层；所有层保持 `layerId` 和 `layerRuntimeHandle`，刷新 `expireFrame`，`ViewData.Stack` 不变。
- `RemoveEarliest`：移除最早层后，聚合 `ViewData.Stack` 减少，剩余层仍可查询。
- `RemoveLatest`：移除最新层后，聚合 `ViewData.Stack` 减少，剩余层仍可查询。
- `ClearAll`：清空后 public `TryGetBuff / GetBuffs` 不再显示该 Buff。
- gate=false 回归仍保留：默认 public 构造路径不会启用 compressed runtime。

Runner V2 仍不覆盖：

- Tick / Expire 行为。
- PendingRemove / Destroy 深度验证。
- `ReplaceEarliestWhenFull`。
- duration / forever 混合内部状态。

这些内容继续留到后续 Runner V3 / V4。

## Phase 3F-4E 验证入口 V3

Runner V3 在 `BuffSystemCompressedParallelValidationRunner` 中追加 Tick / Expire / PendingRemove / Destroy 深度验证。它仍只扩展测试脚本，不修改 `BuffSystemCore`、public API、public 构造函数或默认 gate。

Runner V3 新增覆盖范围：

- `durationFrames = 1`：F1 创建 compressed layer 并触发 `OnApply`；F2 第一次 `OnTick`，同帧自然到期并触发 `OnRemove`。
- `durationFrames = 2`：F2 第一次 `OnTick` 不移除；F3 第二次 `OnTick`，同帧自然到期并触发 `OnRemove`。
- Tick snapshot 使用 `expireFrame - currentFrame + 1` 口径，Remove snapshot 使用 `remainingFrames = 0`。
- forever compressed layer 可 Tick，不自然 Expire；ViewData `RemainingFrames = -1`，Effect snapshot `remainingFrames = 0`。
- 最后一层 Expire 后，`OnRemove` 回调中 public `TryGetBuff / GetBuffs` 不再显示该 Buff。
- compressed runtime 被 Destroy 后，World 查询不再返回该 runtime；再次 Add 同配置可重新创建 runtime，用于验证 lookup 清理。
- compressed runtime container pending remove 不额外触发聚合 Remove Effect；生命周期回调仍只来自单层 snapshot。

Runner V3 仍不覆盖：

- `ReplaceEarliestWhenFull`。
- duration / forever 混合内部状态。
- 性能测试。
- 回滚快照测试。

这些内容继续留到后续 Runner V4 或单独回滚验证阶段。

## Phase 3F-4F 验证入口 V4

Runner V4 在 `BuffSystemCompressedParallelValidationRunner` 中追加 `ReplaceEarliestWhenFull` 与关键边界验证。它仍只扩展测试脚本，不修改 `BuffSystemCore`、public API、public 构造函数或默认 gate。

Runner V4 新增覆盖范围：

- `ReplaceEarliestWhenFull` 未满层：当 `layerCount < MaxStack` 时 Add 会 Append 新层，`ViewData.Stack` 增加，不触发最早层替换。
- `ReplaceEarliestWhenFull` 满层：当 `layerCount == MaxStack` 时再次 Add 会移除最早层并追加新层，最终 `layerCount` 保持 `MaxStack`。
- Replace 后，被替换层的 `layerId / layerRuntimeHandle` 不再存在，未替换层保持原 identity，新追加层生成新的 `layerId / layerRuntimeHandle`。
- Replace 后 public `TryGetBuff / GetBuffs` 仍只返回一个聚合 ViewData，`RuntimeHandle` 仍使用 active layers 中最小 `layerRuntimeHandle`。
- Replace Effect 验证只要求必要的 `Apply / StackChanged / Remove` 存在，不假设 Remove callback 一定早于 Apply callback；实际 Flush 顺序仍由 Phase 2A phaseOrder 决定。
- `MaxStack == CompressedParallelBuffLayerBuffer.Capacity` 仍 eligible，可走 compressed runtime。

Runner V4 仍不覆盖：

- duration / forever 混合内部状态。
- 性能测试。
- 回滚快照测试。

这些内容继续留到单独 edge case 或 rollback 验证阶段。

## Phase 3F-8 - CompressedExpiryFrameList 启用前工程说明

`CompressedExpiryFrameList` 已完成 Phase 3B 到 Phase 3F-4F 的配置入口、数据结构、主流程 gate 分支、验证入口和 Runner V1/V2/V3/V4 行为验证。但当前正式 public constructor 路径 gate 仍默认关闭，正式运行时仍默认 `EntityPerStack`。在 Phase 3G 小范围启用前，不建议业务代码直接使用 validation factory，也不建议全项目直接打开 compressed gate。

### 当前状态

- `ParallelBuffStorageMode.CompressedExpiryFrameList` 已存在。
- `BuffConfigData.ParallelStorageMode` 与 `BuffDefinition.ParallelStorageMode` 已能传递配置。
- compressed Add / Refresh / Remove / Tick / Expire / Query / PendingRemove / Destroy 路径已通过 gate-protected 分支接入。
- `BuffSystemCore.CreateForCompressedParallelValidation(...)` 仅用于验证 Runner 创建 gate=true 实例。
- 正式 public constructor 仍保持 gate=false。

### eligibility 条件

只有同时满足以下条件，且 compressed gate 被开启时，才允许走 compressed runtime：

```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
compressed gate == enabled
```

### fallback 条件

以下任意条件成立时都必须 fallback 到 `EntityPerStack`：

```text
gate=false
EventTrigger parallel buff
Unlimited == true
MaxStack > CompressedParallelBuffLayerBuffer.Capacity
任何不满足 eligibility 的配置
```

fallback 后仍走旧 `EntityPerStack` 语义。

### gate 说明

public constructor 默认 gate=false。当前正式运行时仍默认 `EntityPerStack`。

`CreateForCompressedParallelValidation(...)` 是 internal test-only factory，只允许 validation runner 使用。业务代码不应使用 validation factory，也不应在 Phase 3G 前绕过 gate。

### 当前运行时语义

Add：

- 未存在 compressed runtime 时创建 `CompressedParallelBuffRuntimeComponent`。
- 每个 layer 分配稳定 `layerId` 和 `layerRuntimeHandle`。
- duration layer 使用 `expireFrame = createFrame + durationFrames`。
- forever layer 使用 `expireFrame = int.MaxValue`。

Refresh：

- `RefreshEarliest` 只刷新最早层。
- `RefreshAll` 刷新所有已有层。
- 刷新保留 `layerId / layerRuntimeHandle`，重置 `expireFrame / elapsedFrames / ticks`。

Remove：

- `RemoveEarliest` 移除最早层。
- `RemoveLatest` 移除最新层。
- `ClearAll` 移除全部层。

Tick / Expire：

- 新建 compressed layer 创建当帧不 Tick。
- `durationFrames = 1`：F1 Apply，F2 Tick + Remove。
- `durationFrames = 2`：F1 Apply，F2 Tick，F3 Tick + Remove。
- 同一运行帧内先处理 Tick，再处理 Expire。

Query：

- public `TryGetBuff / GetBuffs` API 不变。
- compressed aggregate 只对外暴露一个 `BuffViewData`，不暴露每层详情。
- Query / ViewCache 是只读语义，不触发 pending remove。

PendingRemove / Destroy：

- 最后一层 Remove / Expire / ClearAll 后，compressed runtime container 进入 pending remove。
- pending remove 使用 `compressedRuntimeHandle`，因为被删除的是 container entity。
- container pending remove 不额外触发聚合 Remove Effect。
- layer Remove 使用 `layerRuntimeHandle`。
- pending remove 后 `TryGetBuff / GetBuffs` 不显示。
- Destroy 前 defensive 清理 `_compressedRuntimeEntityByKey`。

ReplaceEarliestWhenFull：

- 未满层时 Append。
- 满层时移除最早层并追加新层。
- 新层生成新的 `layerId / layerRuntimeHandle`。
- 未替换层 identity 保持。
- 不假设 Remove callback 一定早于 Apply callback。
- Effect Flush 顺序仍由 Phase 2A phaseOrder 决定。

### ViewData 口径

compressed aggregate 的 ViewData 口径为：

```text
Stack = active layerCount
duration RemainingFrames = min(expireFrame - currentFrame)
forever RemainingFrames = -1
RuntimeHandle = min(active layerRuntimeHandle)
```

mixed duration / forever 不是常规配置路径；如果异常出现，按旧 `MergeViewData` 兼容语义倾向 `RemainingFrames = -1`。

注意：ViewData 口径不能和 Tick snapshot 口径混用。ViewData duration 使用 `expireFrame - currentFrame`，Tick snapshot 使用 `expireFrame - currentFrame + 1`。

### Tick / Effect snapshot 口径

```text
Tick snapshot RemainingFrames = expireFrame - currentFrame + 1
Remove snapshot RemainingFrames = 0
forever snapshot remainingFrames = 0
```

forever 的 ViewData 是 `RemainingFrames = -1`，但 runtime / effect snapshot 中 forever `remainingFrames` 可以保持 0。

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

### 后续流程

- Phase 3F-8：文档收敛。
- Phase 3G：小范围正式启用 eligible Tick 型 parallel buff。
- Phase 3H：性能对比、回滚快照验证、行为一致性验证。
- Phase 3I：评估是否扩大到更多 parallel buff 类型。

## Phase 3G-1 - whitelist gate skeleton

Phase 3G-1 在原有 compressed gate 基础上增加 configId 白名单门禁，但生产白名单默认空，不启用任何生产 Buff。正式 public constructor 路径仍保持 `EntityPerStack`。

`ShouldUseCompressedParallel` 当前逻辑为：

```text
_enableCompressedParallelRuntime
&& IsCompressedParallelWhitelisted(definition.ConfigId)
&& IsCompressedParallelEligible(definition)
```

生产路径：

- public constructor 仍传入 `gate=false`。
- public constructor 的 whitelist 为空。
- 所有生产 Buff 默认继续走 `EntityPerStack`。
- 非白名单 Buff 即使满足 eligibility，也 fallback 到 `EntityPerStack`。

验证路径：

- `CreateForCompressedParallelValidation(...)` 会创建 gate=true 的 validation instance。
- validation instance 使用测试白名单，仅包含 `BuffSystemCompressedParallelValidationRunner` 当前使用的测试 configId。
- validation whitelist 只用于 Runner 验证 compressed path，不是正式启用入口。

Phase 3G-1 仍不选择任何生产试点 configId。第一个生产试点必须留到 Phase 3G-2 单独审核。

