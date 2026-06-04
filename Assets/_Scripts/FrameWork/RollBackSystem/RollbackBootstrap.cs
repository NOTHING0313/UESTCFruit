/*
 * RollbackBootstrap — 在场景中将 RollBackSystem 与 ECS 正式连接，构建完整运行闭环。
 *
 * 架构流程：
 *   Unity Update → TimeSimulator → SimulateRunner.TickFrame(frame, isRollback)
 *     → WorldRollbackAdapter.Simulate(input, context)
 *       → IFrameCommandSource (replay or apply)
 *         → IWorldInputApplier.Apply(world, input)
 *           → Runner.TickFrame(frame, isRollback)
 *             → World.Tick(context) → all ECS Systems
 *
 * 回滚流程：
 *   收到权威输入差异 → RollbackCoordinator.ReceiveAuthoritativeInput(frame, input)
 *     → RollbackTo(frame) → IRollbackableWorld.Restore(snapshot) → ECS snapshot restore
 *     → ResimulateTo(target) → 每帧重新调用 WorldRollbackAdapter.Simulate(重放输入)
 */

using BuffSystem;
using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;
using UnityEngine;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackBootstrap : MonoBehaviour
    {
        [Header("Time")]
        [SerializeField] private float _tickLength = 1f / 60f;
        [SerializeField] private int _maxTickCountPerFrame = 5;
        [SerializeField] private int _snapshotRingCapacity = 120;

        [Header("Player")]
        [SerializeField] private int _playerID = 1;
        [SerializeField] private float _moveSpeed = 5f;

        [Header("View")]
        [SerializeField] private int _playerPrefabID = 1;
        [SerializeField] private GameObject _playerViewPrefab;

        [Header("Input")]
        [SerializeField] private UnityInputAdapter _inputAdapter;

        //--------------------------------
        // Core
        //--------------------------------

        private World _world;
        private SimulateRunner _runner;

        //--------------------------------
        // Rollback
        //--------------------------------

        private RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> _coordinator;
        private WorldRollbackAdapter<PlayerInputSnapshot> _rollbackAdapter;
        private PlayerSnapshotInputApplier _inputApplier;
        private FrameCommandSourceAdapter _frameCommandSource;
        private InputBuffer<PlayerInputSnapshot> _inputBuffer;
        private AuthoritativeInputBuffer<PlayerInputSnapshot> _authoritativeInputBuffer;
        private SnapshotRingBuffer<EcsWorldSnapshot> _snapshotBuffer;
        private ChecksumBuffer _checksumBuffer;
        private AuthoritativeChecksumBuffer _authoritativeChecksumBuffer;

        //--------------------------------
        // View
        //--------------------------------

        private ViewManager _viewManager;

        //--------------------------------
        // Buff
        //--------------------------------

        private BuffSystemCore _buffSystem;

        //--------------------------------
        // State
        //--------------------------------

        private Entity _playerEntity;
        private bool _initialized;

        //--------------------------------
        // Unity Lifecycle
        //--------------------------------

        private void Start()
        {
            Init();
        }

        private void Update()
        {
            if (!_initialized) return;

            _inputAdapter?.SampleInput();

            PlayerInputSnapshot snapshot = _inputAdapter != null
                ? _inputAdapter.CollectSnapshot(_runner.NextFrameNumber)
                : default;

            _coordinator.Step(snapshot);

            if (_coordinator.CurrentFrame % 10 == 0)
                _coordinator.SaveSnapshot();
        }

        private void OnDestroy()
        {
            _world?.Dispose();
            _viewManager?.Clear();
        }

        //--------------------------------
        // Init
        //--------------------------------

        public void Init()
        {
            if (_initialized) return;

            CreateWorld();
            CreateBuffSystem();
            CreateRunner();
            CreateViewManager();
            CreateInputPipeline();
            CreateFrameCommandPipeline();
            CreateRollbackAdapter();
            CreateCoordinator();
            CreateSystems();
            CreatePlayerEntity();
            PrepareInputAdapter();

            _initialized = true;
            Debug.Log("[RollbackBootstrap] Initialized.");
        }

        //--------------------------------
        // World
        //--------------------------------

        private void CreateWorld()
        {
            _world = new World();
        }

        private void CreateBuffSystem()
        {
            var loader = BuffConfigDataLoader.Instance;
            if (loader == null) return;

            loader.SetTickLength(_tickLength);
            loader.Init();

            var registry = new BuffEffectRegistry();
            BuffEffectRegistryBootstrap.RegisterProductionEffects(registry);
            _buffSystem = BuffSystemCore.CreateForProduction(loader, registry);
        }

        private void CreateRunner()
        {
            _runner = new SimulateRunner(_world, _tickLength, _maxTickCountPerFrame);
        }

        private void CreateViewManager()
        {
            var provider = new PoolSystemViewInstanceProvider();
            _viewManager = new ViewManager(provider);

            GameObject prefab = _playerViewPrefab != null
                ? _playerViewPrefab
                : CreateDefaultPlayerPrefab();

            _viewManager.RegisterPrefab(_playerPrefabID, prefab);
        }

        private static GameObject CreateDefaultPlayerPrefab()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Player_Default";
            cube.transform.position = new Vector3(9999f, 9999f, 9999f);
            DontDestroyOnLoad(cube);
            return cube;
        }

        //--------------------------------
        // Input Pipeline
        //--------------------------------

        private void CreateInputPipeline()
        {
            _inputBuffer = new InputBuffer<PlayerInputSnapshot>();
            _authoritativeInputBuffer = new AuthoritativeInputBuffer<PlayerInputSnapshot>();
            _inputApplier = new PlayerSnapshotInputApplier();
        }

        //--------------------------------
        // Frame Command Pipeline
        //--------------------------------

        private void CreateFrameCommandPipeline()
        {
            var commandBuffer = new SimulationFrameCommandBuffer();
            var commandApplier = new SimulationFrameCommandApplier(_world, commandBuffer);
            _frameCommandSource = new FrameCommandSourceAdapter(commandApplier);
        }

        //--------------------------------
        // Rollback Adapter
        //--------------------------------

        private void CreateRollbackAdapter()
        {
            _rollbackAdapter = new WorldRollbackAdapter<PlayerInputSnapshot>(
                snapshotProvider: _world,
                world: _world,
                runner: _runner,
                inputApplier: _inputApplier,
                frameCommandSource: _frameCommandSource);
        }

        //--------------------------------
        // Coordinator
        //--------------------------------

        private void CreateCoordinator()
        {
            _snapshotBuffer = new SnapshotRingBuffer<EcsWorldSnapshot>(_snapshotRingCapacity);
            _checksumBuffer = new ChecksumBuffer();
            _authoritativeChecksumBuffer = new AuthoritativeChecksumBuffer();

            _coordinator = new RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot>(
                inputBuffer: _inputBuffer,
                authoritativeInputBuffer: _authoritativeInputBuffer,
                snapshotBuffer: _snapshotBuffer,
                world: _rollbackAdapter,
                runner: null,
                inputComparer: new PlayerInputSnapshotComparer(),
                checksumBuffer: _checksumBuffer,
                authoritativeChecksumBuffer: _authoritativeChecksumBuffer);
        }

        //--------------------------------
        // Systems
        //--------------------------------

        private void CreateSystems()
        {
            // Input
            _world.AddSystem(new InputMoveSystem());

            // Movement
            _world.AddSystem(new MovementSystem());

            // Buff bridge
            if (_buffSystem != null)
                _world.AddSystem(new BuffBridge(_buffSystem));

            // View
            _world.AddSystem(new ViewSpawnSystem(_viewManager));
            _world.AddSystem(new ViewSyncSystem(_viewManager));
            _world.AddSystem(new ViewDestroySystem(_viewManager));
            _world.AddSystem(new EntityDestroySystem(_viewManager));
        }

        //--------------------------------
        // Player Entity
        //--------------------------------

        private void CreatePlayerEntity()
        {
            Vector3 spawnPos = Vector3.zero;
            _playerEntity = _world.CreateMovingEntityWithView(_playerPrefabID, spawnPos, Vector3.zero);

            _world.SetComponent(_playerEntity, new PlayerTagComponent());
            _world.SetComponent(_playerEntity, new PlayerInputSnapshotComponent(0f, 0f));
            _world.SetComponent(_playerEntity, new MoveSpeedComponent(_moveSpeed));

            _inputApplier.RegisterPlayer(_playerID, _playerEntity);
        }

        //--------------------------------
        // Input Adapter
        //--------------------------------

        private void PrepareInputAdapter()
        {
            if (_inputAdapter == null)
                _inputAdapter = GetComponent<UnityInputAdapter>();

            if (_inputAdapter == null)
                _inputAdapter = gameObject.AddComponent<UnityInputAdapter>();

            _inputAdapter.Init(_world, _playerEntity);
        }

        //--------------------------------
        // Debug / Public API
        //--------------------------------

        public int CurrentFrame => _coordinator?.CurrentFrame ?? 0;
        public World World => _world;
        public RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator => _coordinator;

        /// <summary>模拟收到权威输入以触发回滚。</summary>
        public void ReceiveRemoteInput(int frame, PlayerInputSnapshot input)
        {
            _authoritativeInputBuffer.Save(frame, in input);
            _coordinator.ReceiveAuthoritativeInput(frame, in input);
        }

        //--------------------------------
        // Buff Bridge (internal)
        //--------------------------------

        private sealed class BuffBridge : IFixedStepSystem
        {
            private readonly BuffSystemCore _core;
            private World _world;
            public BuffBridge(BuffSystemCore core) => _core = core;
            public SystemTickSequence sequence => SystemTickSequence.logic;
            public void OnCreate(World world) => _world = world;
            public void Tick(in SimulationContext context) => _core.Tick(_world, context);
            public void OnDestroy(World world) { }
        }
    }
}
