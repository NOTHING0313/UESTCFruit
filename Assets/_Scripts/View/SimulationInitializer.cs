using BuffSystem;
using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem;   // 2号回滚系统命名空间
using System;
using UnityEngine;

namespace View
{
    public class SimulationInitializer : MonoBehaviour
    {
        private const int DefaultDebugHudSmokeBuffDurationFrames = 60;

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

        [Header("Buff HUD")]
        [SerializeField] private BuffTextHudPresenter _buffTextHudPresenter;

        [Header("Debug Buff Smoke")]
        [Tooltip("Only for View/HUD smoke. This configId=1 TestBuff duration is not a production Buff config.")]
        [SerializeField] private int _debugHudSmokeBuffDurationFrames = DefaultDebugHudSmokeBuffDurationFrames;

        private World _world;
        private SimulateRunner _runner;
        private BuffSystemCore _buffSystem;
        private ViewManager _viewManager;
        private EntityViewBinder _binder;
        private IViewBridge _viewBridge;
        private Entity _playerEntity;

        private bool _playerBuffUIAttached = false;

        // 回滚相关
        private RollbackBootstrap _rollbackBootstrap;
        private SimulationDebugProbe _probe;

        private void Start()
        {
            TimeSimulator timeSim = TimeSimulator.Instance;
            if (timeSim == null)
            {
                Debug.LogError("[SimulationInitializer] TimeSimulator.Instance missing!");
                return;
            }

            // ---------- 核心世界 ----------
            _world = new World();

            // ---------- Buff 系统（注册测试用 configId=1）----------
            var defRegistry = new BuffDefinitionRegistry();
            defRegistry.Register(new BuffDefinition(
                configId: 1,
                name: "TestBuff",
                priority: 0,
                maxStack: 1,
                unlimited: false,
                isForever: false,
                durationFrames: GetDebugHudSmokeBuffDurationFrames(),
                tickIntervalFrames: 0,
                durationExtendFramesPerStack: 0,
                triggerType: BuffTriggerType.Tick,
                buffType: BuffInstanceType.normal,
                normalStackPolicy: NormalBuffStackPolicy.RefreshDuration,
                parallelStackUpPolicy: ParallelBuffStackUpPolicy.Append,
                parallelStackDownPolicy: ParallelBuffStackDownPolicy.RemoveEarliest,
                effectId: 0
            ));
            _buffSystem = new BuffSystemCore(defRegistry, new BuffEffectRegistry());

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

            if (_buffTextHudPresenter != null)
                _buffTextHudPresenter.Initialize(_buffSystem, _playerEntity);

            // ---------- 输入 ----------
            if (_inputAdapter != null)
            {
                _inputAdapter.Init(_world, _playerEntity);
                _runner.BeforeTick += _inputAdapter.WriteInputToWorld;
            }

            // ---------- 调试面板 ----------
            if (_debugPanel != null)
            {
                _probe = new SimulationDebugProbe(_world, _buffSystem, _runner);
                _debugPanel.Initialize(_probe);
            }

            // ---------- 回滚系统自动接入 ----------
            _rollbackBootstrap = GetComponent<RollbackBootstrap>();
            if (_rollbackBootstrap != null)
                Debug.Log("[SimulationInitializer] RollbackBootstrap found – probe will show checksum.");
            else
                Debug.Log("[SimulationInitializer] No RollbackBootstrap on this GameObject – checksum will be 0.");

            Debug.Log("[SimulationInitializer] Initialized. Use WASD to move.");
        }

        private void Update()
        {
            // 1. 更新回滚状态到调试面板
            if (_probe != null && _rollbackBootstrap != null && _rollbackBootstrap.Coordinator != null)
            {
                uint checksum = _rollbackBootstrap.Coordinator.CalculateChecksum();
                bool isRollback = false; // 目前回滚系统未暴露该标志，保持 false
                _probe.SetRollbackInfo(isRollback, checksum);
            }

            // 2. 输入采样
            if (_inputAdapter != null)
                _inputAdapter.SampleInput();

            // 3. 挂载 Buff UI（仅一次）
            if (!_playerBuffUIAttached && _playerEntity.IsValid)
            {
                if (_binder.TryGetView(_playerEntity, out GameObject view))
                {
                    var buffUI = view.AddComponent<PlayerBuffUI>();
                    buffUI.Initialize(_buffSystem, _playerEntity);
                    _playerBuffUIAttached = true;
                }
            }

            // 4. 刷新调试面板（放在最后，确保数据最新）
            _debugPanel?.Refresh();
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

            // 测试 Buff
            var buffCmd = new AddBuffCommand(_playerEntity, configId: 1, source: _playerEntity, stack: 1);
            _buffSystem.AddBuff(buffCmd);
        }

        private int GetDebugHudSmokeBuffDurationFrames()
        {
            return _debugHudSmokeBuffDurationFrames > 0
                ? _debugHudSmokeBuffDurationFrames
                : DefaultDebugHudSmokeBuffDurationFrames;
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
