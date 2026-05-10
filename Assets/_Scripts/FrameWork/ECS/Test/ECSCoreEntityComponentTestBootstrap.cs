using System.Collections.Generic;
using UnityEngine;

public class ECSCoreEntityComponentTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Core Entity Component Test] Start</color>");

        TestCreateEntityAndCounts();
        TestSetGetTryHasComponent();
        TestOverwriteExistingComponentDoesNotChangeArcheType();
        TestRemoveComponentAndQuery();
        TestDestroyEntityRemovesComponentsAndInvalidatesHandle();
        TestEntityIdReuseRefreshesVersion();
        TestDeadEntitySafeApis();
        TestDisposeIgnoresFurtherOperations();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Core Entity Component Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Core Entity Component Test] Failed count = {_failedCount}");
    }

    private void TestCreateEntityAndCounts()
    {
        Debug.Log("<color=cyan>[Core Test 1] Create Entity And Counts</color>");

        World world = new World();
        EntityInfo e1 = world.CreateEntity();
        EntityInfo e2 = world.CreateEntity();

        Expect(e1.IsValid, "First entity should be valid.");
        Expect(e2.IsValid, "Second entity should be valid.");
        Expect(e1 != e2, "Two created entities should not be equal.");
        Expect(world.CreatedEntityCount == 2, $"CreatedEntityCount should be 2. Actual = {world.CreatedEntityCount}");
        Expect(world.AliveEntityCount == 2, $"AliveEntityCount should be 2. Actual = {world.AliveEntityCount}");
        Expect(world.FreeEntityCount == 0, $"FreeEntityCount should be 0. Actual = {world.FreeEntityCount}");
    }

    private void TestSetGetTryHasComponent()
    {
        Debug.Log("<color=cyan>[Core Test 2] Set / Get / TryGet / Has Component</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new CorePositionComponent { x = 1f, y = 2f, z = 3f });

        Expect(world.HasComponent<CorePositionComponent>(entity), "Entity should have CorePositionComponent.");
        Expect(world.TryGetComponent(entity, out CorePositionComponent copied), "TryGetComponent should return true.");
        Expect(Mathf.Approximately(copied.x, 1f) && Mathf.Approximately(copied.y, 2f) && Mathf.Approximately(copied.z, 3f), "TryGetComponent should return copied component data.");

        ref CorePositionComponent position = ref world.GetComponent<CorePositionComponent>(entity);
        position.x = 10f;

        Expect(Mathf.Approximately(world.GetComponent<CorePositionComponent>(entity).x, 10f), "GetComponent ref modification should write back to store.");
        Expect(world.ComponentStoreCount == 1, $"ComponentStoreCount should be 1. Actual = {world.ComponentStoreCount}");
        Expect(world.ArcheTypeCount == 1, $"ArcheTypeCount should be 1. Actual = {world.ArcheTypeCount}");
    }

    private void TestOverwriteExistingComponentDoesNotChangeArcheType()
    {
        Debug.Log("<color=cyan>[Core Test 3] Overwrite Existing Component Does Not Change ArcheType</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new CorePositionComponent { x = 1f, y = 0f, z = 0f });
        int versionBeforeOverwrite = world.ArcheTypeVersion;
        int archeTypeCountBeforeOverwrite = world.ArcheTypeCount;

        world.SetComponent(entity, new CorePositionComponent { x = 99f, y = 0f, z = 0f });

        Expect(world.ArcheTypeVersion == versionBeforeOverwrite, $"ArcheTypeVersion should not change after overwriting existing component. Before = {versionBeforeOverwrite}, After = {world.ArcheTypeVersion}");
        Expect(world.ArcheTypeCount == archeTypeCountBeforeOverwrite, $"ArcheTypeCount should not change after overwriting existing component. Before = {archeTypeCountBeforeOverwrite}, After = {world.ArcheTypeCount}");
        Expect(Mathf.Approximately(world.GetComponent<CorePositionComponent>(entity).x, 99f), "Existing component data should be overwritten.");
    }

    private void TestRemoveComponentAndQuery()
    {
        Debug.Log("<color=cyan>[Core Test 4] Remove Component And Query</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new CorePositionComponent { x = 1f, y = 0f, z = 0f });
        world.SetComponent(entity, new CoreVelocityComponent { x = 1f, y = 0f, z = 0f });

        List<EntityInfo> before = world.Query().With<CorePositionComponent>().With<CoreVelocityComponent>().Execute();
        int versionBeforeRemove = world.ArcheTypeVersion;

        bool removed = world.RemoveComponent<CoreVelocityComponent>(entity);
        List<EntityInfo> after = world.Query().With<CorePositionComponent>().With<CoreVelocityComponent>().Execute();

        Expect(removed, "RemoveComponent should return true for existing component.");
        Expect(before.Count == 1, $"Before remove, Position + Velocity query should return 1. Actual = {before.Count}");
        Expect(after.Count == 0, $"After remove, Position + Velocity query should return 0. Actual = {after.Count}");
        Expect(!world.HasComponent<CoreVelocityComponent>(entity), "Entity should not have CoreVelocityComponent after removal.");
        Expect(world.ArcheTypeVersion > versionBeforeRemove, "ArcheTypeVersion should increase after component removal.");
    }

    private void TestDestroyEntityRemovesComponentsAndInvalidatesHandle()
    {
        Debug.Log("<color=cyan>[Core Test 5] Destroy Entity Removes Components And Invalidates Handle</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new CorePositionComponent { x = 1f, y = 0f, z = 0f });
        world.SetComponent(entity, new CoreVelocityComponent { x = 1f, y = 0f, z = 0f });

        world.DestroyEntity(entity);

        Expect(!world.IsAlive(entity), "Destroyed entity should not be alive.");
        Expect(!world.HasComponent<CorePositionComponent>(entity), "Destroyed entity should safely report no Position component.");
        Expect(!world.TryGetComponent(entity, out CorePositionComponent _), "TryGetComponent on destroyed entity should return false.");
        Expect(world.AliveEntityCount == 0, $"AliveEntityCount should be 0 after destroy. Actual = {world.AliveEntityCount}");
        Expect(world.FreeEntityCount == 1, $"FreeEntityCount should be 1 after destroy. Actual = {world.FreeEntityCount}");
    }

    private void TestEntityIdReuseRefreshesVersion()
    {
        Debug.Log("<color=cyan>[Core Test 6] Entity ID Reuse Refreshes Version</color>");

        World world = new World();
        EntityInfo oldEntity = world.CreateEntity();
        int oldID = oldEntity.ID;
        int oldVersion = oldEntity.Version;

        world.DestroyEntity(oldEntity);
        EntityInfo reusedEntity = world.CreateEntity();

        Expect(reusedEntity.ID == oldID, $"Reused entity should reuse ID {oldID}. Actual = {reusedEntity.ID}");
        Expect(reusedEntity.Version > oldVersion, $"Reused entity version should be greater than old version. Old = {oldVersion}, New = {reusedEntity.Version}");
        Expect(!world.IsAlive(oldEntity), "Old handle should not be alive after ID reuse.");
        Expect(world.IsAlive(reusedEntity), "New reused handle should be alive.");
    }

    private void TestDeadEntitySafeApis()
    {
        Debug.Log("<color=cyan>[Core Test 7] Dead Entity Safe APIs</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();
        world.SetComponent(entity, new CorePositionComponent { x = 1f, y = 0f, z = 0f });
        world.DestroyEntity(entity);

        bool hasComponent = world.HasComponent<CorePositionComponent>(entity);
        bool tryGet = world.TryGetComponent(entity, out CorePositionComponent component);
        bool removeResult = world.RemoveComponent<CorePositionComponent>(entity);

        Expect(!hasComponent, "HasComponent on dead entity should return false.");
        Expect(!tryGet, "TryGetComponent on dead entity should return false.");
        Expect(!removeResult, "RemoveComponent on dead entity should return false.");
        Expect(component.Equals(default(CorePositionComponent)), "TryGetComponent should output default for dead entity.");
    }

    private void TestDisposeIgnoresFurtherOperations()
    {
        Debug.Log("<color=cyan>[Core Test 8] Dispose Ignores Further Operations</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();
        world.SetComponent(entity, new CorePositionComponent { x = 1f, y = 0f, z = 0f });
        world.AddSystem(new CoreCountingSystem());

        world.Dispose();

        EntityInfo invalid = world.CreateEntity();
        world.SetComponent(entity, new CoreVelocityComponent { x = 1f, y = 0f, z = 0f });
        world.DestroyEntity(entity);
        world.AddSystem(new CoreCountingSystem());
        world.Tick(new SimulationContext(1, 1f, false));

        Expect(world.CurrentState == WorldStates.Disposing, "World should remain in Disposing state after Dispose.");
        Expect(!invalid.IsValid, "CreateEntity during Disposing should return EntityInfo.Invalid.");
        Expect(world.PendingCommandCount == 0, $"No structural command should be recorded during Disposing. Actual = {world.PendingCommandCount}");
        Expect(world.PendingSystemCommandCount == 0, $"No system command should be recorded during Disposing. Actual = {world.PendingSystemCommandCount}");
    }

    private void Expect(bool condition, string message)
    {
        if (condition)
            Debug.Log($"<color=green>[PASS]</color> {message}");
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message}");
        }
    }
}

public struct CorePositionComponent : IComponentData
{
    public float x;
    public float y;
    public float z;
}

public struct CoreVelocityComponent : IComponentData
{
    public float x;
    public float y;
    public float z;
}

public class CoreCountingSystem : FixedStepSystemBase
{
    public int TickCount { get; private set; }
    public override SystemTickSequence sequence => SystemTickSequence.normal;

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
    }
}
