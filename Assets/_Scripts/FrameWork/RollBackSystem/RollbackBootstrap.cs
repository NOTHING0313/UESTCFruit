/*
 * RollbackBootstrap — RollBackSystem 正式入口。
 *
 * 职责：
 *   1. 通过 TimeSimulator.Instance 拿到 World/Runner
 *   2. 自动发现 UnityInputAdapter 并从中采集输入
 *   3. 自动发现 World 中的玩家 Entity（PlayerTagComponent）
 *   4. 移除 SimulationInitializer 对 Runner.BeforeTick 的直接输入写入
 *      使输入经过 Roordinator 的预测/回滚管线
 *   5. 接管 Runner.BeforeTick，coordinator.TryStep 写输入
 *
 * 不修改任何外部文件，全部通过运行时发现与 Hook 实现。
 */

using BuffSystem;
using ECSFrameWork;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _enable = true;
        [SerializeField] private int _snapshotRingCapacity = 120;
        [SerializeField] private int _snapshotIntervalFrames = 10;

        /// <summary>服务端期望客户端到达的帧号，用于加速追帧。</summary>
        [SerializeField] private int _expectedFrame;

        internal const string AlreadyMountedMessage = "Already mounted.";

        private World _world;
        private SimulateRunner _runner;
        private UnityInputAdapter _adapter;

        private RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> _coordinator;
        private WorldRollbackAdapter<PlayerInputSnapshot> _rollbackAdapter;
        private PlayerSnapshotInputApplier _inputApplier;
        private RollbackFrameCommandReplayBinding _frameCommandReplayBinding;
        private BuffSystemCore _boundBuffSystem;
        private BuffRollbackRestoreListener _buffRestoreListener;

        private bool _mounted;
        private bool _catchUpBlockedLogged;
        private Coroutine _mountCoroutine;

        private void Start()
        {
            if (!_enable) return;
            if (TryMount(TimeSimulator.Instance, out _))
                return;

            _mountCoroutine = StartCoroutine(InitCoro());
        }

        private IEnumerator InitCoro()
        {
            yield return null;

            _mountCoroutine = null;

            if (_mounted)
                yield break;

            var timeSim = TimeSimulator.Instance;
            if (!TryMount(timeSim, out string failureReason))
            {
                Debug.LogWarning(
                    $"[RollbackBootstrap] Mount skipped: {failureReason}");
            }
        }

        internal bool TryMount(TimeSimulator timeSim)
        {
            return TryMount(timeSim, out _);
        }

        internal bool TryMount(TimeSimulator timeSim, out string failureReason)
        {
            failureReason = string.Empty;

            if (!_enable)
            {
                failureReason = "RollbackBootstrap is disabled by configuration.";
                return false;
            }

            if (this == null || !isActiveAndEnabled)
            {
                failureReason = "RollbackBootstrap component is disabled.";
                return false;
            }

            if (_mounted)
            {
                failureReason = AlreadyMountedMessage;
                return true;
            }

            if (timeSim == null)
            {
                failureReason = "TimeSimulator.Instance missing.";
                return false;
            }

            World world = timeSim.DebugWorld;
            SimulateRunner runner = timeSim.DebugRunner;
            if (world == null || runner == null)
            {
                failureReason = "World or Runner not found on TimeSimulator.";
                return false;
            }

            if (runner.IsTicking || runner.FrameCount > 0)
            {
                failureReason = "Runner already advanced; late rollback mount is not allowed.";
                return false;
            }

            if (!TryCreateFrameCommandReplayBinding(
                    timeSim,
                    out _frameCommandReplayBinding,
                    out string frameCommandBindingFailure))
            {
                failureReason = frameCommandBindingFailure;
                return false;
            }

            _world = world;
            _runner = runner;
            _adapter = FindObjectOfType<UnityInputAdapter>();
            if (_adapter == null)
                Debug.LogWarning("[RollbackBootstrap] UnityInputAdapter not found — input will fall back to hardcoded keys.");

            Mount();
            failureReason = string.Empty;
            return true;
        }

        private void Mount()
        {
            _inputApplier = new PlayerSnapshotInputApplier();

            _rollbackAdapter = new WorldRollbackAdapter<PlayerInputSnapshot>(_world, _world, _inputApplier, null);
            _rollbackAdapter.SetFrameCommandReplayBinding(_frameCommandReplayBinding);
            AttachBuffRestoreListener();

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
        private static bool TryCreateFrameCommandReplayBinding(
            TimeSimulator timeSim,
            out RollbackFrameCommandReplayBinding binding,
            out string failureReason)
        {
            binding = default(RollbackFrameCommandReplayBinding);

            if (timeSim == null)
            {
                failureReason = "TimeSimulator is null.";
                return false;
            }

            SimulationFrameCommandBuffer commandBuffer = timeSim.DebugFrameCommandBuffer;
            SimulationFrameCommandApplier commandApplier = timeSim.DebugFrameCommandApplier;
            if (commandBuffer == null || commandApplier == null)
            {
                failureReason = "TimeSimulator has no SimulationFrameCommandBuffer/SimulationFrameCommandApplier. SimulationInitializer must create and inject the real pipeline before RollbackBootstrap mounts.";
                return false;
            }

            if (!ReferenceEquals(commandBuffer, commandApplier.CommandBuffer))
            {
                failureReason = "TimeSimulator DebugFrameCommandBuffer is not the same instance used by DebugFrameCommandApplier.";
                return false;
            }

            binding = new RollbackFrameCommandReplayBinding(commandBuffer, commandApplier);
            failureReason = string.Empty;
            return true;
        }

        /// <summary>Runs rollback input preparation before the normal runner tick.</summary>
        private void OnBeforeTick(SimulationContext ctx)
        {
            if (!_mounted || _coordinator == null)
                return;

            var input = CollectInput(ctx.frameNumber);
            RollbackStepResult result = _coordinator.TryStep(ctx.frameNumber, input);
            if (result.Succeeded)
                return;

            throw new InvalidOperationException(
                $"[RollbackBootstrap] TryStep failed before World.Tick. " +
                $"frame={ctx.frameNumber}, kind={result.FailureKind}, message={result.Message}");
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

        private void OnDisable()
        {
            Unmount();
        }

        private void OnDestroy()
        {
            Unmount();
        }
        internal void BindBuffSystem(BuffSystemCore buffSystem)
        {
            if (ReferenceEquals(_boundBuffSystem, buffSystem))
            {
                AttachBuffRestoreListener();
                return;
            }

            DetachBuffRestoreListener();
            _boundBuffSystem = buffSystem;
            AttachBuffRestoreListener();
        }

        private void AttachBuffRestoreListener()
        {
            if (_boundBuffSystem == null || _rollbackAdapter == null || _buffRestoreListener != null) return;
            _buffRestoreListener = new BuffRollbackRestoreListener(_boundBuffSystem);
            _rollbackAdapter.AddRollbackRestoreListener(_buffRestoreListener);
        }

        private void DetachBuffRestoreListener()
        {
            if (_rollbackAdapter != null && _buffRestoreListener != null)
                _rollbackAdapter.RemoveRollbackRestoreListener(_buffRestoreListener);
            _buffRestoreListener = null;
        }
        private void Unmount()
        {
            if (_mountCoroutine != null)
            {
                StopCoroutine(_mountCoroutine);
                _mountCoroutine = null;
            }

            if (!_mounted) return;

            if (_runner != null)
            {
                _runner.BeforeTick -= OnBeforeTick;
                _runner.AfterTick -= OnAfterTick;
            }

            DetachBuffRestoreListener();

            _mounted = false;
            _coordinator = null;
            _rollbackAdapter = null;
            _inputApplier = null;
            _adapter = null;
            _frameCommandReplayBinding = default;
        }

        //--------------------------------
        // Public API
        //--------------------------------

        public RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator => _coordinator;
        public PlayerSnapshotInputApplier InputApplier => _inputApplier;
        public World World => _world;

        /// <summary>
        /// 服务端已确认的帧号。收到服务端确认消息时调用，释放该帧前所有缓存数据。
        /// </summary>
        public void ConfirmFrame(int frame)
        {
            if (_coordinator == null) return;
            _coordinator.ConfirmFrame(frame);
        }

        /// <summary>
        /// 设置服务端期望客户端到达的帧号。Update 中会自动检测并加速追帧。
        /// </summary>
        public void SetExpectedFrame(int frame)
        {
            _expectedFrame = frame;
        }

        /// <summary>
        /// 当前服务端期望帧号。
        /// </summary>
        public int ExpectedFrame => _expectedFrame;

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

        //--------------------------------
        // Catch-up
        //--------------------------------

        private void Update()
        {
            if (!_mounted || _coordinator == null)
                return;

            // 加速追帧：若落后服务端，在一个 Unity 帧内执行多次逻辑 Tick
            int framesBehind = _expectedFrame - _coordinator.CurrentFrame;
            if (framesBehind > 1)
            {
                if (_catchUpBlockedLogged)
                    return;

                _catchUpBlockedLogged = true;
                Debug.LogWarning(
                    "[RollbackBootstrap] Catch-up is disabled in RBS-Fix-A1-A6 logic-only closure. " +
                    "RollbackBootstrap.Update will not call TickMultiple because TimeSimulator/Runner remains the unique normal-frame driver.");
            }
        }
    }
}
