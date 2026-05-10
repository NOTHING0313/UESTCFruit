using System.Collections.Generic;
using UnityEngine;

public class ECSQueryCacheRegressionTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Query Cache Regression Test] Start</color>");

        TestWithAndWithoutQuery();
        TestSameMaskDifferentOrderReusesCache();
        TestDifferentQueryCreatesNewCacheItem();
        TestCacheInvalidatesWhenNewMatchingArcheTypeAppears();
        TestCacheInvalidatesAfterRemoveExcludeComponent();
        TestQueryExecuteSnapshotBeforeImmediateStructuralChanges();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Query Cache Regression Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Query Cache Regression Test] Failed count = {_failedCount}");
    }

    private void TestWithAndWithoutQuery()
    {
        Debug.Log("<color=cyan>[Query Test 1] With / Without Query</color>");

        World world = new World();

        EntityInfo e1 = CreatePV(world);
        EntityInfo e2 = CreatePV(world);
        EntityInfo e3 = world.CreateEntity();
        EntityInfo e4 = world.CreateEntity();
        EntityInfo e5 = CreatePV(world);

        world.SetComponent(e2, new QueryDeadTagComponent());
        world.SetComponent(e3, new QueryPositionComponent { x = 3f, y = 0f, z = 0f });
        world.SetComponent(e4, new QueryVelocityComponent { x = 4f, y = 0f, z = 0f });
        world.SetComponent(e5, new QueryFrozenTagComponent());

        List<EntityInfo> pv = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Execute();
        List<EntityInfo> pvWithoutDead = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Without<QueryDeadTagComponent>().Execute();
        List<EntityInfo> pvWithoutDeadFrozen = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Without<QueryDeadTagComponent>().Without<QueryFrozenTagComponent>().Execute();

        Expect(pv.Count == 3, $"Position + Velocity query should return 3. Actual = {pv.Count}");
        Expect(ContainsEntity(pv, e1) && ContainsEntity(pv, e2) && ContainsEntity(pv, e5), "Position + Velocity query should contain e1, e2 and e5.");
        Expect(pvWithoutDead.Count == 2, $"Position + Velocity without Dead should return 2. Actual = {pvWithoutDead.Count}");
        Expect(ContainsEntity(pvWithoutDead, e1) && ContainsEntity(pvWithoutDead, e5) && !ContainsEntity(pvWithoutDead, e2), "Without Dead should include e1/e5 and exclude e2.");
        Expect(pvWithoutDeadFrozen.Count == 1, $"Position + Velocity without Dead and Frozen should return 1. Actual = {pvWithoutDeadFrozen.Count}");
        Expect(ContainsEntity(pvWithoutDeadFrozen, e1) && !ContainsEntity(pvWithoutDeadFrozen, e2) && !ContainsEntity(pvWithoutDeadFrozen, e5), "Without Dead/Frozen should only include e1.");
    }

    private void TestSameMaskDifferentOrderReusesCache()
    {
        Debug.Log("<color=cyan>[Query Test 2] Same Mask Different Order Reuses Cache</color>");

        World world = new World();
        EntityInfo e1 = CreatePV(world);

        Expect(world.QueryCacheCount == 0, $"Initial QueryCacheCount should be 0. Actual = {world.QueryCacheCount}");

        List<EntityInfo> first = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Execute();
        int cacheCountAfterFirst = world.QueryCacheCount;
        List<EntityInfo> second = world.Query().With<QueryVelocityComponent>().With<QueryPositionComponent>().Execute();

        Expect(first.Count == 1 && ContainsEntity(first, e1), "First query should return e1.");
        Expect(second.Count == 1 && ContainsEntity(second, e1), "Second query should return e1.");
        Expect(cacheCountAfterFirst == 1, $"QueryCacheCount should be 1 after first query. Actual = {cacheCountAfterFirst}");
        Expect(world.QueryCacheCount == 1, $"Same mask in different order should reuse cache. Actual = {world.QueryCacheCount}");
    }

    private void TestDifferentQueryCreatesNewCacheItem()
    {
        Debug.Log("<color=cyan>[Query Test 3] Different Query Creates New Cache Item</color>");

        World world = new World();
        CreatePV(world);

        world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Execute();
        int firstCacheCount = world.QueryCacheCount;

        world.Query().With<QueryPositionComponent>().Without<QueryDeadTagComponent>().Execute();
        int secondCacheCount = world.QueryCacheCount;

        world.Query().With<QueryPositionComponent>().Without<QueryDeadTagComponent>().Execute();
        int thirdCacheCount = world.QueryCacheCount;

        Expect(firstCacheCount == 1, $"First unique query should create one cache item. Actual = {firstCacheCount}");
        Expect(secondCacheCount == 2, $"Second unique query should create second cache item. Actual = {secondCacheCount}");
        Expect(thirdCacheCount == 2, $"Repeating second query should reuse cache. Actual = {thirdCacheCount}");
    }

    private void TestCacheInvalidatesWhenNewMatchingArcheTypeAppears()
    {
        Debug.Log("<color=cyan>[Query Test 4] Cache Invalidates When New Matching ArcheType Appears</color>");

        World world = new World();
        EntityInfo e1 = CreatePV(world);

        List<EntityInfo> first = world.Query().With<QueryPositionComponent>().Execute();
        int versionAfterFirstQuery = world.ArcheTypeVersion;
        int cacheCountAfterFirstQuery = world.QueryCacheCount;

        EntityInfo e2 = world.CreateEntity();
        world.SetComponent(e2, new QueryPositionComponent { x = 2f, y = 0f, z = 0f });
        world.SetComponent(e2, new QueryColliderComponent { radius = 1f });

        List<EntityInfo> second = world.Query().With<QueryPositionComponent>().Execute();

        Expect(first.Count == 1 && ContainsEntity(first, e1), "Initial Position query should return e1 only.");
        Expect(world.ArcheTypeVersion > versionAfterFirstQuery, $"ArcheTypeVersion should increase after new matching archetype. Before = {versionAfterFirstQuery}, After = {world.ArcheTypeVersion}");
        Expect(world.QueryCacheCount == cacheCountAfterFirstQuery, $"Same query should reuse existing cache item, not create a new one. Actual = {world.QueryCacheCount}");
        Expect(second.Count == 2 && ContainsEntity(second, e1) && ContainsEntity(second, e2), $"After cache invalidation, Position query should return e1 and e2. Actual = {second.Count}");
    }

    private void TestCacheInvalidatesAfterRemoveExcludeComponent()
    {
        Debug.Log("<color=cyan>[Query Test 5] Cache Invalidates After Remove Exclude Component</color>");

        World world = new World();
        EntityInfo entity = CreatePV(world);
        world.SetComponent(entity, new QueryDeadTagComponent());

        List<EntityInfo> beforeRemove = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Without<QueryDeadTagComponent>().Execute();
        int versionBeforeRemove = world.ArcheTypeVersion;

        bool removed = world.RemoveComponent<QueryDeadTagComponent>(entity);
        List<EntityInfo> afterRemove = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Without<QueryDeadTagComponent>().Execute();

        Expect(beforeRemove.Count == 0, $"Before removing DeadTag, query should return 0. Actual = {beforeRemove.Count}");
        Expect(removed, "RemoveComponent<DeadTag> should return true.");
        Expect(world.ArcheTypeVersion > versionBeforeRemove, $"ArcheTypeVersion should increase after removing exclude component. Before = {versionBeforeRemove}, After = {world.ArcheTypeVersion}");
        Expect(afterRemove.Count == 1 && ContainsEntity(afterRemove, entity), $"After removing DeadTag, query should return entity. Actual = {afterRemove.Count}");
    }

    private void TestQueryExecuteSnapshotBeforeImmediateStructuralChanges()
    {
        Debug.Log("<color=cyan>[Query Test 6] Execute Snapshot Before Immediate Structural Changes</color>");

        World world = new World();
        EntityInfo e1 = CreatePV(world);
        EntityInfo e2 = CreatePV(world);

        List<EntityInfo> snapshot = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Execute();

        for (int i = 0; i < snapshot.Count; i++)
        {
            world.RemoveComponent<QueryVelocityComponent>(snapshot[i]);
        }

        List<EntityInfo> after = world.Query().With<QueryPositionComponent>().With<QueryVelocityComponent>().Execute();

        Expect(snapshot.Count == 2 && ContainsEntity(snapshot, e1) && ContainsEntity(snapshot, e2), "Snapshot should contain both entities before removal.");
        Expect(after.Count == 0, $"After removing Velocity from snapshot entities, Position + Velocity query should return 0. Actual = {after.Count}");
    }

    private EntityInfo CreatePV(World world)
    {
        EntityInfo entity = world.CreateEntity();
        world.SetComponent(entity, new QueryPositionComponent { x = 1f, y = 2f, z = 3f });
        world.SetComponent(entity, new QueryVelocityComponent { x = 4f, y = 5f, z = 6f });
        return entity;
    }

    private bool ContainsEntity(List<EntityInfo> entities, EntityInfo entity)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] == entity)
                return true;
        }

        return false;
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

public struct QueryPositionComponent : IComponentData
{
    public float x;
    public float y;
    public float z;
}

public struct QueryVelocityComponent : IComponentData
{
    public float x;
    public float y;
    public float z;
}

public struct QueryColliderComponent : IComponentData
{
    public float radius;
}

public struct QueryDeadTagComponent : IComponentData
{
}

public struct QueryFrozenTagComponent : IComponentData
{
}
