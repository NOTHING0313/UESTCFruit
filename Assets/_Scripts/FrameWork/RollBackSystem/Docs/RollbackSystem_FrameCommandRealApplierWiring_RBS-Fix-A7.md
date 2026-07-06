# RollbackSystem FrameCommand Real Applier Wiring - RBS-Fix-A7

## Goal

RBS-Fix-A7 connects rollback resimulation to the same `SimulationFrameCommandBuffer` and `SimulationFrameCommandApplier` used by normal `TimeSimulator` ticks.

This stage does not modify ECS Core, `TimeSimulator`, `SimulateRunner`, or the ECS FrameCommand implementation.

## Runtime Wiring

1. `SimulationInitializer` creates one `SimulationFrameCommandBuffer`.
2. `SimulationInitializer` creates one `SimulationFrameCommandApplier` for the active `World` and that buffer.
3. `SimulationInitializer` injects the applier with `TimeSimulator.SetFrameCommandApplier`.
4. `SimulationInitializer` resolves an optional `RollbackBootstrap` reference and asks it to mount before the first simulation tick.
5. `RollbackBootstrap` reads `TimeSimulator.DebugFrameCommandBuffer` and `TimeSimulator.DebugFrameCommandApplier`.
6. `RollbackBootstrap` refuses late mount if the runner has already advanced.
7. `RollbackBootstrap` refuses to mount if either FrameCommand instance is missing or if the buffer is not the applier's buffer.
8. `WorldRollbackAdapter` receives an internal `RollbackFrameCommandReplayBinding`.
9. `RollbackCoordinator` uses that binding during rollback resimulation for both `BeforeTick` and `AfterTick` frame command replay.
10. `RollbackCoordinator.ConfirmFrame` asks the adapter to remove frame command history before the confirmed frame.

## Mount Timing Hardening

`SimulationInitializer` owns the real frame command pipeline and is the preferred caller of `RollbackBootstrap.TryMount`.

Rollback bootstrap reference resolution is explicit and deterministic:

1. If `SimulationInitializer._rollbackBootstrap` is assigned in the Inspector, that reference is used.
2. If it is not assigned, `SimulationInitializer` falls back to a same-GameObject `RollbackBootstrap` for old scene compatibility.
3. If no bootstrap is configured, rollback mount is skipped and normal simulation continues.

`SimulationInitializer` does not use `FindObjectOfType` or `FindObjectsByType` to pick a bootstrap globally. If `RollbackBootstrap` is on another GameObject, assign it explicitly in the Inspector.

`RollbackBootstrap.TryMount` is idempotent. Repeated calls after a successful mount return success without rebuilding the coordinator, resetting frame state, or subscribing events again.

`RollbackBootstrap.OnDisable` and `RollbackBootstrap.OnDestroy` unmount the bootstrap and unsubscribe from runner events. Runtime disable unmounts rollback. Re-enable after simulation has advanced is not guaranteed to remount safely in this stage.

The coroutine fallback is compatibility-only. It will not mount onto a runner that already advanced.

## Unity .meta Policy

Unity may generate `.meta` files for new script or documentation assets. Codex does not edit, delete, revert, or stage `.meta` files in this stage. Whether generated `.meta` files should be committed is a user/project decision.

## Manual Unity Validation

1. Open Unity and wait for script compilation.
2. Clear the Console.
3. In the current same-GameObject scene, enter Play Mode.
4. Verify `Mounted` appears once.
5. Verify there is no continuous `FrameMismatch expected frame 1` log.
6. Verify there is no `FrameCommand replay binding failed` log.
7. For a cross-GameObject scene, put `RollbackBootstrap` on another GameObject and explicitly assign it to `SimulationInitializer._rollbackBootstrap`.
8. Enter Play Mode and verify first-frame mount succeeds the same way.
9. Test an unconfigured scene with no assigned bootstrap and no same-GameObject bootstrap; normal simulation should continue and rollback should be skipped without an Error.
10. In Play Mode, disable `RollbackBootstrap`; it should unmount and stop receiving runner callbacks without repeated tick, null reference, or FrameMismatch spam.

## Failure Semantics

Rollback resimulation now fails fast if frame command replay is unavailable or if replay fails. It no longer logs a warning and silently continues.

New `RollbackResimulateFailureKind` values:

- `FrameCommandReplayUnavailable`
- `FrameCommandReplayFailed`
- `FrameCommandHistoryCleanupFailed`

`ConfirmFrame` cleanup failure is currently diagnostic-only. The confirmed-frame buffers still clear as before, and a warning is logged if frame command history cleanup cannot be reached.

## Logic-Only Manual Validation

Attach `RollbackCoordinatorLogicOnlyTestBootstrap` to a temporary GameObject and run:

`Run Logic-Only Rollback Tests`

A7 adds checks for:

- resimulation fails when frame command replay is unavailable;
- `WorldRollbackAdapter` replays through the real `SimulationFrameCommandApplier`;
- `ConfirmFrame` removes both command-buffer history and applied-command markers.

Also verify in a scene containing `SimulationInitializer`, `TimeSimulator`, and `RollbackBootstrap`:

- `RollbackBootstrap` mounts successfully;
- no log reports `FrameCommand replay binding failed`;
- normal ticks still run through `TimeSimulator`;
- rollback resimulation no longer reports skipped frame command replay.

## Non-Goals

- This does not claim a full production netcode rollback closure.
- This does not add server reconciliation protocol, remote command transport, or catch-up ownership.
- This does not change ECS Core, Snapshot internals, `TimeSimulator`, or `SimulateRunner`.
