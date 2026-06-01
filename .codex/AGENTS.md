# Local Codex Instructions for BuffSystem ECS Migration

## Scope

These instructions apply only to this repository and this Codex session.

Do not modify the user's global Codex instructions, global AGENTS.md, global AGENTS.override.md, or any file outside this repository.

The target work area is the first BuffSystem from FrameWork.zip.

FrameWork2.zip is a reference implementation only. Its advantages should be migrated conceptually, but the first BuffSystem must remain ECS-centered.

## Core Architecture Rules

1. Keep BuffSystem ECS-centered.
   - Runtime identity must use Entity or the existing ECS entity abstraction.
   - Do not reintroduce GameObject as the runtime Buff target/source.
   - Do not make runtime Buff logic depend on MonoBehaviour lifecycle.
   - Do not use UnityEngine.Time, Time.time, or Time.deltaTime for simulation timing.
   - Use SimulationContext.frameNumber or the existing fixed-frame simulation clock.

2. Keep the runtime deterministic and rollback-friendly.
   - Prefer int frame numbers over float time.
   - Avoid object, dynamic, reflection-heavy runtime paths, and non-deterministic containers in hot simulation code.
   - Avoid Dictionary<int, object> as rollback-critical Effect state.
   - Avoid ScriptableObject as the runtime executor.
   - ScriptableObject may be used only as editor/authoring data if converted into deterministic runtime definitions.

3. Preserve existing public API compatibility where practical.
   - If an API must change, add compatibility wrappers or obsolete aliases when reasonable.
   - Document every changed interface, method, enum, component, lifecycle hook, and query result in BuffSystem/Docs.

4. Treat FrameWork2 BuffSystem as a feature reference.
   Migrate its advantages into the ECS BuffSystem:
   - Effect request queue / deferred effect flush.
   - Ordered effect execution.
   - Safer removal and delayed destruction.
   - Rich stack policy semantics.
   - Parallel buff layer expiry management.
   - Composite effect behavior.
   - Strategy extensibility.
   - Clearer API and usage examples.
   Do not migrate:
   - GameObject runtime binding.
   - MonoBehaviour update loop.
   - Time.time / Time.deltaTime timing.
   - ScriptableObject runtime effects.
   - String strategy IDs in hot paths.
   - object-boxed effect state in rollback-critical logic.

## Approval Gate

Before making any file changes, Codex must output an implementation proposal and wait for user approval.

The proposal must include:

1. Goal
2. Files to inspect
3. Files expected to modify
4. Public API changes
5. Runtime behavior changes
6. Documentation files to update
7. Test or validation plan
8. Rollback risk
9. Whether DeepSeek MCP generator will be used
10. Exact patch scope

Codex must not edit files until the user explicitly approves.

Allowed approval phrases include:
- “批准执行”
- “同意执行”
- “按此方案执行”
- “可以改”

If the user asks for a revised plan, revise the plan only. Do not implement.

## DeepSeek MCP Generator Usage

To save quota, Codex may call the MCP DeepSeek generator only for simple and clearly scoped code generation.

Allowed uses:
- Small pure C# helper class.
- Simple enum or policy executor boilerplate.
- Markdown documentation draft.
- Unit test skeleton.
- Straightforward adapter or compatibility wrapper.

Forbidden uses:
- Deciding architecture.
- Rewriting the whole BuffSystem.
- Changing ECS ownership model.
- Designing rollback semantics.
- Making final API decisions without Codex review.
- Producing code that Codex applies without inspection.

Maximum rework count is 5.

Codex must track this in every implementation summary:

DeepSeek rework count: N / 5

If DeepSeek-generated code is used, Codex must:
1. State what was generated.
2. Review it for project conventions.
3. Adapt it to ECS / deterministic runtime requirements.
4. Run or describe validation.
5. Include the result in the final diff summary.

## Required Documentation Policy

Any BuffSystem interface, API, enum, lifecycle, component, query type, configuration field, event type, effect executor, or stack policy change must update BuffSystem/Docs.

Every documentation update must include:

1. Name
2. Purpose
3. Runtime behavior
4. Parameters or fields
5. Return value if applicable
6. Lifecycle timing if applicable
7. Determinism / rollback notes if applicable
8. Usage example
9. Migration note if it changes old behavior

Documentation must be written clearly enough for a developer to use the feature without reading implementation code.

## Required Docs

Maintain or create these documents:

- BuffSystem/Docs/BuffSystem_Overview.md
- BuffSystem/Docs/BuffSystem_API.md
- BuffSystem/Docs/BuffSystem_StackPolicy.md
- BuffSystem/Docs/BuffSystem_EffectPipeline.md
- BuffSystem/Docs/BuffSystem_EventPipeline.md
- BuffSystem/Docs/BuffSystem_ParallelBuff.md
- BuffSystem/Docs/BuffSystem_Migration_From_Framework2.md
- BuffSystem/Docs/BuffSystem_Examples.md
- BuffSystem/Docs/BuffSystem_Changelog.md

## Implementation Priorities

Follow this order unless the user approves a different order.

### Phase 1: Low-risk semantic fixes

- Add or implement duration-reset-only semantics equivalent to FrameWork2 ResetRuntimeBuffStackUpStrategy.
- Ensure timing reset policies reset elapsed frames and tick count when appropriate.
- Fix suspicious constructors or APIs where passed frameNumber is ignored.
- Reduce unnecessary ViewCache dirty marking where only internal time changes.
- Add tests or validation examples.

### Phase 2: Effect request pipeline

- Introduce ECS-compatible BuffEffectRequest queue.
- Defer effect execution until a deterministic flush point.
- Sort queued effects deterministically.
- Delay runtime entity destruction until remove effects are flushed.
- Avoid object boxing on hot generic event paths where practical.

### Phase 3: Parallel buff optimization

- Add compressed parallel runtime mode based on fixed expiry frames.
- Do not use float time.
- Preserve existing entity-per-stack behavior if needed for compatibility.
- Add tests for append, refresh earliest, refresh all, replace earliest when full, remove earliest, remove latest, and clear all.

### Phase 4: Extensible stack strategies

- Keep enum-based built-in policies for deterministic defaults.
- Add optional pure C# policy executor registry using stable int IDs.
- Do not use string IDs in hot runtime paths.
- Convert authoring names to stable IDs before simulation.

### Phase 5: Composite effects and authoring support

- Add pure C# CompositeBuffEffectExecutor.
- Optionally support editor authoring assets that convert to deterministic runtime EffectId definitions.
- Do not execute ScriptableObject directly in runtime simulation.

## Review Requirements

After implementation, Codex must provide:

1. Files changed
2. Summary of behavior changes
3. Public API changes
4. Docs updated
5. Tests run or validation performed
6. Remaining risks
7. DeepSeek rework count
8. Suggested next step

Before finalizing, Codex must review its own diff for:
- Non-ECS runtime dependencies.
- Time.time or Time.deltaTime usage.
- GameObject leakage into runtime logic.
- ScriptableObject runtime executors.
- Object-boxed rollback-critical state.
- Missing docs.
- Missing examples.
- Missing migration notes.