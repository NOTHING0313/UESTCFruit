//using BuffSystem;
//using Contracts;
//using Drivers;
//using ECSFrameWork;
//using FrameWork.RollBackSystem;
//using UnityEngine;

//namespace View
//{
//    public sealed class SimulationBootstrap : MonoBehaviour
//    {
//        [Header("Time")]
//        [SerializeField] private float _fixedDeltaTime = 1f / 60f;
//        [SerializeField] private int _maxCompensationTicks = 5;

//        [Header("View")]
//        [SerializeField] private Transform _worldViewRoot;
//        [SerializeField] private ViewPrefabCatalog _viewPrefabCatalog;
//        [SerializeField] private BuffUIViewPresenter _buffUIViewPresenter;
//        [SerializeField] private BuffUIViewConfig _buffUIViewConfig;

//        [Header("Input")]
//        [SerializeField] private UnityInputAdapter _inputAdapter;

//        [Header("Debug")]
//        [SerializeField] private LogicFrameDebugPanel _debugPanel;

//        [Header("Mode")]
//        [SerializeField] private bool _enableRollback;

//        private World _world;
//        private SimulateRunner _runner;
//        private BuffSystemCore _buffSystem;
//        private ViewManager _viewManager;
//        private EntityViewBinder _binder;
//        private ObjectPoolFacade _pool;
//        private IViewBridge _viewBridge;
//        private ISimulationDriver _driver;
//        private SimulationDebugProbe _probe;
//        private RollbackBootstrap _rollbackBootstrap;
//        private Entity _playerEntity = Entity.Invalid;

//        private void Awake()
//        {
//            CreateCore();
//            CreateView();
//            RegisterSystems();
//            CreateDemoPlayer();
//            CreateDriver();
//            CreateDebug();
//        }

//        private void Update()
//        {
//            _inputAdapter?.SampleInput();
//            _debugPanel?.Refresh();
//        }

//        private void OnDestroy()
//        {
//            if (_runner != null && _inputAdapter != null)
//                _runner.BeforeTick -= _inputAdapter.WriteInputToWorld;

//            _viewManager?.Clear();
//            _world?.Dispose();
//        }

//        private void CreateCore()
//        {
//            _world = new World();

//            var definitions = new BuffDefinitionRegistry();
//            var effects = new BuffEffectRegistry();
//            _buffSystem = new BuffSystemCore(definitions, effects);

//            _runner = new SimulateRunner(_world, _fixedDeltaTime, _maxCompensationTicks);
//            TimeSimulator.Instance.InitSimulator(_runner);
//        }

//        private void CreateView()
//        {
//            _pool = new ObjectPoolFacade(_viewPrefabCatalog);
//            _viewManager = new ViewManager(new GameObjectPoolViewInstanceProvider(_worldViewRoot));
//            _binder = new EntityViewBinder(_viewManager, entity => _world != null && _world.IsAlive(entity));

//            if (_buffUIViewPresenter != null)
//                _buffUIViewPresenter.Initialize(_pool, _buffUIViewConfig, _fixedDeltaTime);

//            _viewBridge = new ViewBridge(
//                _binder,
//                _viewManager,
//                _pool,
//                _buffUIViewPresenter,
//                _world);
//        }

//        private void RegisterSystems()
//        {
//            _world.AddSystem(new ViewSpawnSystem(_viewManager));
//            _world.AddSystem(new EntityViewBindingSystem(_binder));
//            _world.AddSystem(new InputMoveSystem());
//            _world.AddSystem(new MovementSystem());
//            _world.AddSystem(new BuffSystemBridge(_buffSystem));
//            _world.AddSystem(new ViewSyncSystem(_viewManager));
//            _world.AddSystem(new BuffUIRefreshSystem(_buffUIViewPresenter, _buffSystem));
//            _world.AddSystem(new WorldViewEventConsumer(_viewBridge));
//            _world.AddSystem(new ViewDestroySystem(_viewManager));
//            _world.AddSystem(new EntityDestroySystem(_viewManager));
//        }

//        private void CreateDemoPlayer()
//        {
//            _playerEntity = _world.CreateEntity();

//            _world.AddComponent(_playerEntity, new PlayerTagComponent());
//            _world.AddComponent(_playerEntity, new PositionComponent { x = 0f, y = 0f });
//            _world.AddComponent(_playerEntity, new PrefabViewRequestComponent { prefabId = 1 });

//            if (_inputAdapter != null)
//            {
//                _inputAdapter.Init(_world, _playerEntity);
//                _runner.BeforeTick += _inputAdapter.WriteInputToWorld;
//            }
//        }

//        private void CreateDriver()
//        {
//            _rollbackBootstrap = GetComponent<RollbackBootstrap>();

//            if (_enableRollback && _rollbackBootstrap != null)
//                _driver = new RollbackSimulationDriver(_rollbackBootstrap, _runner);
//            else
//                _driver = new RealtimeSimulationDriver(_runner);
//        }

//        private void CreateDebug()
//        {
//            if (_debugPanel == null)
//                return;

//            _probe = new SimulationDebugProbe(_world, _buffSystem, _runner);
//            _probe.BindRollback(_rollbackBootstrap);
//            _debugPanel.Initialize(_probe);
//        }
//    }
//}