using BuffSystem;   // BuffDefinition / BuffDefinitionRegistry 等所在命名空间
using Contracts;
using ECSFrameWork;
using UnityEngine;

namespace View
{
    public class SimulationInitializer : MonoBehaviour
    {
        [Header("Time")]
        [SerializeField] private float _fixedDeltaTime = 1f / 60f;
        [SerializeField] private int _maxCompensationTicks = 5;

        [Header("View")]
        [SerializeField] private Transform _worldViewRoot;
        [SerializeField] private GameObject _playerPrefab;

        [Header("Input")]
        [SerializeField] private UnityInputAdapter _inputAdapter;

        [Header("Debug")]
        [SerializeField] private LogicFrameDebugPanel _debugPanel;

        private World _world;
        private SimulateRunner _runner;
        private BuffSystemCore _buffSystem;
        private ViewManager _viewManager;
        private EntityViewBinder _binder;
        private IViewBridge _viewBridge;
        private Entity _playerEntity;

        private bool _playerBuffUIAttached = false;

        private void Start()
        {
            TimeSimulator timeSim = TimeSimulator.Instance;
            if (timeSim == null)
            {
                Debug.LogError("[SimulationInitializer] TimeSimulator.Instance missing!");
                return;
            }

            // ---------- Buff 系统（生产组合路径）----------
            BuffConfigDataLoader definitionProvider = BuffConfigDataLoader.Instance;
            if (definitionProvider == null)
            {
                Debug.LogError("[SimulationInitializer] BuffConfigDataLoader.Instance missing. Please add BuffConfigDataLoader to the scene before starting simulation.");
                enabled = false;
                return;
            }

            definitionProvider.SetTickLength(_fixedDeltaTime);
            definitionProvider.Init();

            BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
            BuffEffectRegistryBootstrap.RegisterProductionEffects(effectRegistry);
            _buffSystem = BuffSystemCore.CreateForProduction(definitionProvider, effectRegistry);

            // ---------- 核心世界 ----------
            _world = new World();

            // ---------- 固定帧推进 ----------
            _runner = new SimulateRunner(_world, _fixedDeltaTime, _maxCompensationTicks);
            timeSim.InitSimulator(_runner);

            // ---------- 视图管理 ----------
            IViewInstanceProvider provider = new GameObjectPoolViewInstanceProvider(_worldViewRoot);
            _viewManager = new ViewManager(provider);
            if (_playerPrefab != null)
                _viewManager.RegisterPrefab(1, _playerPrefab);

            // ---------- 绑定器与桥接器 ----------
            _binder = new EntityViewBinder(_viewManager);
            _viewBridge = new ViewBridge(_binder, _viewManager, _buffSystem, _world);

            // ---------- 系统注册（按 sequence 顺序）----------
            _world.AddSystem(new ViewSpawnSystem(_viewManager));
            _world.AddSystem(new EntityViewBindingSystem(_binder));
            _world.AddSystem(new InputMoveSystem());
            _world.AddSystem(new MovementSystem());
            _world.AddSystem(new BuffSystemBridge(_buffSystem));
            _world.AddSystem(new ViewSyncSystem(_viewManager));
            _world.AddSystem(new WorldViewEventConsumer(_viewBridge));
            _world.AddSystem(new ViewDestroySystem(_viewManager));
            _world.AddSystem(new EntityDestroySystem(_viewManager));

            // ---------- 创建玩家实体 ----------
            CreatePlayerEntity();
            // 测试：检查配置是否存在
           
            // ---------- 输入 ----------
            if (_inputAdapter != null)
            {
                _inputAdapter.Init(_world, _playerEntity);
                _runner.BeforeTick += _inputAdapter.WriteInputToWorld;
            }

            // ---------- 调试面板 ----------
            if (_debugPanel != null)
            {
                var probe = new SimulationDebugProbe(_world, _buffSystem, _runner);
                _debugPanel.Initialize(probe);
            }

            Debug.Log("[SimulationInitializer] Initialized. Use WASD to move.");
            Debug.Log($"[SimulationInitializer] BuffSystem production path prepared. loadedDefinitions={definitionProvider.DefinitionCount}");
        }

        private void Update()
        {
            
            if (_inputAdapter != null)
                _inputAdapter.SampleInput();

            _debugPanel?.Refresh();

            // 尝试挂载 Buff UI（仅一次，等待视图生成）
            if (!_playerBuffUIAttached && _playerEntity.IsValid)
            {
                if (_binder.TryGetView(_playerEntity, out GameObject view))
                {
                    var buffUI = view.AddComponent<PlayerBuffUI>();
                    buffUI.Initialize(_buffSystem, _playerEntity);
                    _playerBuffUIAttached = true;
                }
            }
            if (_playerEntity.IsValid && _playerBuffUIAttached && Time.frameCount % 60 == 0)
                Debug.Log($"[SimulationInitializer] Player buff count: {_buffSystem.GetBuffs(_playerEntity).Count}");
        }

        private void OnDestroy()
        {
            if (_runner != null && _inputAdapter != null)
                _runner.BeforeTick -= _inputAdapter.WriteInputToWorld;
            _viewManager?.Clear();
            _world?.Dispose();
        }

        private void CreatePlayerEntity()
        {
            Vector3 spawnPos = Vector3.zero;
            _playerEntity = _world.CreateEntity();
            _world.SetComponent(_playerEntity, new PositionComponent(spawnPos.x, spawnPos.y, spawnPos.z));
            _world.SetComponent(_playerEntity, new VelocityComponent(0f, 0f, 0f));
            _world.SetComponent(_playerEntity, new PrefabViewRequestComponent(1));
            _world.SetComponent(_playerEntity, new PlayerInputSnapshotComponent(0f, 0f));
            _world.SetComponent(_playerEntity, new PlayerTagComponent());
            _world.SetComponent(_playerEntity, new MoveSpeedComponent(5f));
            // 生产路径不在玩家创建时自动添加调试 Buff；991001 试点请通过 Debug 面板手动添加。
        }

        // BuffSystem 桥接
        private class BuffSystemBridge : IFixedStepSystem
        {
            private readonly BuffSystemCore _core;
            private World _world;
            public BuffSystemBridge(BuffSystemCore core) => _core = core;
            public SystemTickSequence sequence => SystemTickSequence.logic;
            public void OnCreate(World world) => _world = world;
            public void Tick(in SimulationContext context) => _core.Tick(_world, context);
            public void OnDestroy(World world) { }

        }
    }
}
