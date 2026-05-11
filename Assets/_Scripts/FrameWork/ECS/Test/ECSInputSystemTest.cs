using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 测试 ECS 输入链路：输入组件 -> 速度组件 -> 位置组件 -> Unity View 同步。
/// </summary>
public sealed class ECSInputSystemTestBootstrap : MonoBehaviour
{
    private const int CubePrefabID = 1;

    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Input System Test] Start</color>");

        TestInputMoveSystemSetsVelocity();
        TestInputMoveSystemDrivesMovement();
        TestInputMovementSyncsToView();
        TestZeroInputStopsMovement();
        TestFrameBoundInputIgnoresStaleFrame();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Input System Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Input System Test] Failed count = {_failedCount}");
    }

    /// <summary>验证 PlayerInputSnapshotComponent 会被 InputMoveSystem 转换成 VelocityComponent。</summary>
    private void TestInputMoveSystemSetsVelocity()
    {
        Debug.Log("<color=cyan>[Test 1] InputMoveSystem Sets Velocity</color>");

        TestEnvironment env = CreateEnvironment(false);
        Entity entity = env.World.CreateEntity();

        env.World.SetComponent(entity, new PlayerInputSnapshotComponent(1f, -0.5f));
        env.World.SetComponent(entity, new MoveSpeedComponent(4f));
        env.World.SetComponent(entity, new VelocityComponent(0f, 0f, 0f));
        env.World.AddSystem(new InputMoveSystem());

        env.Tick(1, 0.5f);

        ref VelocityComponent velocity = ref env.World.GetComponent<VelocityComponent>(entity);

        ExpectApproximately(velocity.x, 4f, "Velocity.x should equal input.moveX * speed.");
        ExpectApproximately(velocity.y, 0f, "Velocity.y should remain 0.");
        ExpectApproximately(velocity.z, -2f, "Velocity.z should equal input.moveY * speed.");

        env.Dispose();
        Debug.Log("<color=green>[Test 1] Finished</color>");
    }

    /// <summary>验证输入转速度后，MovementSystem 会使用速度推进 PositionComponent。</summary>
    private void TestInputMoveSystemDrivesMovement()
    {
        Debug.Log("<color=cyan>[Test 2] InputMoveSystem Drives Movement</color>");

        TestEnvironment env = CreateEnvironment(false);
        Entity entity = env.World.CreateEntity();

        env.World.SetComponent(entity, new PositionComponent(0f, 0f, 0f));
        env.World.SetComponent(entity, new VelocityComponent(0f, 0f, 0f));
        env.World.SetComponent(entity, new PlayerInputSnapshotComponent(0f, 1f));
        env.World.SetComponent(entity, new MoveSpeedComponent(2f));
        env.World.AddSystem(new InputMoveSystem());
        env.World.AddSystem(new MovementSystem());

        env.Tick(1, 0.5f);

        ref PositionComponent position = ref env.World.GetComponent<PositionComponent>(entity);
        ref VelocityComponent velocity = ref env.World.GetComponent<VelocityComponent>(entity);

        ExpectApproximately(velocity.z, 2f, "Velocity.z should be 2 after input conversion.");
        ExpectApproximately(position.z, 1f, "Position.z should move by velocity.z * tickLength.");

        env.Dispose();
        Debug.Log("<color=green>[Test 2] Finished</color>");
    }

    /// <summary>验证完整 Unity Adapter 表现链路：输入 -> 移动 -> View 同步。</summary>
    private void TestInputMovementSyncsToView()
    {
        Debug.Log("<color=cyan>[Test 3] Input Movement Syncs To View</color>");

        TestEnvironment env = CreateEnvironment(true);
        Entity entity = env.World.CreateMovingEntityWithView(CubePrefabID, Vector3.zero, Vector3.zero);

        env.World.SetComponent(entity, new PlayerTagComponent());
        env.World.SetComponent(entity, new PlayerInputSnapshotComponent(1f, 0f));
        env.World.SetComponent(entity, new MoveSpeedComponent(2f));

        env.Tick(1, 0.5f);

        Expect(env.World.HasComponent<ViewComponent>(entity), "Entity should have ViewComponent after first Tick playback.");

        ref ViewComponent view = ref env.World.GetComponent<ViewComponent>(entity);

        Expect(env.ViewManager.TryGetTransform(view.viewID, out Transform target), "ViewManager should find Transform after ViewSpawnSystem.");

        env.Tick(1, 0.5f);

        ref PositionComponent position = ref env.World.GetComponent<PositionComponent>(entity);
        Vector3 ecsPosition = new Vector3(position.x, position.y, position.z);

        ExpectVectorApproximately(ecsPosition, new Vector3(2f, 0f, 0f), "ECS Position should be (2, 0, 0) after two ticks.");
        ExpectVectorApproximately(target.position, ecsPosition, "Transform position should sync to ECS Position.");

        env.Dispose();
        Debug.Log("<color=green>[Test 3] Finished</color>");
    }

    /// <summary>验证输入归零后，InputMoveSystem 会把速度归零并停止移动。</summary>
    private void TestZeroInputStopsMovement()
    {
        Debug.Log("<color=cyan>[Test 4] Zero Input Stops Movement</color>");

        TestEnvironment env = CreateEnvironment(true);
        Entity entity = env.World.CreateMovingEntityWithView(CubePrefabID, Vector3.zero, Vector3.zero);

        env.World.SetComponent(entity, new PlayerInputSnapshotComponent(1f, 0f));
        env.World.SetComponent(entity, new MoveSpeedComponent(2f));

        env.Tick(2, 0.5f);

        ref PositionComponent beforePosition = ref env.World.GetComponent<PositionComponent>(entity);
        Vector3 previousPosition = new Vector3(beforePosition.x, beforePosition.y, beforePosition.z);

        env.World.SetComponent(entity, new PlayerInputSnapshotComponent(0f, 0f));
        env.Tick(1, 0.5f);

        ref PositionComponent afterPosition = ref env.World.GetComponent<PositionComponent>(entity);
        ref VelocityComponent velocity = ref env.World.GetComponent<VelocityComponent>(entity);
        Vector3 currentPosition = new Vector3(afterPosition.x, afterPosition.y, afterPosition.z);

        ExpectApproximately(velocity.x, 0f, "Velocity.x should become 0 after zero input.");
        ExpectApproximately(velocity.z, 0f, "Velocity.z should become 0 after zero input.");
        ExpectVectorApproximately(currentPosition, previousPosition, "Position should not change after input becomes zero.");

        env.Dispose();
        Debug.Log("<color=green>[Test 4] Finished</color>");
    }


    /// <summary>验证绑定到具体逻辑帧的输入不会被错误消费到其他逻辑帧。</summary>
    private void TestFrameBoundInputIgnoresStaleFrame()
    {
        Debug.Log("<color=cyan>[Test 5] Frame Bound Input Ignores Stale Frame</color>");

        TestEnvironment env = CreateEnvironment(false);
        Entity entity = env.World.CreateEntity();

        env.World.SetComponent(entity, new PlayerInputSnapshotComponent(5, 0, 1f, 0f));
        env.World.SetComponent(entity, new MoveSpeedComponent(3f));
        env.World.SetComponent(entity, new VelocityComponent(9f, 0f, 9f));
        env.World.AddSystem(new InputMoveSystem());

        env.Tick(1, 0.02f);

        ref VelocityComponent velocity = ref env.World.GetComponent<VelocityComponent>(entity);

        ExpectApproximately(velocity.x, 0f, "Stale frame input should clear Velocity.x.");
        ExpectApproximately(velocity.z, 0f, "Stale frame input should clear Velocity.z.");

        env.Dispose();
        Debug.Log("<color=green>[Test 5] Finished</color>");
    }

    /// <summary>创建测试环境，并按需注册 Unity 表现相关 System。</summary>
    private TestEnvironment CreateEnvironment(bool withUnityAdapterSystems)
    {
        TestEnvironment env = new TestEnvironment();
        env.Create();

        if (withUnityAdapterSystems)
        {
            env.World.AddSystem(new ViewSpawnSystem(env.ViewManager));
            env.World.AddSystem(new InputMoveSystem());
            env.World.AddSystem(new MovementSystem());
            env.World.AddSystem(new ViewSyncSystem(env.ViewManager));
            env.World.AddSystem(new ViewDestroySystem(env.ViewManager));
            env.World.AddSystem(new EntityDestroySystem(env.ViewManager));
        }

        return env;
    }

    /// <summary>判断浮点值是否近似相等。</summary>
    private void ExpectApproximately(float actual, float expected, string message)
    {
        if (Mathf.Approximately(actual, expected))
        {
            Debug.Log($"<color=green>[PASS]</color> {message} Actual = {actual}");
        }
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message} Expected = {expected}, Actual = {actual}");
        }
    }

    /// <summary>判断 Vector3 是否近似相等。</summary>
    private void ExpectVectorApproximately(Vector3 actual, Vector3 expected, string message)
    {
        bool result = Mathf.Approximately(actual.x, expected.x)
            && Mathf.Approximately(actual.y, expected.y)
            && Mathf.Approximately(actual.z, expected.z);

        if (result)
        {
            Debug.Log($"<color=green>[PASS]</color> {message} Actual = {actual}");
        }
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message} Expected = {expected}, Actual = {actual}");
        }
    }

    /// <summary>输出普通布尔断言。</summary>
    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
        }
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message}");
        }
    }

    /// <summary>测试环境，负责创建 World、ViewManager、Prefab 模板和逻辑帧推进。</summary>
    private sealed class TestEnvironment
    {
        public World World { get; private set; }
        public ViewManager ViewManager { get; private set; }

        private GameObject _prefabTemplate;
        private int _frameNumber;

        /// <summary>初始化测试环境。</summary>
        public void Create()
        {
            World = new World();
            ViewManager = new ViewManager();

            _prefabTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _prefabTemplate.name = "ECS_Input_Test_View_Template";
            _prefabTemplate.SetActive(false);

            ViewManager.RegisterPrefab(CubePrefabID, _prefabTemplate);
        }

        /// <summary>推进指定次数的固定逻辑帧。</summary>
        public void Tick(int count, float tickLength)
        {
            for (int i = 0; i < count; i++)
            {
                _frameNumber++;
                SimulationContext context = new SimulationContext(_frameNumber, tickLength, false);
                World.Tick(in context);
            }
        }

        /// <summary>清理测试环境。</summary>
        public void Dispose()
        {
            if (World != null)
            {
                World.Dispose();
                World = null;
            }

            if (ViewManager != null)
            {
                ViewManager.Clear();
                ViewManager = null;
            }

            if (_prefabTemplate != null)
            {
                Destroy(_prefabTemplate);
                _prefabTemplate = null;
            }
        }
    }
}

}
