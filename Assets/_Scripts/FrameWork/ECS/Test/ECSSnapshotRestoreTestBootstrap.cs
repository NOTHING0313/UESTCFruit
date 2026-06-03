using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 验证 ECS World-level Snapshot Capture / Restore 的核心契约。
/// </summary>
public sealed class ECSSnapshotRestoreTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Snapshot Restore Test] Start</color>");

        TestCaptureSnapshotShouldRejectNegativeFrameNumber();
        TestCaptureAndRestoreShouldRejectWhenWorldIsTickingAndStructuralCommandsArePending();
        TestCaptureAndRestoreShouldRejectWhenPendingSystemCommandsExist();
        TestRestoreSnapshotShouldPreserveEntityIdVersionAndAlive();
        TestRestoreSnapshotShouldPreserveFutureEntityIdReuseOrder();
        TestRestoreSnapshotShouldRestoreComponentValuesAndRemovedComponents();
        TestRestoreSnapshotShouldRemoveStoresNotPresentInSnapshot();
        TestRestoreSnapshotShouldPreserveComponentStoreDenseOrder();
        TestRestoreSnapshotShouldRebuildArchetypeQueries();
        TestRestoreSnapshotShouldRestoreSingletonMappings();
        TestRestoreSnapshotShouldClearWorldEvents();
        TestRestoreSnapshotWhenSnapshotInvalidShouldNotMutateWorld();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Snapshot Restore Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Snapshot Restore Test] Failed count = {_failedCount}");
    }

    private void TestCaptureSnapshotShouldRejectNegativeFrameNumber()
    {
        Debug.Log("<color=cyan>[Snapshot Test 1] Capture Rejects Negative Frame Number</color>");

        World world = new World();
        bool tryRejected = !world.TryCaptureSnapshot(-1, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result);
        bool throwRejected = ThrowsInvalidOperation(() => world.CaptureSnapshot(-1));

        Expect(tryRejected, "TryCaptureSnapshot should reject negative frame number.");
        Expect(snapshot == null, "TryCaptureSnapshot should output null snapshot on negative frame number.");
        Expect(result != null && !result.Success && !string.IsNullOrEmpty(result.ErrorMessage), "TryCaptureSnapshot should return failure result with error message.");
        Expect(throwRejected, "CaptureSnapshot should throw InvalidOperationException on negative frame number.");
    }

    private void TestCaptureAndRestoreShouldRejectWhenWorldIsTickingAndStructuralCommandsArePending()
    {
        Debug.Log("<color=cyan>[Snapshot Test 2] Capture / Restore Reject While Ticking And Structural Pending</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new SnapshotPositionComponent { x = 1f, y = 0f, z = 0f });
        EcsWorldSnapshot snapshot = world.CaptureSnapshot(10);

        SnapshotBoundaryProbeSystem system = new SnapshotBoundaryProbeSystem(entity, snapshot);
        world.AddSystem(system);
        world.Tick(new SimulationContext(11, 1f, false));

        Expect(system.TickCount == 1, "Boundary probe system should tick once.");
        Expect(system.PendingCommandCountDuringTick > 0, "Boundary probe should create structural pending command during Tick.");
        Expect(system.CaptureRejectedDuringTick, "TryCaptureSnapshot should reject while World is Ticking.");
        Expect(system.RestoreRejectedDuringTick, "TryRestoreSnapshot should reject while World is Ticking.");
    }

    private void TestCaptureAndRestoreShouldRejectWhenPendingSystemCommandsExist()
    {
        Debug.Log("<color=cyan>[Snapshot Test 3] Capture / Restore Reject Pending System Commands</color>");

        World world = new World();
        EcsWorldSnapshot snapshot = world.CaptureSnapshot(20);

        SnapshotPassiveSystem nestedSystem = new SnapshotPassiveSystem();
        SnapshotAddNestedOnCreateSystem firstAddedSystem = new SnapshotAddNestedOnCreateSystem(nestedSystem);
        SnapshotAddSystemDuringTickSystem requestAddSystem = new SnapshotAddSystemDuringTickSystem(firstAddedSystem);

        world.AddSystem(requestAddSystem);
        world.Tick(new SimulationContext(21, 1f, false));

        bool pendingSystemCommand = world.PendingSystemCommandCount > 0;
        bool captureRejected = !world.TryCaptureSnapshot(22, out _, out EcsWorldSnapshotCaptureResult captureResult);
        bool restoreRejected = !world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult restoreResult);

        Expect(pendingSystemCommand, "World should have pending system command after nested OnCreate request.");
        Expect(captureRejected && !captureResult.Success, "TryCaptureSnapshot should reject while system commands are pending.");
        Expect(restoreRejected && !restoreResult.Success, "TryRestoreSnapshot should reject while system commands are pending.");
    }

    private void TestRestoreSnapshotShouldPreserveEntityIdVersionAndAlive()
    {
        Debug.Log("<color=cyan>[Snapshot Test 4] Restore Preserves Entity ID / Version / Alive</color>");

        World world = new World();
        Entity first = world.CreateEntity();
        Entity second = world.CreateEntity();
        world.SetComponent(first, new SnapshotPositionComponent { x = 1f, y = 0f, z = 0f });
        world.SetComponent(second, new SnapshotPositionComponent { x = 2f, y = 0f, z = 0f });

        EcsWorldSnapshot snapshot = world.CaptureSnapshot(30);
        world.DestroyEntity(first);
        Entity postSnapshotEntity = world.CreateEntity();
        world.SetComponent(postSnapshotEntity, new SnapshotPositionComponent { x = 99f, y = 0f, z = 0f });

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "TryRestoreSnapshot should succeed for valid entity snapshot."))
            return;

        Expect(world.IsAlive(first), "First entity handle from snapshot should be alive after restore.");
        Expect(world.IsAlive(second), "Second entity handle from snapshot should be alive after restore.");
        Expect(!world.IsAlive(postSnapshotEntity), "Entity created after snapshot should not be alive after restore.");
        Expect(world.AliveEntityCount == 2, $"AliveEntityCount should be restored to 2. Actual = {world.AliveEntityCount}");
    }

    private void TestRestoreSnapshotShouldPreserveFutureEntityIdReuseOrder()
    {
        Debug.Log("<color=cyan>[Snapshot Test 5] Restore Preserves Future Entity ID Reuse Order</color>");

        World world = new World();
        Entity first = world.CreateEntity();
        world.CreateEntity();
        Entity third = world.CreateEntity();

        world.DestroyEntity(first);
        world.DestroyEntity(third);
        EcsWorldSnapshot snapshot = world.CaptureSnapshot(40);

        Entity expectedNext = world.CreateEntity();
        Entity expectedSecond = world.CreateEntity();

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed before checking future ID reuse order."))
            return;

        Entity actualNext = world.CreateEntity();
        Entity actualSecond = world.CreateEntity();

        Expect(actualNext.ID == expectedNext.ID, $"First reused ID should match snapshot future order. Expected = {expectedNext.ID}, Actual = {actualNext.ID}");
        Expect(actualNext.Version == expectedNext.Version, $"First reused version should match. Expected = {expectedNext.Version}, Actual = {actualNext.Version}");
        Expect(actualSecond.ID == expectedSecond.ID, $"Second reused ID should match snapshot future order. Expected = {expectedSecond.ID}, Actual = {actualSecond.ID}");
        Expect(actualSecond.Version == expectedSecond.Version, $"Second reused version should match. Expected = {expectedSecond.Version}, Actual = {actualSecond.Version}");
    }

    private void TestRestoreSnapshotShouldRestoreComponentValuesAndRemovedComponents()
    {
        Debug.Log("<color=cyan>[Snapshot Test 6] Restore Component Values And Removed Components</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new SnapshotPositionComponent { x = 1f, y = 2f, z = 3f });
        world.SetComponent(entity, new SnapshotVelocityComponent { x = 4f, y = 5f, z = 6f });

        EcsWorldSnapshot snapshot = world.CaptureSnapshot(50);
        world.SetComponent(entity, new SnapshotPositionComponent { x = 99f, y = 99f, z = 99f });
        world.RemoveComponent<SnapshotVelocityComponent>(entity);

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed for component value snapshot."))
            return;

        bool positionRestored = world.TryGetComponent(entity, out SnapshotPositionComponent position)
            && NearlyEqual(position.x, 1f)
            && NearlyEqual(position.y, 2f)
            && NearlyEqual(position.z, 3f);
        bool velocityRestored = world.TryGetComponent(entity, out SnapshotVelocityComponent velocity)
            && NearlyEqual(velocity.x, 4f)
            && NearlyEqual(velocity.y, 5f)
            && NearlyEqual(velocity.z, 6f);

        Expect(positionRestored, "Position component value should be restored.");
        Expect(velocityRestored, "Removed Velocity component should be restored from snapshot.");
    }

    private void TestRestoreSnapshotShouldRemoveStoresNotPresentInSnapshot()
    {
        Debug.Log("<color=cyan>[Snapshot Test 7] Restore Removes Stores Not Present In Snapshot</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new SnapshotPositionComponent { x = 1f, y = 0f, z = 0f });

        EcsWorldSnapshot snapshot = world.CaptureSnapshot(60);
        world.SetComponent(entity, new SnapshotExtraComponent { value = 777 });

        bool hadExtraBeforeRestore = world.HasComponent<SnapshotExtraComponent>(entity)
            && world.Query().With<SnapshotExtraComponent>().Execute().Count == 1;
        Expect(hadExtraBeforeRestore, "Extra component store should exist before restore.");

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed when removing post-snapshot store."))
            return;

        bool extraRemoved = !world.HasComponent<SnapshotExtraComponent>(entity)
            && world.Query().With<SnapshotExtraComponent>().Execute().Count == 0;

        Expect(extraRemoved, "Component store absent from snapshot should be removed after restore.");
    }

    private void TestRestoreSnapshotShouldPreserveComponentStoreDenseOrder()
    {
        Debug.Log("<color=cyan>[Snapshot Test 8] Restore Preserves ComponentStore Dense Order</color>");

        World world = new World();
        Entity first = CreatePosition(world, 1f);
        Entity second = CreatePosition(world, 2f);
        Entity third = CreatePosition(world, 3f);

        EcsWorldSnapshot snapshot = world.CaptureSnapshot(70);

        world.DestroyEntity(second);
        Entity fourth = CreatePosition(world, 4f);
        world.Query().With<SnapshotPositionComponent>().Execute();

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed before dense order check."))
            return;

        List<Entity> afterRestore = world.Query().With<SnapshotPositionComponent>().Execute();

        Expect(afterRestore.Count == 3, $"Position query should return 3 restored entities. Actual = {afterRestore.Count}");
        Expect(IsOrdered(afterRestore, first, second, third), "Position query order should match captured ComponentStore dense order.");
        Expect(!ContainsEntity(afterRestore, fourth), "Post-snapshot entity should not remain in restored dense order.");
    }

    private void TestRestoreSnapshotShouldRebuildArchetypeQueries()
    {
        Debug.Log("<color=cyan>[Snapshot Test 9] Restore Rebuilds ArcheType Queries</color>");

        World world = new World();
        Entity positionOnly = world.CreateEntity();
        world.SetComponent(positionOnly, new SnapshotPositionComponent { x = 1f, y = 0f, z = 0f });

        Entity moving = world.CreateEntity();
        world.SetComponent(moving, new SnapshotPositionComponent { x = 2f, y = 0f, z = 0f });
        world.SetComponent(moving, new SnapshotVelocityComponent { x = 1f, y = 0f, z = 0f });

        List<Entity> before = world.Query().With<SnapshotPositionComponent>().With<SnapshotVelocityComponent>().Execute();
        EcsWorldSnapshot snapshot = world.CaptureSnapshot(80);

        world.SetComponent(positionOnly, new SnapshotVelocityComponent { x = 5f, y = 0f, z = 0f });
        List<Entity> mutated = world.Query().With<SnapshotPositionComponent>().With<SnapshotVelocityComponent>().Execute();

        Expect(before.Count == 1 && ContainsEntity(before, moving), "Before mutation, Position + Velocity query should contain only moving entity.");
        Expect(mutated.Count == 2, "After mutation, Position + Velocity query should contain two entities.");

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed before Query rebuild check."))
            return;

        bool cacheCleared = world.QueryCacheCount == 0;
        List<Entity> afterRestore = world.Query().With<SnapshotPositionComponent>().With<SnapshotVelocityComponent>().Execute();

        Expect(cacheCleared, "QueryCache should be cleared by restore before next query.");
        Expect(afterRestore.Count == 1 && ContainsEntity(afterRestore, moving) && !ContainsEntity(afterRestore, positionOnly), "After restore, Query should match snapshot archetypes.");
    }

    private void TestRestoreSnapshotShouldRestoreSingletonMappings()
    {
        Debug.Log("<color=cyan>[Snapshot Test 10] Restore Singleton Mappings</color>");

        World world = new World();
        Entity singletonEntity = world.SetSingleton(new SnapshotGameStateComponent { frame = 1, score = 10 });
        EcsWorldSnapshot snapshot = world.CaptureSnapshot(90);

        world.RemoveSingleton<SnapshotGameStateComponent>();
        world.SetSingleton(new SnapshotGameStateComponent { frame = 9, score = 99 });

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed before checking singleton mapping."))
            return;

        bool mappingRestored = world.TryGetSingletonEntity<SnapshotGameStateComponent>(out Entity restoredEntity)
            && restoredEntity == singletonEntity;
        bool valueRestored = world.TryGetSingleton(out SnapshotGameStateComponent restoredValue)
            && restoredValue.frame == 1
            && restoredValue.score == 10;

        Expect(result.RestoredSingletonCount == 1, "Restore should report one restored singleton.");
        Expect(mappingRestored, "Singleton entity mapping should be restored.");
        Expect(valueRestored, "Singleton component value should be restored.");
    }

    private void TestRestoreSnapshotShouldClearWorldEvents()
    {
        Debug.Log("<color=cyan>[Snapshot Test 11] Restore Clears WorldEventBuffer</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new SnapshotPositionComponent { x = 1f, y = 0f, z = 0f });
        EcsWorldSnapshot snapshot = world.CaptureSnapshot(100);

        world.AddWorldEvent(new SnapshotTestWorldEvent(101, 42));
        bool eventWritten = world.WorldEventCount == 1 && world.GetWorldEvents<SnapshotTestWorldEvent>().Count == 1;
        Expect(eventWritten, "WorldEvent should exist before restore.");

        bool restored = world.TryRestoreSnapshot(snapshot, out EcsWorldSnapshotRestoreResult result);

        if (!ExpectRestoreSuccess(restored, result, "Restore should succeed before checking WorldEvent clear."))
            return;

        Expect(world.WorldEventCount == 0, $"WorldEventBuffer should be cleared after restore. Actual = {world.WorldEventCount}");
        Expect(world.GetWorldEvents<SnapshotTestWorldEvent>().Count == 0, "Typed WorldEvent list should be empty after restore.");
    }

    private void TestRestoreSnapshotWhenSnapshotInvalidShouldNotMutateWorld()
    {
        Debug.Log("<color=cyan>[Snapshot Test 12] Invalid Snapshot Does Not Mutate World</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new SnapshotPositionComponent { x = 3f, y = 0f, z = 0f });
        Entity singletonEntity = world.SetSingleton(new SnapshotGameStateComponent { frame = 3, score = 30 });
        world.AddWorldEvent(new SnapshotTestWorldEvent(120, 1200));

        EcsWorldSnapshot validSnapshot = world.CaptureSnapshot(120);
        EcsWorldSnapshot invalidSnapshot = CreateInvalidComponentValueSnapshot(validSnapshot, entity);

        bool rejected = !world.TryRestoreSnapshot(invalidSnapshot, out EcsWorldSnapshotRestoreResult result);
        bool positionUnchanged = world.TryGetComponent(entity, out SnapshotPositionComponent position)
            && NearlyEqual(position.x, 3f);
        bool singletonUnchanged = world.TryGetSingletonEntity<SnapshotGameStateComponent>(out Entity currentSingletonEntity)
            && currentSingletonEntity == singletonEntity
            && world.TryGetSingleton(out SnapshotGameStateComponent state)
            && state.frame == 3
            && state.score == 30;
        bool eventUnchanged = world.WorldEventCount == 1;

        Expect(rejected && !result.Success, "Invalid snapshot should be rejected.");
        Expect(positionUnchanged, "Invalid snapshot should not mutate existing component value.");
        Expect(singletonUnchanged, "Invalid snapshot should not mutate singleton mapping or value.");
        Expect(eventUnchanged, "Invalid snapshot should not clear WorldEventBuffer before validation succeeds.");
    }

    private Entity CreatePosition(World world, float x)
    {
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new SnapshotPositionComponent { x = x, y = 0f, z = 0f });
        return entity;
    }

    private EcsWorldSnapshot CreateInvalidComponentValueSnapshot(EcsWorldSnapshot source, Entity entity)
    {
        List<EcsComponentSnapshot> components = new List<EcsComponentSnapshot>
        {
            new EcsComponentSnapshot(entity, new SnapshotVelocityComponent { x = 9f, y = 0f, z = 0f })
        };

        List<EcsComponentStoreSnapshot> stores = new List<EcsComponentStoreSnapshot>
        {
            new EcsComponentStoreSnapshot(typeof(SnapshotPositionComponent), 0, components)
        };

        return new EcsWorldSnapshot(source.FrameNumber, source.RegisteredComponentTypes, source.EntityManager, stores, Array.Empty<EcsSingletonSnapshot>());
    }

    private bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private bool ContainsEntity(List<Entity> entities, Entity entity)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] == entity)
                return true;
        }

        return false;
    }

    private bool IsOrdered(List<Entity> entities, Entity first, Entity second, Entity third)
    {
        return entities.Count == 3
            && entities[0] == first
            && entities[1] == second
            && entities[2] == third;
    }

    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
            return;
        }

        _failedCount++;
        Debug.LogError($"[FAIL] {message}");
    }

    private bool ExpectRestoreSuccess(bool restored, EcsWorldSnapshotRestoreResult result, string message)
    {
        if (restored && result != null && result.Success)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
            return true;
        }

        string errorMessage = result != null ? result.ErrorMessage : "<null result>";

        if (string.IsNullOrEmpty(errorMessage))
            errorMessage = "<empty>";

        _failedCount++;
        Debug.LogError($"[FAIL] {message}");
        Debug.LogError($"[RESTORE ERROR] restored = {restored}, resultSuccess = {result != null && result.Success}, error = {errorMessage}");
        return false;
    }

    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private struct SnapshotPositionComponent : IComponentData
    {
        public float x;
        public float y;
        public float z;
    }

    private struct SnapshotVelocityComponent : IComponentData
    {
        public float x;
        public float y;
        public float z;
    }

    private struct SnapshotExtraComponent : IComponentData
    {
        public int value;
    }

    private struct SnapshotGameStateComponent : IComponentData
    {
        public int frame;
        public int score;
    }

    private readonly struct SnapshotTestWorldEvent : IWorldEvent
    {
        public int frameNumber { get; }
        public readonly int value;

        public SnapshotTestWorldEvent(int frameNumber, int value)
        {
            this.frameNumber = frameNumber;
            this.value = value;
        }
    }

    private sealed class SnapshotBoundaryProbeSystem : FixedStepSystemBase
    {
        private readonly Entity _entity;
        private readonly EcsWorldSnapshot _snapshot;

        public int TickCount { get; private set; }
        public int PendingCommandCountDuringTick { get; private set; }
        public bool CaptureRejectedDuringTick { get; private set; }
        public bool RestoreRejectedDuringTick { get; private set; }

        public override SystemTickSequence sequence => SystemTickSequence.normal;

        public SnapshotBoundaryProbeSystem(Entity entity, EcsWorldSnapshot snapshot)
        {
            _entity = entity;
            _snapshot = snapshot;
        }

        public override void Tick(in SimulationContext context)
        {
            TickCount++;
            World.SetComponent(_entity, new SnapshotVelocityComponent { x = 1f, y = 0f, z = 0f });
            PendingCommandCountDuringTick = World.PendingCommandCount;
            CaptureRejectedDuringTick = !World.TryCaptureSnapshot(context.frameNumber, out _, out _);
            RestoreRejectedDuringTick = !World.TryRestoreSnapshot(_snapshot, out _);
        }
    }

    private sealed class SnapshotAddSystemDuringTickSystem : FixedStepSystemBase
    {
        private readonly IFixedStepSystem _systemToAdd;
        public override SystemTickSequence sequence => SystemTickSequence.normal;

        public SnapshotAddSystemDuringTickSystem(IFixedStepSystem systemToAdd)
        {
            _systemToAdd = systemToAdd;
        }

        public override void Tick(in SimulationContext context)
        {
            World.AddSystem(_systemToAdd);
        }
    }

    private sealed class SnapshotAddNestedOnCreateSystem : FixedStepSystemBase
    {
        private readonly IFixedStepSystem _nestedSystem;
        public override SystemTickSequence sequence => SystemTickSequence.normal;

        public SnapshotAddNestedOnCreateSystem(IFixedStepSystem nestedSystem)
        {
            _nestedSystem = nestedSystem;
        }

        protected override void OnSystemCreate()
        {
            World.AddSystem(_nestedSystem);
        }

        public override void Tick(in SimulationContext context)
        {
        }
    }

    private sealed class SnapshotPassiveSystem : FixedStepSystemBase
    {
        public override SystemTickSequence sequence => SystemTickSequence.normal;

        public override void Tick(in SimulationContext context)
        {
        }
    }
}

}
