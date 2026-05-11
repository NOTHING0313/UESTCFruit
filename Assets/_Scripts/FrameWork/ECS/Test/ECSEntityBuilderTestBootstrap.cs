using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 验证 EntityBuilder 的链式创建、重复 Build、委托配置和 Build 后继续追加组件规则。
/// </summary>
public sealed class ECSEntityBuilderTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS EntityBuilder Test] Start</color>");

        TestBuilderCreatesEntityWithComponents();
        TestBuildIsIdempotent();
        TestBuildEntityCallback();
        TestWithAfterBuildStillAppliesToSameEntity();
        TestBuilderReturnsInvalidWhenWorldDisposed();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS EntityBuilder Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS EntityBuilder Test] Failed count = {_failedCount}");
    }

    /// <summary>验证 Builder 可以创建实体并设置多个初始组件。</summary>
    private void TestBuilderCreatesEntityWithComponents()
    {
        Debug.Log("<color=cyan>[EntityBuilder Test 1] Create Entity With Components</color>");

        World world = new World();
        Entity entity = world.CreateEntityBuilder()
            .With(new PositionComponent(1, 2, 3))
            .With(new VelocityComponent(4, 5, 6))
            .Build();

        bool alive = world.IsAlive(entity);
        bool hasPosition = world.HasComponent<PositionComponent>(entity);
        bool hasVelocity = world.HasComponent<VelocityComponent>(entity);
        bool positionValue = world.TryGetComponent(entity, out PositionComponent position) && NearlyEqual(position.x, 1) && NearlyEqual(position.y, 2) && NearlyEqual(position.z, 3);
        bool velocityValue = world.TryGetComponent(entity, out VelocityComponent velocity) && NearlyEqual(velocity.x, 4) && NearlyEqual(velocity.y, 5) && NearlyEqual(velocity.z, 6);

        Expect(alive, "Builder should create an alive entity.");
        Expect(hasPosition, "Builder should set PositionComponent.");
        Expect(hasVelocity, "Builder should set VelocityComponent.");
        Expect(positionValue, "PositionComponent value should match builder input.");
        Expect(velocityValue, "VelocityComponent value should match builder input.");
    }

    /// <summary>验证 Build 多次调用只返回同一个 Entity，不会重复创建。</summary>
    private void TestBuildIsIdempotent()
    {
        Debug.Log("<color=cyan>[EntityBuilder Test 2] Build Is Idempotent</color>");

        World world = new World();
        EntityBuilder builder = world.CreateEntityBuilder()
            .With(new PositionComponent(1, 0, 0));

        Entity first = builder.Build();
        Entity second = builder.Build();

        bool sameEntity = first == second;
        bool aliveCountSuccess = world.AliveEntityCount == 1;
        bool isBuilt = builder.IsBuilt;

        Expect(sameEntity, "Build should return the same entity when called multiple times.");
        Expect(aliveCountSuccess, $"Build should not create extra entities. Alive = {world.AliveEntityCount}");
        Expect(isBuilt, "Builder.IsBuilt should be true after Build.");
    }

    /// <summary>验证 World.BuildEntity 可以通过委托集中配置组件。</summary>
    private void TestBuildEntityCallback()
    {
        Debug.Log("<color=cyan>[EntityBuilder Test 3] BuildEntity Callback</color>");

        World world = new World();
        Entity entity = world.BuildEntity(builder =>
        {
            builder.With(new PositionComponent(10, 0, 0));
            builder.With(new HealthComponent(80, 100));
        });

        bool alive = world.IsAlive(entity);
        bool hasPosition = world.HasComponent<PositionComponent>(entity);
        bool hasHealth = world.HasComponent<HealthComponent>(entity);
        bool healthValue = world.TryGetComponent(entity, out HealthComponent health) && health.current == 80 && health.max == 100;

        Expect(alive, "BuildEntity should create an alive entity.");
        Expect(hasPosition, "BuildEntity should apply PositionComponent from callback.");
        Expect(hasHealth, "BuildEntity should apply HealthComponent from callback.");
        Expect(healthValue, "HealthComponent value should match callback input.");
    }

    /// <summary>验证 Build 后继续调用 With 会继续作用于同一个 Entity。</summary>
    private void TestWithAfterBuildStillAppliesToSameEntity()
    {
        Debug.Log("<color=cyan>[EntityBuilder Test 4] With After Build</color>");

        World world = new World();
        EntityBuilder builder = world.CreateEntityBuilder();
        Entity entity = builder.Build();

        builder.With(new PositionComponent(7, 8, 9));

        bool aliveCountSuccess = world.AliveEntityCount == 1;
        bool hasPosition = world.HasComponent<PositionComponent>(entity);
        bool positionValue = world.TryGetComponent(entity, out PositionComponent position) && NearlyEqual(position.x, 7) && NearlyEqual(position.y, 8) && NearlyEqual(position.z, 9);

        Expect(aliveCountSuccess, $"With after Build should not create another entity. Alive = {world.AliveEntityCount}");
        Expect(hasPosition, "With after Build should still apply component to the same entity.");
        Expect(positionValue, "Component added after Build should have correct value.");
    }

    /// <summary>验证 World 释放后创建 Builder 会得到无效 Entity，With 调用不会抛异常。</summary>
    private void TestBuilderReturnsInvalidWhenWorldDisposed()
    {
        Debug.Log("<color=cyan>[EntityBuilder Test 5] Invalid When World Disposed</color>");

        World world = new World();
        world.Dispose();

        EntityBuilder builder = world.CreateEntityBuilder();
        Entity entity = builder
            .With(new PositionComponent(1, 1, 1))
            .Build();

        bool invalid = entity == Entity.Invalid;
        bool notAlive = !world.IsAlive(entity);

        Expect(invalid, "Builder should return Entity.Invalid when World is disposed.");
        Expect(notAlive, "Invalid entity from disposed World should not be alive.");
    }

    /// <summary>输出测试结果。</summary>
    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
            return;
        }

        _failedCount++;
        Debug.LogError($"<color=red>[FAIL]</color> {message}");
    }

    /// <summary>比较浮点数是否近似相等。</summary>
    private bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }
}

}
