/*
 * RollbackBootstrap — RollBackSystem 正式入口。
 *
 * 职责：
 *   1. 通过 TimeSimulator.Instance 拿到 World/Runner
 *   2. 自动发现 UnityInputAdapter 并从中采集输入
 *   3. 自动发现 World 中的玩家 Entity（PlayerTagComponent）
 *   4. 移除 SimulationInitializer 对 Runner.BeforeTick 的直接输入写入
 *      使输入经过 Roordinator 的预测/回滚管线
 *   5. 接管 Runner.BeforeTick，coordinator.Step 写输入
 *
 * 不修改任何外部文件，全部通过运行时发现与 Hook 实现。
 */

using ECSFrameWork;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _enable = true;
        [SerializeField] private int _snapshotRingCapacity = 120;
        [SerializeField] private int _snapshotIntervalFrames = 10;

        private World _world;
        private SimulateRunner _runner;
        private UnityInputAdapter _adapter;

        private RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> _coordinator;
        private WorldRollbackAdapter<PlayerInputSnapshot> _rollbackAdapter;
        private PlayerSnapshotInputApplier _inputApplier;

        private bool _mounted;

        private void Start()
        {
            if (!_enable) return;
            StartCoroutine(InitCoro());
        }

        private IEnumerator InitCoro()
        {
            yield return null;

            var timeSim = TimeSimulator.Instance;
            if (timeSim == null)
            {
                Debug.LogError("[RollbackBootstrap] TimeSimulator.Instance missing!");
                yield break;
            }

            _world = timeSim.DebugWorld;
            _runner = timeSim.DebugRunner;
            if (_world == null || _runner == null)
            {
                Debug.LogError("[RollbackBootstrap] World or Runner not found on TimeSimulator!");
                yield break;
            }

            _adapter = FindObjectOfType<UnityInputAdapter>();
            if (_adapter == null)
                Debug.LogWarning("[RollbackBootstrap] UnityInputAdapter not found — input will fall back to hardcoded keys.");

            Mount();
        }

        private void Mount()
        {
            _inputApplier = new PlayerSnapshotInputApplier();

            var cmdBuffer = new SimulationFrameCommandBuffer();
            var cmdApplier = new SimulationFrameCommandApplier(_world, cmdBuffer);
            var frameSource = new FrameCommandSourceAdapter(cmdApplier);

            _rollbackAdapter = new WorldRollbackAdapter<PlayerInputSnapshot>(
                _world, _world, _inputApplier, frameSource);

            var snapBuf = new SnapshotRingBuffer<EcsWorldSnapshot>(_snapshotRingCapacity);

            _coordinator = new RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot>(
                new InputBuffer<PlayerInputSnapshot>(),
                new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                snapBuf, _rollbackAdapter,
                new PlayerInputSnapshotComparer(),
                new ChecksumBuffer(), new AuthoritativeChecksumBuffer())
            {
                TickLength = _runner != null ? _runner.TickLength : 1f / 60f
            };

            // 初始快照在 Mount 时 World 处于 Idle，安全
            _coordinator.SaveSnapshot();

            AutoDiscoverPlayers();
            DetachSimulationInitializerInput();

            _runner.BeforeTick += OnBeforeTick;
            _runner.AfterTick += OnAfterTick;
            _mounted = true;

            Debug.Log($"[RollbackBootstrap] Mounted. Players: {_inputApplier.PlayerCount}. Adapter: {(_adapter != null ? _adapter.name : "none")}.");
        }

        /// <summary>
        /// 自动发现 World 中带有 PlayerTagComponent 的 Entity 并注册到 InputApplier。
        /// </summary>
        private void AutoDiscoverPlayers()
        {
            var entities = new List<Entity>();
            _world.FillAliveEntities(entities);

            for (int i = 0; i < entities.Count; i++)
            {
                Entity entity = entities[i];

                if (!_world.HasComponent<PlayerTagComponent>(entity))
                    continue;

                int playerId = 0;
                if (_world.TryGetComponent<PlayerInputSnapshotComponent>(entity, out var snap))
                    playerId = snap.playerID;

                if (playerId <= 0)
                    playerId = (int)entity.ID;

                _inputApplier.RegisterPlayer(playerId, entity);
                Debug.Log($"[RollbackBootstrap] Auto-discovered player entity={entity}, playerId={playerId}.");
            }
        }

        /// <summary>
        /// 把 UnityInputAdapter 的 ECS 写入字段置空，使其后续写入变成无操作。
        /// 这样 adapter 的采样逻辑正常执行，但 WriteInputToWorld 不再生效，
        /// 输入写入由 RollbackBootstrap 的 OnBeforeTick → coordinator.Step 接管。
        /// </summary>
        private void DetachSimulationInitializerInput()
        {
            if (_adapter == null) return;

            var worldField = typeof(UnityInputAdapter).GetField("_world",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var playerField = typeof(UnityInputAdapter).GetField("_playerEntity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (worldField != null)
                worldField.SetValue(_adapter, null);

            if (playerField != null)
                playerField.SetValue(_adapter, Entity.Invalid);

            Debug.Log("[RollbackBootstrap] Disabled adapter ECS write path — input now routed through coordinator.");
        }

        /// <summary>
        /// 在每个逻辑帧开始前：采集输入 → 进入回滚预测管线 → 写入 ECS World。
        /// 接下来 SimulateRunner.TickFrame 会执行 World.Tick，System 会消费帧输入。
        /// </summary>
        private void OnBeforeTick(SimulationContext ctx)
        {
            if (!_mounted || _coordinator == null)
                return;

            var input = CollectInput(ctx.frameNumber);
            _coordinator.Step(input);
        }

        /// <summary>
        /// 每个逻辑帧结束后，World 回到 Idle，此时保存快照是安全的。
        /// </summary>
        private void OnAfterTick(SimulationContext ctx)
        {
            if (!_mounted || _coordinator == null)
                return;

            if (_coordinator.CurrentFrame % _snapshotIntervalFrames == 0)
                _coordinator.SaveSnapshot();
        }

        /// <summary>
        /// 从 UnityInputAdapter 采集当前帧输入。
        /// CollectSnapshot 会创建快照并清空一次性输入缓存（pressed/released/delta）。
        /// 若未找到 adapter 则回退到硬编码键盘读取。
        /// </summary>
        private PlayerInputSnapshot CollectInput(int frameNumber)
        {
            if (_adapter != null)
                return _adapter.CollectSnapshot(frameNumber);

            float h = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            return new PlayerInputSnapshot(frameNumber, 1) { moveX = h, moveY = 0f };
        }

        private void OnDestroy()
        {
            if (_mounted && _runner != null)
            {
                _runner.BeforeTick -= OnBeforeTick;
                _runner.AfterTick -= OnAfterTick;
            }
        }

        //--------------------------------
        // Public API
        //--------------------------------

        public RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator => _coordinator;
        public PlayerSnapshotInputApplier InputApplier => _inputApplier;
        public World World => _world;

        /// <summary>
        /// 接收远程权威输入，触发回滚校验与重模拟。
        /// 回滚完成后通过 Runner.SetFrameCount 对齐帧号。
        /// </summary>
        public void ReceiveRemoteInput(int frame, PlayerInputSnapshot input)
        {
            if (_coordinator == null) return;
            _coordinator.ReceiveAuthoritativeInput(frame, input);
            _runner?.SetFrameCount(_coordinator.CurrentFrame);
        }
    }
}
