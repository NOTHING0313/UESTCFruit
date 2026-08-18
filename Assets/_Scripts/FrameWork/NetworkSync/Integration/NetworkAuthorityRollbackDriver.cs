using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 将服务器权威 FrameInputSet 接入预测历史与 RollbackCoordinator。
    /// </summary>
    public sealed class NetworkAuthorityRollbackDriver
    {
        private readonly FrameInputAssembler _assembler;
        private readonly RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> _coordinator;
        private int _highestAuthorityFrame;

        public int AppliedAuthorityCount { get; private set; }
        public int OutOfOrderAuthorityCount { get; private set; }
        public int LastAuthorityFrame { get; private set; }

        public NetworkAuthorityRollbackDriver(FrameInputAssembler assembler, RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> coordinator)
        {
            _assembler = assembler ?? throw new ArgumentNullException(nameof(assembler));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        /// <summary>应用一个已经由网络客户端完成 Session/Protocol 校验的服务器权威帧。</summary>
        public void Apply(in ServerAuthorityFramePacket packet)
        {
            FrameInputSet inputSet = packet.InputSet;
            if (!inputSet.IsCreated) throw new InvalidOperationException("Network Authority Input Set Is Not Created");

            int currentFrame = _coordinator.CurrentFrame;

            for (int i = 0; i < inputSet.Count; i++)
            {
                PlayerInputSnapshot input = inputSet.GetInputAt(i);
                _assembler.ObserveAuthoritativeInput(in input);
            }

            if (_highestAuthorityFrame > 0 && inputSet.frameNumber < _highestAuthorityFrame) OutOfOrderAuthorityCount++;
            if (inputSet.frameNumber > _highestAuthorityFrame) _highestAuthorityFrame = inputSet.frameNumber;

            _coordinator.ReceiveAuthoritativeInput(inputSet.frameNumber, inputSet);

            if (inputSet.frameNumber <= currentFrame && _coordinator.CurrentFrame != currentFrame)
                throw new InvalidOperationException($"Network Authority Rollback CurrentFrame Error: AuthorityFrame={inputSet.frameNumber}, Expected={currentFrame}, Actual={_coordinator.CurrentFrame}");

            LastAuthorityFrame = inputSet.frameNumber;
            AppliedAuthorityCount++;
        }
    }
}