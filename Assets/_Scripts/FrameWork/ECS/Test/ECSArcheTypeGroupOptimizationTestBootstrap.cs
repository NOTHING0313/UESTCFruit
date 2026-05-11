using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// ArcheTypeGroup 优化回归测试，验证 List + Dictionary 的 Swap Remove 不会产生重复、遗漏或 Query 缓存失效问题。
/// </summary>
public class ECSArcheTypeGroupOptimizationTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS ArcheTypeGroup Optimization Test] Start</color>");

        TestRemoveComponentSwapBackKeepsQueryCorrect();
        TestAddRemoveBackDoesNotDuplicateEntity();
        TestDestroyEntitiesKeepsArcheTypeGroupCorrect();
        TestQueryCacheRefreshesAfterArcheTypeGroupChange();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS ArcheTypeGroup Optimization Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS ArcheTypeGroup Optimization Test] Failed count = {_failedCount}");
    }

    private void TestRemoveComponentSwapBackKeepsQueryCorrect()
    {
        Debug.Log("<color=cyan>[ArcheTypeGroup Test 1] Remove Component SwapBack Keeps Query Correct</color>");

        World world = new World();
        List<Entity> entities = CreatePositionVelocityEntities(world, 16);

        world.RemoveComponent<VelocityComponent>(entities[0]);
        world.RemoveComponent<VelocityComponent>(entities[7]);
        world.RemoveComponent<VelocityComponent>(entities[15]);

        List<Entity> pv = world.Query().With<PositionComponent>().With<VelocityComponent>().Execute();
        List<Entity> positionOnly = world.Query().With<PositionComponent>().Without<VelocityComponent>().Execute();

        Expect(pv.Count == 13, $"PV query should contain 13 entities after removing 3 velocities. Actual = {pv.Count}");
        Expect(positionOnly.Count == 3, $"Position without Velocity query should contain 3 entities. Actual = {positionOnly.Count}");
        Expect(!ContainsEntity(pv, entities[0]) && !ContainsEntity(pv, entities[7]) && !ContainsEntity(pv, entities[15]), "PV query should not contain entities whose Velocity was removed.");
        Expect(HasNoDuplicate(pv), "PV query should not contain duplicated entities after swap remove.");
        Expect(HasNoDuplicate(positionOnly), "Position-only query should not contain duplicated entities after swap remove.");
    }

    private void TestAddRemoveBackDoesNotDuplicateEntity()
    {
        Debug.Log("<color=cyan>[ArcheTypeGroup Test 2] Add Remove Back Does Not Duplicate Entity</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PositionComponent(1f, 0f, 0f));
        world.SetComponent(entity, new VelocityComponent(1f, 0f, 0f));

        world.RemoveComponent<VelocityComponent>(entity);
        world.SetComponent(entity, new VelocityComponent(2f, 0f, 0f));
        world.SetComponent(entity, new VelocityComponent(3f, 0f, 0f));

        List<Entity> pv = world.Query().With<PositionComponent>().With<VelocityComponent>().Execute();
        int occurrence = CountOccurrence(pv, entity);

        bool hasVelocity = world.TryGetComponent(entity, out VelocityComponent velocity);
        float velocityX = hasVelocity ? velocity.x : -1f;

        Expect(pv.Count == 1, $"PV query should contain exactly one entity. Actual = {pv.Count}");
        Expect(occurrence == 1, $"Entity should appear once after remove/add/set. Actual occurrence = {occurrence}");
        Expect(hasVelocity && velocity.x == 3f, $"Velocity should keep last set value 3. Actual = {velocityX}");
    }

    private void TestDestroyEntitiesKeepsArcheTypeGroupCorrect()
    {
        Debug.Log("<color=cyan>[ArcheTypeGroup Test 3] Destroy Entities Keeps ArcheTypeGroup Correct</color>");

        World world = new World();
        List<Entity> entities = CreatePositionVelocityEntities(world, 32);

        for (int i = 0; i < entities.Count; i += 2)
            world.DestroyEntity(entities[i]);

        List<Entity> pv = world.Query().With<PositionComponent>().With<VelocityComponent>().Execute();

        Expect(pv.Count == 16, $"PV query should contain 16 alive entities after destroying half. Actual = {pv.Count}");
        Expect(HasNoDuplicate(pv), "PV query should not contain duplicated entities after DestroyEntity.");

        for (int i = 0; i < entities.Count; i++)
        {
            bool shouldBeAlive = (i % 2) != 0;
            bool contained = ContainsEntity(pv, entities[i]);

            if (shouldBeAlive)
                Expect(contained, $"Alive entity at index {i} should still be returned by PV query.");
            else
                Expect(!contained, $"Destroyed entity at index {i} should not be returned by PV query.");
        }
    }

    private void TestQueryCacheRefreshesAfterArcheTypeGroupChange()
    {
        Debug.Log("<color=cyan>[ArcheTypeGroup Test 4] Query Cache Refreshes After ArcheTypeGroup Change</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PositionComponent(1f, 0f, 0f));

        List<Entity> before = world.Query().With<PositionComponent>().With<VelocityComponent>().Execute();
        int cacheCountBefore = world.QueryCacheCount;
        int versionBefore = world.ArcheTypeVersion;

        world.SetComponent(entity, new VelocityComponent(1f, 0f, 0f));
        List<Entity> after = world.Query().With<PositionComponent>().With<VelocityComponent>().Execute();

        Expect(before.Count == 0, $"Before adding Velocity, PV query should return 0. Actual = {before.Count}");
        Expect(cacheCountBefore == 1, $"First PV query should create one query cache item. Actual = {cacheCountBefore}");
        Expect(world.QueryCacheCount == 1, $"Same PV query should reuse cache item after version refresh. Actual = {world.QueryCacheCount}");
        Expect(world.ArcheTypeVersion > versionBefore, $"ArcheTypeVersion should increase after adding Velocity. Before = {versionBefore}, After = {world.ArcheTypeVersion}");
        Expect(after.Count == 1 && ContainsEntity(after, entity), $"After adding Velocity, PV query should return the entity. Actual = {after.Count}");
    }

    private List<Entity> CreatePositionVelocityEntities(World world, int count)
    {
        List<Entity> entities = new List<Entity>(count);

        for (int i = 0; i < count; i++)
        {
            Entity entity = world.CreateEntity();
            world.SetComponent(entity, new PositionComponent(i, 0f, 0f));
            world.SetComponent(entity, new VelocityComponent(1f, 0f, 0f));
            entities.Add(entity);
        }

        return entities;
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

    private int CountOccurrence(List<Entity> entities, Entity entity)
    {
        int count = 0;

        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] == entity)
                count++;
        }

        return count;
    }

    private bool HasNoDuplicate(List<Entity> entities)
    {
        HashSet<Entity> set = new HashSet<Entity>();

        for (int i = 0; i < entities.Count; i++)
        {
            if (!set.Add(entities[i]))
                return false;
        }

        return true;
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

}
