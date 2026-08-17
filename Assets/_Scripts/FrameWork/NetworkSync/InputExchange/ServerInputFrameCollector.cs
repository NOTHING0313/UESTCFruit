using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 在服务器端按逻辑帧收集全部已注册玩家输入，并在输入到齐后生成权威 FrameInputSet。
    /// </summary>
    public sealed class ServerInputFrameCollector
    {
        private readonly List<int> _playerIDs = new();
        private readonly Dictionary<int, FrameInputAccumulator> _pendingFrames = new();
        private readonly Dictionary<int, FrameInputSet> _completedFrames = new();
        private readonly Queue<int> _completedOrder = new();
        private readonly PlayerInputSnapshotComparer _inputComparer = new();
        private readonly int _completedRetention;
        private bool _registrationLocked;

        public int PlayerCount => _playerIDs.Count;
        public int PendingFrameCount => _pendingFrames.Count;
        public int CompletedFrameCount => _completedFrames.Count;

        public ServerInputFrameCollector(int completedRetention = 512)
        {
            if (completedRetention <= 0) throw new ArgumentOutOfRangeException(nameof(completedRetention));
            _completedRetention = completedRetention;
        }

        /// <summary>注册当前同步会话中的玩家。开始接收输入后禁止继续修改成员集合。</summary>
        public bool RegisterPlayer(int playerID)
        {
            if (playerID <= 0) throw new ArgumentOutOfRangeException(nameof(playerID));
            if (_registrationLocked) throw new InvalidOperationException("Server Input Frame Collector Registration Is Locked");
            if (_playerIDs.BinarySearch(playerID) >= 0) return false;

            _playerIDs.Add(playerID);
            _playerIDs.Sort();
            return true;
        }

        /// <summary>
        /// 添加玩家真实输入。返回 true 表示本帧全部玩家输入已经到齐并产生完整权威 FrameInputSet。
        /// </summary>
        public bool TryAddInput(in PlayerInputSnapshot input, out FrameInputSet completedFrame)
        {
            completedFrame = default;
            _registrationLocked = true;

            if (_playerIDs.Count == 0) throw new InvalidOperationException("Server Input Frame Collector Has No Registered Players");
            if (_playerIDs.BinarySearch(input.playerID) < 0)
                throw new InvalidOperationException($"Server Input Unregistered Player: Frame={input.frameNumber}, PlayerID={input.playerID}");

            if (_completedFrames.TryGetValue(input.frameNumber, out FrameInputSet existingFrame))
            {
                if (!existingFrame.TryGetInput(input.playerID, out PlayerInputSnapshot existingInput))
                    throw new InvalidOperationException($"Completed Authority Missing Player: Frame={input.frameNumber}, PlayerID={input.playerID}");

                if (_inputComparer.IsEqual(existingInput, input)) return false;

                throw new InvalidOperationException($"Conflicting Completed Player Input: Frame={input.frameNumber}, PlayerID={input.playerID}");
            }

            if (!_pendingFrames.TryGetValue(input.frameNumber, out FrameInputAccumulator accumulator))
            {
                accumulator = new FrameInputAccumulator(input.frameNumber);
                _pendingFrames.Add(input.frameNumber, accumulator);
            }

            accumulator.TryAddInput(in input);

            if (accumulator.Count != _playerIDs.Count) return false;

            var inputs = new PlayerInputSnapshot[_playerIDs.Count];

            for (int i = 0; i < _playerIDs.Count; i++)
            {
                int playerID = _playerIDs[i];

                if (!accumulator.TryGetInput(playerID, out PlayerInputSnapshot playerInput))
                    throw new InvalidOperationException($"Complete Frame Missing Player: Frame={input.frameNumber}, PlayerID={playerID}");

                inputs[i] = playerInput;
            }

            completedFrame = new FrameInputSet(input.frameNumber, inputs);

            _pendingFrames.Remove(input.frameNumber);
            _completedFrames.Add(input.frameNumber, completedFrame);
            _completedOrder.Enqueue(input.frameNumber);

            while (_completedOrder.Count > _completedRetention)
            {
                int expiredFrame = _completedOrder.Dequeue();
                _completedFrames.Remove(expiredFrame);
            }

            return true;
        }
    }
}