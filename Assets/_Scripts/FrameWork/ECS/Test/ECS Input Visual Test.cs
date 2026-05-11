using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 可视化测试 ECS 输入链路：UnityInputAdapter -> PlayerInputSnapshotComponent -> InputMoveSystem -> MovementSystem -> ViewSyncSystem。
/// </summary>
public sealed class ECSInputVisualTestBootstrap : MonoBehaviour
{
    private const int CubePrefabID = 1;

    [Header("View")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;

    [Header("Input")]
    [SerializeField] private UnityInputAdapter inputAdapter;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Simulation")]
    [SerializeField] private float tickLength = 0.02f;
    [SerializeField] private int maxTickCountPerFrame = 5;

    private World _world;
    private ViewManager _viewManager;
    private SimulateRunner _runner;
    private Entity _playerEntity;
    private GameObject _runtimeCubeTemplate;

    private void Start()
    {
        CreateWorld();
        CreateViewManager();
        CreateSystems();
        CreatePlayerEntity();
        PrepareInputAdapter();
        CreateRunner();

        Debug.Log("<color=cyan>[ECSInputVisualTestBootstrap] Started. Use WASD to move the cube.</color>");
    }

    private void Update()
    {
        if (inputAdapter != null)
            inputAdapter.SampleInput();

        _runner?.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        Dispose();
    }

    /// <summary>创建 ECS World。</summary>
    private void CreateWorld()
    {
        _world = new World();
    }

    /// <summary>创建 ViewManager，并注册可用于生成 View 的 Prefab。</summary>
    private void CreateViewManager()
    {
        _viewManager = new ViewManager(new PoolSystemViewInstanceProvider());

        GameObject prefab = cubePrefab != null ? cubePrefab : CreateRuntimeCubeTemplate();
        _viewManager.RegisterPrefab(CubePrefabID, prefab);
    }

    /// <summary>注册 ECS 系统。</summary>
    private void CreateSystems()
    {
        _world.AddSystem(new ViewSpawnSystem(_viewManager));
        _world.AddSystem(new InputMoveSystem());
        _world.AddSystem(new MovementSystem());
        _world.AddSystem(new ViewSyncSystem(_viewManager));
        _world.AddSystem(new ViewDestroySystem(_viewManager));
        _world.AddSystem(new EntityDestroySystem(_viewManager));
    }

    /// <summary>创建玩家 Entity，并请求生成 Unity View。</summary>
    private void CreatePlayerEntity()
    {
        _playerEntity = _world.CreateMovingEntityWithView(CubePrefabID, spawnPosition, Vector3.zero);

        _world.SetComponent(_playerEntity, new PlayerTagComponent());
        _world.SetComponent(_playerEntity, new PlayerInputSnapshotComponent(0f, 0f));
        _world.SetComponent(_playerEntity, new MoveSpeedComponent(moveSpeed));
    }

    /// <summary>准备输入 Adapter，并绑定当前玩家 Entity。</summary>
    private void PrepareInputAdapter()
    {
        if (inputAdapter == null)
            inputAdapter = GetComponent<UnityInputAdapter>();

        if (inputAdapter == null)
            inputAdapter = gameObject.AddComponent<UnityInputAdapter>();

        inputAdapter.Init(_world, _playerEntity);
    }

    /// <summary>创建逻辑帧推进器，并在每个逻辑帧开始前写入输入快照。</summary>
    private void CreateRunner()
    {
        _runner = new SimulateRunner(_world, tickLength, maxTickCountPerFrame);

        if (inputAdapter != null)
            _runner.BeforeTick += inputAdapter.WriteInputToWorld;
    }

    /// <summary>创建运行时 Cube 模板；真正显示的是 ViewSpawnSystem 生成的实例。</summary>
    private GameObject CreateRuntimeCubeTemplate()
    {
        _runtimeCubeTemplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _runtimeCubeTemplate.name = "ECS_Runtime_Cube_Template";
        _runtimeCubeTemplate.transform.position = new Vector3(9999f, 9999f, 9999f);
        _runtimeCubeTemplate.SetActive(true);

        return _runtimeCubeTemplate;
    }

    /// <summary>释放 World、ViewManager 和运行时模板。</summary>
    private void Dispose()
    {
        if (_runner != null && inputAdapter != null)
            _runner.BeforeTick -= inputAdapter.WriteInputToWorld;

        _runner = null;

        if (_world != null)
        {
            _world.Dispose();
            _world = null;
        }

        if (_viewManager != null)
        {
            _viewManager.Clear();
            _viewManager = null;
        }

        if (_runtimeCubeTemplate != null)
        {
            Destroy(_runtimeCubeTemplate);
            _runtimeCubeTemplate = null;
        }
    }
}

}
