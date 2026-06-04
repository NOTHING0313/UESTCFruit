/*
 * RollbackBootstrap — RollBackSystem 正式入口。
 *
 * 只做 RollBackSystem 自己的事：
 *   1. 通过 TimeSimulator.Instance 拿到 World/Runner
 *   2. 创建 Rollback 管线 (inputBuffer → coordinator → adapter)
 *   3. Hook Runner.BeforeTick，coordinator.Step 写输入
 *
 * 不包含属于业务层 / 演示层的逻辑：
 *   - 不注入 System（应由 SimulationInitializer 负责）
 *   - 不创建 Entity（应由业务层负责）
 *   - 不创建 View（应由 ViewManager 负责）
 */

using ECSFrameWork;
using UnityEngine;
using System.Collections;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _enable = true;
        [SerializeField] private int _snapshotRingCapacity = 120;

        private World _world;
        private SimulateRunner _runner;

        private RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> _coordinator;
        private WorldRollbackAdapter<PlayerInputSnapshot> _adapter;
        private PlayerSnapshotInputApplier _inputApplier;

        private bool _mounted;

        private IEnumerator Start()
        {
            if (!_enable) yield break;

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

            Mount();
        }

        private void Mount()
        {
            _inputApplier = new PlayerSnapshotInputApplier();

            var cmdBuffer = new SimulationFrameCommandBuffer();
            var cmdApplier = new SimulationFrameCommandApplier(_world, cmdBuffer);
            var frameSource = new FrameCommandSourceAdapter(cmdApplier);

            _adapter = new WorldRollbackAdapter<PlayerInputSnapshot>(
                _world, _world, _inputApplier, frameSource);

            var snapBuf = new SnapshotRingBuffer<EcsWorldSnapshot>(_snapshotRingCapacity);

            _coordinator = new RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot>(
                new InputBuffer<PlayerInputSnapshot>(),
                new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                snapBuf, _adapter,
                new PlayerInputSnapshotComparer(),
                new ChecksumBuffer(), new AuthoritativeChecksumBuffer());

            _coordinator.SaveSnapshot();

            _runner.BeforeTick += OnBeforeTick;
            _mounted = true;

            Debug.Log("[RollbackBootstrap] Mounted (core only).");
        }

        private void OnBeforeTick(SimulationContext ctx)
        {
            if (!_mounted || _coordinator == null) return;

            var snapshot = CollectInput(ctx.frameNumber);
            _coordinator.Step(snapshot);

            if (_coordinator.CurrentFrame % 10 == 0)
                _coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CollectInput(int frameNumber)
        {
            float h = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            return new PlayerInputSnapshot { moveX = h, moveY = 0f };
        }

        private void OnDestroy()
        {
            if (_mounted && _runner != null)
                _runner.BeforeTick -= OnBeforeTick;
        }

        //--------------------------------
        // Public API
        //--------------------------------

        public RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator => _coordinator;
        public PlayerSnapshotInputApplier InputApplier => _inputApplier;
        public World World => _world;

        public void ReceiveRemoteInput(int frame, PlayerInputSnapshot input)
        {
            if (_coordinator == null) return;
            _coordinator.ReceiveAuthoritativeInput(frame, input);
            _runner?.SetFrameCount(_coordinator.CurrentFrame);
        }
    }
}
