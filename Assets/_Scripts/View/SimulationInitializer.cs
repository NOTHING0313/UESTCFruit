using BuffSystem;
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

        [Header("Debug")]
        [SerializeField] private LogicFrameDebugPanel _debugPanel;

        private World _world;
        private SimulateRunner _runner;
        private BuffSystemCore _buffSystem;
        private ViewManager _viewManager;
        private EntityViewBinder _binder;
        private IViewBridge _viewBridge;

        private void Start()
        {
            TimeSimulator timeSim = TimeSimulator.Instance;
            if (timeSim == null)
            {
                Debug.LogError("[SimulationInitializer] TimeSimulator.Instance missing!");
                enabled = false;
                return;
            }

            BuffConfigDataLoader definitionProvider = BuffConfigDataLoader.Instance;
            if (definitionProvider == null)
            {
                Debug.LogError("[SimulationInitializer] BuffConfigDataLoader.Instance missing. Please add BuffConfigDataLoader to the Bootstrap GameObject or scene before starting simulation.");
                enabled = false;
                return;
            }

            _world = new World();

            definitionProvider.SetTickLength(_fixedDeltaTime);
            definitionProvider.Init();

            BuffEffectRegistry effectRegistry = new BuffEffectRegistry();
            BuffEffectRegistryBootstrap.RegisterProductionEffects(effectRegistry);

            _buffSystem = BuffSystemCore.CreateForProduction(definitionProvider, effectRegistry);
            _runner = new SimulateRunner(_world, _fixedDeltaTime, _maxCompensationTicks);
            timeSim.InitSimulator(_runner);

            IViewInstanceProvider provider = new GameObjectPoolViewInstanceProvider(_worldViewRoot);
            _viewManager = new ViewManager(provider);

            // 绑定器
            _binder = new EntityViewBinder(_viewManager);
            // 桥接器
            _viewBridge = new ViewBridge(_binder, _viewManager, _buffSystem);

            // 注册 System（按 sequence 顺序）
            // 业务系统（暂未实现，注释）
            // _world.AddSystem(new InputMoveSystem());
            // _world.AddSystem(new MovementSystem());
            // _world.AddSystem(new DamageResolveSystem());
            // _world.AddSystem(new DeadCleanupSystem());

            _world.AddSystem(new BuffSystemBridge(_buffSystem));   // Buff 桥接

            _world.AddSystem(new ViewSpawnSystem(_viewManager));   // 生成 View
            _world.AddSystem(new EntityViewBindingSystem(_binder)); // 绑定 Entity ↔ View
            _world.AddSystem(new ViewSyncSystem(_viewManager));    // 同步位置
            _world.AddSystem(new ViewDestroySystem(_viewManager)); // 销毁 View （会自动解绑）
            _world.AddSystem(new WorldViewEventConsumer(_viewBridge)); // 消费事件

            if (_debugPanel != null)
            {
                var probe = new SimulationDebugProbe(_world, _buffSystem, _runner);
                _debugPanel.Initialize(probe);
            }

            Debug.Log("[SimulationInitializer] Initialized.");
        }

        private void Update()
        {
            _debugPanel?.Refresh();
        }

        private void OnDestroy()
        {
            _viewManager?.Clear();
            _world?.Dispose();
        }

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
