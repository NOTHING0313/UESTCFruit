using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 验证 Query 条件缓存、NoAlloc Fill、缓存 Query 的 System 以及 ViewManager Provider 接入。
/// </summary>
public sealed class ECSQuerySystemViewProviderTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Query / System / ViewProvider Test] Start</color>");

        TestQueryExecuteAndFillReuseCache();
        TestExecuteSortedProvidesStableOrder();
        TestMovementSystemUsesCachedQuery();
        TestInputMoveSystemUsesCachedQuery();
        TestDamageAndDeadCleanupSystems();
        TestViewManagerUsesInstanceProvider();
        TestViewManagerRejectsProviderSwitchWithActiveViews();
        TestViewSpawnSyncDestroySystems();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Query / System / ViewProvider Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Query / System / ViewProvider Test] Failed count = {_failedCount}");
    }

    /// <summary>验证 Execute 和 Fill 都会复用同一个 QueryCache 条件项。</summary>
    private void TestQueryExecuteAndFillReuseCache()
    {
        Debug.Log("<color=cyan>[Query Provider Test 1] Execute / Fill Reuse Cache</color>");

        World world = new World();
        Entity e1 = CreateMoveEntity(world, 1f, 0f, 0f, 1f, 0f, 0f);
        Entity e2 = CreateMoveEntity(world, 2f, 0f, 0f, 1f, 0f, 0f);
        List<Entity> results = new List<Entity>(8);

        EntityQueryDescription query = world.Query().With<PositionComponent>().With<VelocityComponent>().BuildDescription();

        int firstCount = world.FillQuery(query, results, false);
        int cacheAfterFirst = world.QueryCacheCount;
        int secondCount = world.FillQuery(query, results, false);
        int cacheAfterSecond = world.QueryCacheCount;
        List<Entity> executeResults = world.Query().With<VelocityComponent>().With<PositionComponent>().Execute();

        Expect(firstCount == 2 && ContainsEntity(results, e1) && ContainsEntity(results, e2), $"First Fill should return two entities. Actual = {firstCount}");
        Expect(secondCount == 2, $"Second Fill should still return two entities. Actual = {secondCount}");
        Expect(cacheAfterFirst == 1, $"First Fill should create one QueryCache item. Actual = {cacheAfterFirst}");
        Expect(cacheAfterSecond == 1, $"Second Fill should reuse QueryCache item. Actual = {cacheAfterSecond}");
        Expect(world.QueryCacheCount == 1 && executeResults.Count == 2, $"Execute with same mask order changed should reuse cache. Cache = {world.QueryCacheCount}, Count = {executeResults.Count}");

        Entity e3 = world.CreateEntity();
        world.SetComponent(e3, new PositionComponent(3f, 0f, 0f));
        world.SetComponent(e3, new VelocityComponent(1f, 0f, 0f));
        world.SetComponent(e3, new MoveSpeedComponent(1f));

        int afterNewArchetype = world.FillQuery(query, results, false);
        Expect(afterNewArchetype == 3 && ContainsEntity(results, e3), $"Cache should refresh after new matching archetype. Actual = {afterNewArchetype}");
        Expect(world.QueryCacheCount == 1, $"Refreshing same query should not add cache item. Actual = {world.QueryCacheCount}");
    }

    /// <summary>验证 ExecuteSorted 和 Fill(sorted:true) 的结果顺序稳定。</summary>
    private void TestExecuteSortedProvidesStableOrder()
    {
        Debug.Log("<color=cyan>[Query Provider Test 2] ExecuteSorted Stable Order</color>");

        World world = new World();
        Entity e1 = CreateMoveEntity(world, 1f, 0f, 0f, 0f, 0f, 0f);
        Entity e2 = CreateMoveEntity(world, 2f, 0f, 0f, 0f, 0f, 0f);
        Entity e3 = CreateMoveEntity(world, 3f, 0f, 0f, 0f, 0f, 0f);

        world.RemoveComponent<VelocityComponent>(e2);
        world.SetComponent(e2, new VelocityComponent(0f, 0f, 0f));

        List<Entity> sorted = world.Query().With<PositionComponent>().With<VelocityComponent>().ExecuteSorted();
        bool ordered = sorted.Count == 3 && sorted[0] == e1 && sorted[1] == e2 && sorted[2] == e3;

        Expect(ordered, "ExecuteSorted should sort entities by ID / Version even when archetype internal order changes.");
    }

    /// <summary>验证 MovementSystem 使用缓存 Query 后仍然正确推进位置。</summary>
    private void TestMovementSystemUsesCachedQuery()
    {
        Debug.Log("<color=cyan>[Query Provider Test 3] MovementSystem Cached Query</color>");

        World world = new World();
        Entity entity = CreateMoveEntity(world, 0f, 0f, 0f, 2f, 0f, -1f);
        world.AddSystem(new MovementSystem());

        TickWorld(world, 1, 0.5f);
        PositionComponent position = world.GetComponent<PositionComponent>(entity);

        Expect(NearlyEqual(position.x, 1f) && NearlyEqual(position.z, -0.5f), $"MovementSystem should update position. Actual = ({position.x}, {position.z})");
        Expect(world.QueryCacheCount == 1, $"MovementSystem should create one cached query. Actual = {world.QueryCacheCount}");
    }

    /// <summary>验证 InputMoveSystem 使用缓存 Query 后仍然正确写入速度。</summary>
    private void TestInputMoveSystemUsesCachedQuery()
    {
        Debug.Log("<color=cyan>[Query Provider Test 4] InputMoveSystem Cached Query</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PlayerInputSnapshotComponent(1, 0, 1f, -1f));
        world.SetComponent(entity, new MoveSpeedComponent(3f));
        world.SetComponent(entity, new VelocityComponent(0f, 0f, 0f));
        world.AddSystem(new InputMoveSystem());

        TickWorld(world, 1, 1f);
        VelocityComponent velocity = world.GetComponent<VelocityComponent>(entity);

        Expect(NearlyEqual(velocity.x, 3f) && NearlyEqual(velocity.z, -3f), $"InputMoveSystem should write velocity from input. Actual = ({velocity.x}, {velocity.z})");
        Expect(world.QueryCacheCount == 1, $"InputMoveSystem should create one cached query. Actual = {world.QueryCacheCount}");

        world.SetComponent(entity, new PlayerInputSnapshotComponent(2, 0, 1f, 1f));
        TickWorld(world, 3, 1f);
        velocity = world.GetComponent<VelocityComponent>(entity);

        Expect(NearlyEqual(velocity.x, 0f) && NearlyEqual(velocity.z, 0f), "InputMoveSystem should clear velocity when input frame is invalid.");
    }

    /// <summary>验证伤害结算和死亡清理系统在缓存 Query 后仍然正确工作。</summary>
    private void TestDamageAndDeadCleanupSystems()
    {
        Debug.Log("<color=cyan>[Query Provider Test 5] Damage / DeadCleanup Cached Query</color>");

        World world = new World();
        Entity target = world.CreateEntity();
        world.SetComponent(target, new HealthComponent(10, 10));

        Entity request = world.CreateEntity();
        world.SetComponent(request, new DamageRequestComponent(Entity.Invalid, target, 12));

        world.AddSystem(new DamageResolveSystem());
        world.AddSystem(new DeadCleanupSystem());
        TickWorld(world, 1, 1f);

        Expect(!world.IsAlive(request), "Damage request entity should be destroyed after first Tick playback.");
        Expect(world.IsAlive(target) && world.HasComponent<DeadTagComponent>(target), "Target should receive DeadTag after damage playback and remain alive until cleanup Tick.");

        TickWorld(world, 2, 1f);

        Expect(!world.IsAlive(target), "Target should be destroyed by DeadCleanupSystem on the next Tick.");
        Expect(world.QueryCacheCount == 2, $"Damage and DeadCleanup should create two cached queries. Actual = {world.QueryCacheCount}");
    }

    /// <summary>验证 ViewManager 通过 IViewInstanceProvider 创建和释放 View。</summary>
    private void TestViewManagerUsesInstanceProvider()
    {
        Debug.Log("<color=cyan>[Query Provider Test 6] ViewManager Uses InstanceProvider</color>");

        TrackingViewInstanceProvider provider = new TrackingViewInstanceProvider();
        ViewManager viewManager = new ViewManager(provider);
        GameObject prefab = new GameObject("ViewProviderTestPrefab");
        viewManager.RegisterPrefab(1001, prefab);

        int viewID = viewManager.SpawnView(1001, new Vector3(1f, 2f, 3f), Quaternion.identity);
        bool hasTransform = viewManager.TryGetTransform(viewID, out Transform transform);
        bool destroyed = viewManager.DestroyView(viewID);

        Expect(viewID > 0, "SpawnView should return a valid viewID.");
        Expect(hasTransform && transform != null && NearlyEqual(transform.position.x, 1f), "TryGetTransform should return spawned transform.");
        Expect(destroyed, "DestroyView should return true for existing view.");
        Expect(provider.SpawnCount == 1 && provider.ReleaseCount == 1, $"Provider should receive one Spawn and one Release. Spawn = {provider.SpawnCount}, Release = {provider.ReleaseCount}");
        Expect(viewManager.ViewCount == 0, $"ViewManager should remove mapping after DestroyView. Actual = {viewManager.ViewCount}");

        Object.Destroy(prefab);
    }

    /// <summary>验证已有 View 时不能切换 Provider，避免旧 Provider 创建的对象被新 Provider 误释放。</summary>
    private void TestViewManagerRejectsProviderSwitchWithActiveViews()
    {
        TrackingViewInstanceProvider firstProvider = new TrackingViewInstanceProvider();
        TrackingViewInstanceProvider secondProvider = new TrackingViewInstanceProvider();
        ViewManager viewManager = new ViewManager(firstProvider);
        GameObject prefab = new GameObject("ViewProviderSwitchTestPrefab");
        viewManager.RegisterPrefab(1101, prefab);

        int viewID = viewManager.SpawnView(1101, Vector3.zero, Quaternion.identity);
        viewManager.SetInstanceProvider(secondProvider);
        viewManager.DestroyView(viewID);

        Expect(firstProvider.ReleaseCount == 1, $"Active view should still be released by the original provider. Actual = {firstProvider.ReleaseCount}");
        Expect(secondProvider.ReleaseCount == 0, $"Provider switch should be ignored while views exist. Actual = {secondProvider.ReleaseCount}");

        Object.Destroy(prefab);
    }

    /// <summary>验证 ViewSpawnSystem、ViewSyncSystem、ViewDestroySystem 的完整表现对象生命周期。</summary>
    private void TestViewSpawnSyncDestroySystems()
    {
        Debug.Log("<color=cyan>[Query Provider Test 7] View Spawn / Sync / Destroy Systems</color>");

        World world = new World();
        TrackingViewInstanceProvider provider = new TrackingViewInstanceProvider();
        ViewManager viewManager = new ViewManager(provider);
        GameObject prefab = new GameObject("ViewSystemTestPrefab");

        viewManager.RegisterPrefab(2001, prefab);
        world.AddSystem(new ViewSpawnSystem(viewManager));
        world.AddSystem(new ViewSyncSystem(viewManager));
        world.AddSystem(new ViewDestroySystem(viewManager));

        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PositionComponent(1f, 2f, 3f));
        world.SetComponent(entity, new PrefabViewRequestComponent(2001));

        TickWorld(world, 1, 1f);

        Expect(world.HasComponent<ViewComponent>(entity), "ViewSpawnSystem should add ViewComponent after playback.");
        Expect(!world.HasComponent<PrefabViewRequestComponent>(entity), "ViewSpawnSystem should remove PrefabViewRequestComponent after playback.");
        Expect(provider.SpawnCount == 1 && viewManager.ViewCount == 1, $"View should be spawned once. Spawn = {provider.SpawnCount}, ViewCount = {viewManager.ViewCount}");

        ViewComponent view = world.GetComponent<ViewComponent>(entity);
        bool hasTransform = viewManager.TryGetTransform(view.viewID, out Transform transform);
        Expect(hasTransform && transform != null, "Spawned view transform should be registered.");

        world.SetComponent(entity, new PositionComponent(5f, 6f, 7f));
        TickWorld(world, 2, 1f);
        Expect(NearlyEqual(transform.position.x, 5f) && NearlyEqual(transform.position.y, 6f) && NearlyEqual(transform.position.z, 7f), "ViewSyncSystem should sync transform position on next Tick.");

        world.SetComponent(entity, new ViewDestroyRequestComponent());
        TickWorld(world, 3, 1f);

        Expect(!world.HasComponent<ViewComponent>(entity), "ViewDestroySystem should remove ViewComponent after playback.");
        Expect(!world.HasComponent<ViewDestroyRequestComponent>(entity), "ViewDestroySystem should remove ViewDestroyRequestComponent after playback.");
        Expect(provider.ReleaseCount == 1 && viewManager.ViewCount == 0, $"View should be released once. Release = {provider.ReleaseCount}, ViewCount = {viewManager.ViewCount}");

        Object.Destroy(prefab);
    }

    /// <summary>创建拥有 Position 和 Velocity 的移动测试实体。</summary>
    private Entity CreateMoveEntity(World world, float px, float py, float pz, float vx, float vy, float vz)
    {
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PositionComponent(px, py, pz));
        world.SetComponent(entity, new VelocityComponent(vx, vy, vz));
        return entity;
    }

    /// <summary>推进 World 一个逻辑帧。</summary>
    private void TickWorld(World world, int frameNumber, float tickLength)
    {
        SimulationContext context = new SimulationContext(frameNumber, tickLength, false);
        world.Tick(in context);
    }

    /// <summary>判断列表中是否存在指定 Entity。</summary>
    private bool ContainsEntity(List<Entity> entities, Entity entity)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] == entity)
                return true;
        }

        return false;
    }

    /// <summary>输出测试断言结果。</summary>
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

    /// <summary>比较两个 float 是否近似相等。</summary>
    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }

    /// <summary>测试用 View 实例提供器，用于验证 ViewManager 是否调用 Provider。</summary>
    private sealed class TrackingViewInstanceProvider : IViewInstanceProvider
    {
        private readonly List<GameObject> _instances = new List<GameObject>();

        public int SpawnCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            SpawnCount++;
            GameObject instance = new GameObject(prefab != null ? prefab.name + "_Instance" : "TrackingViewInstance");
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);
            _instances.Add(instance);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            ReleaseCount++;
            _instances.Remove(instance);
            Object.Destroy(instance);
        }

        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                    Object.Destroy(_instances[i]);
            }

            _instances.Clear();
        }
    }
}

}
