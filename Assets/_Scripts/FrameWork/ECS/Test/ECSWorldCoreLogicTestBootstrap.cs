using UnityEngine;

/// <summary>
/// 验证 World / Entity / Component / System / Resolver / ViewReader 的纯逻辑闭环。
/// </summary>
public sealed class ECSWorldCoreLogicTestBootstrap : MonoBehaviour
{
    private World _world;
    private int _passedCount;
    private int _failedCount;

    private void Start()
    {
        RunAllTests();
    }

    /// <summary>执行全部核心逻辑测试。</summary>
    private void RunAllTests()
    {
        _world = new World();
        _world.AddSystem(new MovementSystem());
        _world.AddSystem(new DamageResolveSystem());
        _world.AddSystem(new DeadCleanupSystem());

        TestMovementAndComponentWrite();
        TestBuffTargetResolver();
        TestWorldViewReader();
        TestDamageAndDeadCleanup();

        Debug.Log($"[ECSWorldCoreLogicTestBootstrap] Passed: {_passedCount}, Failed: {_failedCount}");

        _world.Dispose();
        _world = null;
    }

    /// <summary>测试 MovementSystem 是否能推进 PositionComponent。</summary>
    private void TestMovementAndComponentWrite()
    {
        EntityInfo entity = _world.CreateEntity();

        _world.SetComponent(entity, new PositionComponent(0f, 0f, 0f));
        _world.SetComponent(entity, new VelocityComponent(2f, 0f, 0f));
        _world.Tick(new SimulationContext(1, 0.5f, false));

        PositionComponent position;
        bool success = _world.TryGetComponent(entity, out position) && NearlyEqual(position.x, 1f);

        Assert(success, "MovementSystem should move entity by velocity * tickLength.");
    }

    /// <summary>测试 Buff 受限访问器是否能读取和修改目标组件。</summary>
    private void TestBuffTargetResolver()
    {
        EntityInfo entity = _world.CreateEntity();
        _world.SetComponent(entity, new HealthComponent(10, 10));
        _world.SetComponent(entity, new StatComponent(3, 1, 5));

        IBuffTargetResolver resolver = new WorldBuffTargetResolver(_world);

        if (resolver.HasHealth(entity))
        {
            ref HealthComponent health = ref resolver.GetHealth(entity);
            health.current = 7;
        }

        HealthComponent result;
        bool success = _world.TryGetComponent(entity, out result) && result.current == 7;

        Assert(success, "WorldBuffTargetResolver should expose writable component ref.");
    }

    /// <summary>测试表现层只读访问器是否能读取 ViewID / Position / Health。</summary>
    private void TestWorldViewReader()
    {
        EntityInfo entity = _world.CreateEntity();

        _world.SetComponent(entity, new ViewComponent(1001));
        _world.SetComponent(entity, new PositionComponent(2f, 3f, 4f));
        _world.SetComponent(entity, new HealthComponent(5, 10));

        IWorldViewReader reader = new WorldViewReader(_world);

        bool success = reader.TryGetViewId(entity, out int viewId)
            && viewId == 1001
            && reader.TryGetPosition(entity, out PositionComponent position)
            && NearlyEqual(position.y, 3f)
            && reader.TryGetHealth(entity, out HealthComponent health)
            && health.current == 5;

        Assert(success, "WorldViewReader should read view, position and health data.");
    }

    /// <summary>测试伤害请求、死亡标记和死亡清理流程。</summary>
    private void TestDamageAndDeadCleanup()
    {
        EntityInfo target = _world.CreateEntity();
        _world.SetComponent(target, new HealthComponent(10, 10));

        EntityInfo firstRequest = _world.CreateEntity();
        _world.SetComponent(firstRequest, new DamageRequestComponent(EntityInfo.Invalid, target, 4));

        _world.Tick(new SimulationContext(2, 1f, false));

        bool firstDamageSuccess = _world.TryGetComponent(target, out HealthComponent healthAfterFirstHit)
            && healthAfterFirstHit.current == 6
            && !_world.IsAlive(firstRequest);

        EntityInfo secondRequest = _world.CreateEntity();
        _world.SetComponent(secondRequest, new DamageRequestComponent(EntityInfo.Invalid, target, 10));

        _world.Tick(new SimulationContext(3, 1f, false));

        bool deadTagAdded = _world.IsAlive(target)
            && _world.HasComponent<DeadTagComponent>(target)
            && !_world.IsAlive(secondRequest);

        _world.Tick(new SimulationContext(4, 1f, false));

        bool cleanupSuccess = !_world.IsAlive(target);

        Assert(firstDamageSuccess && deadTagAdded && cleanupSuccess, "DamageResolveSystem and DeadCleanupSystem should damage, tag and destroy dead entity.");
    }

    /// <summary>输出测试结果。</summary>
    private void Assert(bool condition, string message)
    {
        if (condition)
        {
            _passedCount++;
            Debug.Log($"<color=green>[PASS]</color> {message}");
            return;
        }

        _failedCount++;
        Debug.LogError($"[FAIL] {message}");
    }

    /// <summary>比较两个 float 是否近似相等。</summary>
    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }
}
