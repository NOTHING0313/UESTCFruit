using BuffSystem;
using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem;   // 2号回滚系统命名空间
using FrameWork.NetworkSync;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace View
{
    public class SimulationInitializer : MonoBehaviour
    {
        private const int DefaultDebugHudSmokeBuffDurationFrames = 60;
        private const string BuffConfigResourcesPath = "BuffSystem/Buff";

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

        [Header("Midterm Debug HUD")]
        [SerializeField] private MidtermDebugHudPresenter _midtermDebugHudPresenter;

        [Header("Debug Buff Smoke")]
        [Tooltip("Only for View/HUD smoke. This configId=1 TestBuff duration is not a production Buff config.")]
        [SerializeField] private int _debugHudSmokeBuffDurationFrames = DefaultDebugHudSmokeBuffDurationFrames;

        private World _world;
        private SimulateRunner _runner;
        private SimulationFrameCommandBuffer _frameCommandBuffer;
        private SimulationFrameCommandApplier _frameCommandApplier;
        private BuffSystemCore _buffSystem;
        private ViewManager _viewManager;
        private EntityViewBinder _binder;
        private IViewBridge _viewBridge;
        private Entity _playerEntity;

        private bool _playerBuffUIAttached = false;

        [Header("Rollback")]
        [SerializeField] private RollbackBootstrap _rollbackBootstrap;
        [SerializeField] private NetworkRollbackBootstrap _networkRollbackBootstrap;
        private SimulationDebugProbe _probe;
        private bool _directInputWriteEnabled;

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

            // ---------- Buff 系统（注册本地 smoke + Resources 配置）----------
            BuffDefinitionRegistry defRegistry = CreateBuffDefinitionRegistry();
            _buffSystem = new BuffSystemCore(defRegistry, CreateBuffEffectRegistry());

            // ---------- 固定帧推进 ----------
            _runner = new SimulateRunner(_world, _fixedDeltaTime, _maxCompensationTicks);
            _frameCommandBuffer = new SimulationFrameCommandBuffer();
            _frameCommandApplier = new SimulationFrameCommandApplier(_world, _frameCommandBuffer);
            timeSim.SetFrameCommandApplier(_frameCommandApplier);
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
                timeSim.SetInputAdapters(_inputAdapter);
                SetDirectInputWriteEnabled(true);
            }
            else
            {
                timeSim.SetInputAdapters();
            }

            // ---------- 调试面板 ----------
            if (_debugPanel != null)
            {
                _probe = new SimulationDebugProbe(_world, _buffSystem, _runner);
                _debugPanel.Initialize(_probe);
            }

            // ---------- 回滚系统自动接入 ----------
            NetworkRollbackBootstrap networkRollbackBootstrap = ResolveNetworkRollbackBootstrap();
            RollbackBootstrap rollbackBootstrap = ResolveRollbackBootstrap();

            if (networkRollbackBootstrap != null && networkRollbackBootstrap.isActiveAndEnabled && networkRollbackBootstrap.NetworkEnabled)
            {
                if (rollbackBootstrap != null && rollbackBootstrap.isActiveAndEnabled)
                {
                    Debug.LogError("SimulationInitializer Start Error: NetworkRollbackBootstrap 与 RollbackBootstrap 不能同时启用，请在网络场景禁用 RollbackBootstrap。");
                }
                else if (networkRollbackBootstrap.TryStartMount(this,out string networkMountMessage))
                {
                    Debug.Log($"SimulationInitializer Start Log: {networkMountMessage}");
                }
                else
                {
                    Debug.LogError($"SimulationInitializer Start Error: NetworkRollbackBootstrap Mount Failed: {networkMountMessage}");
                }
            }
            else if (rollbackBootstrap != null && rollbackBootstrap.isActiveAndEnabled)
            {
                rollbackBootstrap.BindBuffSystem(_buffSystem);
                Debug.Log("[SimulationInitializer] RollbackBootstrap reference resolved, attempting mount...");
                if (rollbackBootstrap.TryMount(timeSim, out string rollbackMountMessage))
                {
                    if (rollbackMountMessage == RollbackBootstrap.AlreadyMountedMessage)
                        Debug.Log("[SimulationInitializer] RollbackBootstrap already mounted.");
                    else
                        Debug.Log("[SimulationInitializer] RollbackBootstrap mounted by SimulationInitializer.");
                }
                else
                {
                    Debug.LogWarning($"[SimulationInitializer] RollbackBootstrap mount skipped: {rollbackMountMessage}");
                }
            }
            else if (rollbackBootstrap != null)
            {
                Debug.Log("[SimulationInitializer] RollbackBootstrap is disabled; rollback mount skipped.");
            }
            else
            {
                Debug.Log("[SimulationInitializer] RollbackBootstrap not configured; rollback mount skipped.");
            }

            if (_midtermDebugHudPresenter != null)
            {
                _midtermDebugHudPresenter.Initialize(
                    _world,
                    _runner,
                    _buffSystem,
                    _playerEntity,
                    _binder,
                    _worldViewRoot,
                    _playerPrefab,
                    rollbackBootstrap);
            }

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

            // 2. 输入由 TimeSimulator 在 Unity Update 中统一采样。

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
            SetDirectInputWriteEnabled(false);

            TimeSimulator timeSim = TimeSimulator.Instance;
            if (timeSim != null)
            {
                timeSim.SetInputAdapters();
                if (ReferenceEquals(timeSim.DebugFrameCommandApplier, _frameCommandApplier))
                    timeSim.SetFrameCommandApplier(null);
            }

            _viewManager?.Clear();
            _world?.Dispose();
        }

        private RollbackBootstrap ResolveRollbackBootstrap()
        {
            if (_rollbackBootstrap != null)
                return _rollbackBootstrap;

            _rollbackBootstrap = GetComponent<RollbackBootstrap>();
            return _rollbackBootstrap;
        }

        private NetworkRollbackBootstrap ResolveNetworkRollbackBootstrap()
        {
            if (_networkRollbackBootstrap != null)
                return _networkRollbackBootstrap;

            _networkRollbackBootstrap = GetComponent<NetworkRollbackBootstrap>();
            return _networkRollbackBootstrap;
        }

        /// <summary>网络回滚接线使用的当前 World。</summary>
        internal World RuntimeWorld => _world;

        /// <summary>网络回滚接线使用的当前 Runner。</summary>
        internal SimulateRunner RuntimeRunner => _runner;

        /// <summary>网络回滚接线使用的真实 FrameCommandBuffer。</summary>
        internal SimulationFrameCommandBuffer RuntimeFrameCommandBuffer => _frameCommandBuffer;

        /// <summary>网络回滚接线使用的真实 FrameCommandApplier。</summary>
        internal SimulationFrameCommandApplier RuntimeFrameCommandApplier => _frameCommandApplier;

        /// <summary>当前本地 Unity 输入 Adapter。</summary>
        internal UnityInputAdapter RuntimeInputAdapter => _inputAdapter;

        /// <summary>当前本地玩家 Entity。</summary>
        internal Entity RuntimeLocalPlayerEntity => _playerEntity;

        /// <summary>当前 BuffSystem，用于 Rollback Restore 后重建派生缓存。</summary>
        internal BuffSystemCore RuntimeBuffSystem => _buffSystem;

        /// <summary>
        /// 显式切换旧版 BeforeTick 直接输入写入所有权。
        /// 网络回滚挂载时必须关闭，避免 CollectSnapshot 被旧路径提前消费。
        /// </summary>
        internal void SetDirectInputWriteEnabled(bool enabled)
        {
            if (_runner == null || _inputAdapter == null)
            {
                _directInputWriteEnabled = false;
                return;
            }

            if (enabled == _directInputWriteEnabled)
                return;

            if (enabled)
                _runner.BeforeTick += _inputAdapter.WriteInputToWorld;
            else
                _runner.BeforeTick -= _inputAdapter.WriteInputToWorld;

            _directInputWriteEnabled = enabled;
        }

        /// <summary>把已有玩家 Entity 显式绑定到网络 PlayerID。</summary>
        internal void SetNetworkPlayerIdentity(Entity entity,int playerID)
        {
            if (_world == null || !_world.IsAlive(entity))
                throw new InvalidOperationException($"SimulationInitializer SetNetworkPlayerIdentity Error: Entity Is Not Alive: {entity}");

            if (playerID <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerID));

            _world.SetComponent(entity,new PlayerInputSnapshotComponent(0,playerID,0f,0f));
        }

        /// <summary>创建一个用于网络会话的远端玩家 Entity。</summary>
        internal Entity CreateNetworkPlayerEntity(int playerID,Vector3 spawnPos)
        {
            if (_world == null)
                throw new InvalidOperationException("SimulationInitializer CreateNetworkPlayerEntity Error: World Is Null");

            if (playerID <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerID));

            Entity entity=_world.CreateEntity();
            _world.SetComponent(entity,new PositionComponent(spawnPos.x,spawnPos.y,spawnPos.z));
            _world.SetComponent(entity,new VelocityComponent(0f,0f,0f));
            _world.SetComponent(entity,new PrefabViewRequestComponent(1));
            _world.SetComponent(entity,new PlayerInputSnapshotComponent(0,playerID,0f,0f));
            _world.SetComponent(entity,new PlayerTagComponent());
            _world.SetComponent(entity,new MoveSpeedComponent(5f));
            return entity;
        }

        private BuffDefinitionRegistry CreateBuffDefinitionRegistry()
        {
            var defRegistry = new BuffDefinitionRegistry();
            RegisterDebugHudSmokeBuff(defRegistry);
            int resourcesCount = RegisterResourcesBuffDefinitions(defRegistry);
            Debug.Log($"[SimulationInitializer] Buff definitions registered. LocalDebug=1, Resources={resourcesCount}, Total={defRegistry.Count}.");
            return defRegistry;
        }

        private void RegisterDebugHudSmokeBuff(BuffDefinitionRegistry defRegistry)
        {
            if (defRegistry == null)
                return;

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
                effectId: 0));
        }

        private int RegisterResourcesBuffDefinitions(BuffDefinitionRegistry defRegistry)
        {
            if (defRegistry == null)
                return 0;

            BuffConfigData[] configs = Resources.LoadAll<BuffConfigData>(BuffConfigResourcesPath);
            if (configs == null || configs.Length == 0)
            {
                Debug.LogWarning($"[SimulationInitializer] No BuffConfigData found in Resources/{BuffConfigResourcesPath}.");
                return 0;
            }

            int registeredCount = 0;
            var resourceNames = new HashSet<string>();

            for (int i = 0; i < configs.Length; i++)
            {
                BuffConfigData config = configs[i];
                if (config == null)
                    continue;

                if (config.ID <= 0)
                {
                    Debug.LogWarning($"[SimulationInitializer] Skip invalid Resources BuffConfigData: ID={config.ID}, AssetName={config.name}.");
                    continue;
                }

                if (defRegistry.TryGetDefinition(config.ID, out BuffDefinition _))
                {
                    Debug.LogWarning($"[SimulationInitializer] Skip duplicate Resources BuffConfigData ID={config.ID}. Existing runtime definition is preserved.");
                    continue;
                }

                string configName = !string.IsNullOrEmpty(config.Name) ? config.Name : config.name;
                if (!string.IsNullOrEmpty(configName) && !resourceNames.Add(configName))
                {
                    Debug.LogWarning($"[SimulationInitializer] Skip duplicate Resources BuffConfigData name={configName}, ID={config.ID}.");
                    continue;
                }

                try
                {
                    BuffDefinition definition = config.ToDefinition(_fixedDeltaTime);
                    defRegistry.Register(definition);
                    registeredCount++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[SimulationInitializer] Skip Resources BuffConfigData ID={config.ID}, Name={configName}: {exception.Message}");
                }
            }

            return registeredCount;
        }

        private static BuffEffectRegistry CreateBuffEffectRegistry()
        {
            var effectRegistry = new BuffEffectRegistry();
            BuffEffectRegistryBootstrap.RegisterProductionEffects(effectRegistry);
            return effectRegistry;
        }

        private void CreatePlayerEntity()
        {
            int playerID = _inputAdapter != null && _inputAdapter.PlayerID > 0 ? _inputAdapter.PlayerID : 1;
            _playerEntity = CreateNetworkPlayerEntity(playerID,Vector3.zero);

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
