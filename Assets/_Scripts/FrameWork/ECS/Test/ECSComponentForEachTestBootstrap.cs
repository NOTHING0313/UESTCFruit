using UnityEngine;

/// <summary>
/// 验证 World.ForEach<T> / ForEach<T1,T2> / ForEach<T1,T2,T3> 的高频遍历不会破坏组件对应关系和现有 Query 规则。
/// </summary>
public sealed class ECSComponentForEachTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Component ForEach Test] Start</color>");

        TestForEachSingleComponentWritesExpectedComponent();
        TestForEachWritesExpectedComponents();
        TestForEachSkipsMissingComponent();
        TestForEachKeepsParameterOrderWhenSecondStoreIsSmaller();
        TestForEachThreeComponentsWritesExpectedVelocity();
        TestMovementSystemUsesForEachCorrectly();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Component ForEach Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Component ForEach Test] Failed count = {_failedCount}");
    }


    /// <summary>验证单组件 ForEach 能以 ref 形式修改组件，并且只遍历拥有该组件的实体。</summary>
    private void TestForEachSingleComponentWritesExpectedComponent()
    {
        Debug.Log("<color=cyan>[ForEach Test 0] Single Component Writes Expected Component</color>");

        World world = new World();
        EntityInfo positionA = world.CreateEntity();
        EntityInfo positionB = world.CreateEntity();
        EntityInfo noPosition = world.CreateEntity();

        world.SetComponent(positionA, new PositionComponent(1f, 2f, 3f));
        world.SetComponent(positionB, new PositionComponent(10f, 20f, 30f));
        world.SetComponent(noPosition, new VelocityComponent(100f, 0f, 0f));

        int count = world.ForEach<PositionComponent>(RaisePositionXOnce);

        bool positionASuccess = world.TryGetComponent(positionA, out PositionComponent a) && NearlyEqual(a.x, 2f) && NearlyEqual(a.y, 2f) && NearlyEqual(a.z, 3f);
        bool positionBSuccess = world.TryGetComponent(positionB, out PositionComponent b) && NearlyEqual(b.x, 11f) && NearlyEqual(b.y, 20f) && NearlyEqual(b.z, 30f);
        bool noPositionSuccess = !world.HasComponent<PositionComponent>(noPosition);

        Expect(count == 2, $"Single-component ForEach should execute twice. Actual = {count}");
        Expect(positionASuccess, "Single-component ForEach should update first Position through ref parameter.");
        Expect(positionBSuccess, "Single-component ForEach should update second Position through ref parameter.");
        Expect(noPositionSuccess, "Single-component ForEach should not add or touch missing Position components.");
    }

    /// <summary>验证 ForEach 能以 ref 形式修改组件，并且只处理同时拥有两个组件的实体。</summary>
    private void TestForEachWritesExpectedComponents()
    {
        Debug.Log("<color=cyan>[ForEach Test 1] Writes Expected Components</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();

        world.SetComponent(entity, new PositionComponent(1f, 2f, 3f));
        world.SetComponent(entity, new VelocityComponent(4f, 5f, 6f));

        int count = world.ForEach<PositionComponent, VelocityComponent>(MoveByVelocityOnce);

        bool success = world.TryGetComponent(entity, out PositionComponent position)
            && NearlyEqual(position.x, 5f)
            && NearlyEqual(position.y, 7f)
            && NearlyEqual(position.z, 9f);

        Expect(count == 1, $"ForEach should execute once. Actual = {count}");
        Expect(success, "ForEach should expose writable component refs and update Position.");
    }

    /// <summary>验证缺少任意一个组件的实体不会被 ForEach 遍历。</summary>
    private void TestForEachSkipsMissingComponent()
    {
        Debug.Log("<color=cyan>[ForEach Test 2] Skips Missing Component</color>");

        World world = new World();
        EntityInfo onlyPosition = world.CreateEntity();
        EntityInfo onlyVelocity = world.CreateEntity();
        EntityInfo both = world.CreateEntity();

        world.SetComponent(onlyPosition, new PositionComponent(100f, 0f, 0f));
        world.SetComponent(onlyVelocity, new VelocityComponent(10f, 0f, 0f));
        world.SetComponent(both, new PositionComponent(0f, 0f, 0f));
        world.SetComponent(both, new VelocityComponent(2f, 0f, 0f));

        int count = world.ForEach<PositionComponent, VelocityComponent>(MoveByVelocityOnce);

        bool onlyPositionUnchanged = world.TryGetComponent(onlyPosition, out PositionComponent posOnly) && NearlyEqual(posOnly.x, 100f);
        bool bothMoved = world.TryGetComponent(both, out PositionComponent bothPosition) && NearlyEqual(bothPosition.x, 2f);

        Expect(count == 1, $"ForEach should only execute for entities with both components. Actual = {count}");
        Expect(onlyPositionUnchanged, "Entity without Velocity should not be modified.");
        Expect(bothMoved, "Entity with Position and Velocity should be modified.");
    }

    /// <summary>验证当第二个 Store 更小时，ForEach 仍然保持回调参数顺序为 T1、T2。</summary>
    private void TestForEachKeepsParameterOrderWhenSecondStoreIsSmaller()
    {
        Debug.Log("<color=cyan>[ForEach Test 3] Keeps Parameter Order When Second Store Is Smaller</color>");

        World world = new World();

        for (int i = 0; i < 10; i++)
        {
            EntityInfo entity = world.CreateEntity();
            world.SetComponent(entity, new PositionComponent(i, 0f, 0f));

            if (i < 3)
                world.SetComponent(entity, new VelocityComponent(10f, 0f, 0f));
        }

        int positionFirstCount = world.ForEach<PositionComponent, VelocityComponent>(MoveByVelocityOnce);
        int velocityFirstCount = world.ForEach<VelocityComponent, PositionComponent>(MovePositionByVelocityReversed);

        EntityQueryDescription query = world.Query().With<PositionComponent>().With<VelocityComponent>().BuildDescription();
        System.Collections.Generic.List<EntityInfo> results = new System.Collections.Generic.List<EntityInfo>();
        world.FillQuery(query, results, false);

        bool allMovedTwice = true;

        for (int i = 0; i < results.Count; i++)
        {
            if (!world.TryGetComponent(results[i], out PositionComponent position) || position.x < 20f)
                allMovedTwice = false;
        }

        Expect(positionFirstCount == 3, $"Position-first ForEach should execute 3 times. Actual = {positionFirstCount}");
        Expect(velocityFirstCount == 3, $"Velocity-first ForEach should execute 3 times. Actual = {velocityFirstCount}");
        Expect(results.Count == 3, $"Query result should still be compatible with ForEach result. Actual = {results.Count}");
        Expect(allMovedTwice, "ForEach should keep component parameter order even when the second store is smaller.");
    }

    /// <summary>验证三组件 ForEach 能保持参数顺序并正确写入 Velocity。</summary>
    private void TestForEachThreeComponentsWritesExpectedVelocity()
    {
        Debug.Log("<color=cyan>[ForEach Test 4] Three Components Writes Expected Velocity</color>");

        World world = new World();
        EntityInfo controlled = world.CreateEntity();
        EntityInfo missingSpeed = world.CreateEntity();

        world.SetComponent(controlled, new PlayerInputComponent(1, 0, 1f, -1f));
        world.SetComponent(controlled, new MoveSpeedComponent(3f));
        world.SetComponent(controlled, new VelocityComponent(0f, 0f, 0f));

        world.SetComponent(missingSpeed, new PlayerInputComponent(1, 0, 1f, 1f));
        world.SetComponent(missingSpeed, new VelocityComponent(9f, 0f, 9f));

        int count = world.ForEach<PlayerInputComponent, MoveSpeedComponent, VelocityComponent>(ApplyInputVelocityForTest);

        bool controlledSuccess = world.TryGetComponent(controlled, out VelocityComponent velocity)
            && NearlyEqual(velocity.x, 3f)
            && NearlyEqual(velocity.z, -3f);

        bool missingSpeedUnchanged = world.TryGetComponent(missingSpeed, out VelocityComponent missingSpeedVelocity)
            && NearlyEqual(missingSpeedVelocity.x, 9f)
            && NearlyEqual(missingSpeedVelocity.z, 9f);

        Expect(count == 1, $"Three-component ForEach should only execute for entities with all components. Actual = {count}");
        Expect(controlledSuccess, "Three-component ForEach should update Velocity through ref parameter.");
        Expect(missingSpeedUnchanged, "Entity missing MoveSpeed should not be modified.");
    }

    /// <summary>验证 MovementSystem 使用 ForEach 后仍能在 World.Tick 中正确推进位置。</summary>
    private void TestMovementSystemUsesForEachCorrectly()
    {
        Debug.Log("<color=cyan>[ForEach Test 5] MovementSystem Uses ForEach Correctly</color>");

        World world = new World();
        world.AddSystem(new MovementSystem());

        EntityInfo moving = world.CreateEntity();
        EntityInfo staticEntity = world.CreateEntity();

        world.SetComponent(moving, new PositionComponent(0f, 0f, 0f));
        world.SetComponent(moving, new VelocityComponent(3f, 0f, 0f));
        world.SetComponent(staticEntity, new PositionComponent(100f, 0f, 0f));

        world.Tick(new SimulationContext(1, 0.5f, false));

        bool movingSuccess = world.TryGetComponent(moving, out PositionComponent movingPosition) && NearlyEqual(movingPosition.x, 1.5f);
        bool staticSuccess = world.TryGetComponent(staticEntity, out PositionComponent staticPosition) && NearlyEqual(staticPosition.x, 100f);

        Expect(movingSuccess, "MovementSystem should move entity by Velocity * tickLength.");
        Expect(staticSuccess, "MovementSystem should not move entity without Velocity.");
    }


    /// <summary>单组件 Position 测试回调。</summary>
    private void RaisePositionXOnce(EntityInfo entity, ref PositionComponent position)
    {
        position.x += 1f;
    }

    /// <summary>测试用输入速度转换回调。</summary>
    private void ApplyInputVelocityForTest(EntityInfo entity, ref PlayerInputComponent input, ref MoveSpeedComponent speed, ref VelocityComponent velocity)
    {
        velocity.x = input.moveX * speed.value;
        velocity.y = 0f;
        velocity.z = input.moveY * speed.value;
    }

    /// <summary>Position + Velocity 标准顺序移动回调。</summary>
    private void MoveByVelocityOnce(EntityInfo entity, ref PositionComponent position, ref VelocityComponent velocity)
    {
        position.x += velocity.x;
        position.y += velocity.y;
        position.z += velocity.z;
    }

    /// <summary>Velocity + Position 反向泛型参数移动回调，用于验证参数顺序正确。</summary>
    private void MovePositionByVelocityReversed(EntityInfo entity, ref VelocityComponent velocity, ref PositionComponent position)
    {
        position.x += velocity.x;
        position.y += velocity.y;
        position.z += velocity.z;
    }

    /// <summary>比较两个 float 是否近似相等。</summary>
    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
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
}
