using BuffSystem;
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

        private void Start()
        {
            TimeSimulator timeSim = TimeSimulator.Instance;
            if (timeSim == null)
            {
                Debug.LogError("[SimulationInitializer] TimeSimulator.Instance missing!");
                return;
            }

            // 1. 创建 World
            _world = new World();

            // 2. 创建 Buff 核心系统
            _buffSystem = new BuffSystemCore();

            // 3. 创建固定帧推进器
            _runner = new SimulateRunner(_world, _fixedDeltaTime, _maxCompensationTicks);
            timeSim.InitSimulator(_runner);

            // 4. 创建 ViewManager，注入对象池适配器
            IViewInstanceProvider provider = new GameObjectPoolViewInstanceProvider(_worldViewRoot);
            _viewManager = new ViewManager(provider);

            // 5. 注册 System（暂时注释未实现的类）
            // _world.AddSystem(new InputMoveSystem());
            // _world.AddSystem(new MovementSystem());
            // _world.AddSystem(new DamageResolveSystem());
            // _world.AddSystem(new DeadCleanupSystem());

            // Buff 系统通过桥接注册
            _world.AddSystem(new BuffSystemBridge(_buffSystem));

            // View 系统（若 1 号已提供，取消注释）
            // _world.AddSystem(new ViewSpawnSystem(_viewManager));
            // _world.AddSystem(new ViewSyncSystem(_viewManager));
            // _world.AddSystem(new ViewDestroySystem(_viewManager));

            // 6. 调试面板
            if (_debugPanel != null)
            {
                var probe = new SimulationDebugProbe(_world, _buffSystem, _runner);
                _debugPanel.Initialize(probe);
            }
            // ========== 测试代码开始 ==========
            // 1. 创建一个测试实体
            Entity testEntity = _world.CreateEntity();
            Debug.Log($"Test entity created: id={testEntity.ID}, version={testEntity.Version}");

            // 2. 设置一些基础组件（如果1号提供了这些组件，否则注释掉）
            // _world.SetComponent(testEntity, new PositionComponent { x = 0, y = 0, z = 0 });
            // _world.SetComponent(testEntity, new HealthComponent { current = 100, max = 100 });

            // 3. 添加一个测试 Buff（configId = 1 需要与3号对齐，先用1作为示例）
            //    假设 configId=1 是一个“每秒扣血”或“永久存在”的测试Buff
            AddBuffCommand addCmd = new AddBuffCommand(
                target: testEntity,
                source: testEntity,
                configId: 1,        // 请与3号确认这个ID是否存在
                stack: 1
            );
            _buffSystem.AddBuff(addCmd);
            Debug.Log("AddBuffCommand sent to BuffSystemCore.");

            // 4. 如果1号提供了视图组件，可以生成一个视图（需要先注册预制体）
            // _world.SetComponent(testEntity, new PrefabViewRequestComponent { prefabId = 1 });

            // ========== 测试代码结束 ==========
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

        /// <summary>
        /// 将 BuffSystemCore 适配为 IFixedStepSystem。
        /// 等 BuffSystemCore 直接实现接口后可删除。
        /// </summary>
        private class BuffSystemBridge : IFixedStepSystem
        {
            private readonly BuffSystemCore _core;
            private World _world;

            public BuffSystemBridge(BuffSystemCore core) => _core = core;

            // 执行阶段设为逻辑阶段（可根据需要调整为 movement 或 damage 之间）
            public SystemTickSequence sequence => SystemTickSequence.logic;

            public void OnCreate(World world) => _world = world;

            public void Tick(in SimulationContext context) => _core.Tick(_world, context);

            public void OnDestroy(World world) { }
        }
    }
}