using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 将部分真实玩家输入补全为可交给 RollbackCoordinator 的完整 FrameInputSet。
    /// </summary>
    public sealed class FrameInputAssembler
    {
        private readonly IPlayerInputPredictionPolicy _predictionPolicy;
        private readonly PlayerInputSnapshotComparer _comparer = new();
        private readonly List<int> _registeredPlayerIDs = new();
        private readonly Dictionary<int, PlayerInputSnapshot> _lastKnownInputs = new();
        private readonly List<int> _receivedPlayerIDsScratch = new();
        private bool _playerOrderDirty;

        public int PlayerCount => _registeredPlayerIDs.Count;

        public FrameInputAssembler(IPlayerInputPredictionPolicy predictionPolicy)
            => _predictionPolicy = predictionPolicy ?? throw new ArgumentNullException(nameof(predictionPolicy));

        /// <summary>注册参与当前帧同步会话的玩家。</summary>
        public bool RegisterPlayer(int playerID)
        {
            if (_registeredPlayerIDs.Contains(playerID)) return false;
            _registeredPlayerIDs.Add(playerID);
            _playerOrderDirty = true;
            return true;
        }

        /// <summary>注销玩家并移除其预测历史。</summary>
        public bool UnregisterPlayer(int playerID)
        {
            if (!_registeredPlayerIDs.Remove(playerID)) return false;
            _lastKnownInputs.Remove(playerID);
            _playerOrderDirty = true;
            return true;
        }

        /// <summary>
        /// 记录已经确认到达的真实输入。较旧的延迟输入不会覆盖更新的预测历史。
        /// </summary>
        public void ObserveAuthoritativeInput(in PlayerInputSnapshot input)
        {
            if (!_registeredPlayerIDs.Contains(input.playerID))
                throw new InvalidOperationException($"Unregistered Authoritative Player Input: Frame={input.frameNumber}, PlayerID={input.playerID}");

            if (!_lastKnownInputs.TryGetValue(input.playerID, out PlayerInputSnapshot current))
            {
                _lastKnownInputs[input.playerID] = input;
                return;
            }

            if (input.frameNumber < current.frameNumber) return;

            if (input.frameNumber == current.frameNumber)
            {
                if (!_comparer.IsEqual(current, input))
                    throw new InvalidOperationException($"Conflicting Authoritative Player Input: Frame={input.frameNumber}, PlayerID={input.playerID}");

                return;
            }

            _lastKnownInputs[input.playerID] = input;
        }

        /// <summary>
        /// 将当前帧已到达的真实输入和预测输入组合成完整 FrameInputSet。
        /// </summary>
        public FrameInputAssemblyResult Assemble(FrameInputAccumulator accumulator)
        {
            if (accumulator == null) throw new ArgumentNullException(nameof(accumulator));
            if (_registeredPlayerIDs.Count == 0) throw new InvalidOperationException("Frame Input Assembler Has No Registered Players");

            RebuildPlayerOrder();
            ValidateReceivedPlayers(accumulator);

            var inputs = new PlayerInputSnapshot[_registeredPlayerIDs.Count];
            int[] predictedPlayerIDs = null;
            int predictedCount = 0;

            for (int i = 0; i < _registeredPlayerIDs.Count; i++)
            {
                int playerID = _registeredPlayerIDs[i];

                if (accumulator.TryGetInput(playerID, out PlayerInputSnapshot realInput))
                {
                    ObserveAuthoritativeInput(in realInput);
                    inputs[i] = realInput;
                    continue;
                }

                bool hasLastKnown = _lastKnownInputs.TryGetValue(playerID, out PlayerInputSnapshot lastKnown);
                PlayerInputSnapshot predicted = _predictionPolicy.Predict(accumulator.FrameNumber, playerID, hasLastKnown, in lastKnown);

                if (predicted.frameNumber != accumulator.FrameNumber)
                    throw new InvalidOperationException($"Prediction Frame Mismatch: Expected={accumulator.FrameNumber}, PlayerID={playerID}, Actual={predicted.frameNumber}");

                if (predicted.playerID != playerID)
                    throw new InvalidOperationException($"Prediction Player Mismatch: Frame={accumulator.FrameNumber}, Expected={playerID}, Actual={predicted.playerID}");

                inputs[i] = predicted;
                predictedPlayerIDs ??= new int[_registeredPlayerIDs.Count];
                predictedPlayerIDs[predictedCount++] = playerID;
            }

            return new FrameInputAssemblyResult(
                new FrameInputSet(accumulator.FrameNumber, inputs),
                predictedPlayerIDs,
                predictedCount);
        }

        private void RebuildPlayerOrder()
        {
            if (!_playerOrderDirty) return;
            _registeredPlayerIDs.Sort();
            _playerOrderDirty = false;
        }

        private void ValidateReceivedPlayers(FrameInputAccumulator accumulator)
        {
            accumulator.FillPlayerIDs(_receivedPlayerIDsScratch);

            for (int i = 0; i < _receivedPlayerIDsScratch.Count; i++)
            {
                int playerID = _receivedPlayerIDsScratch[i];

                if (_registeredPlayerIDs.BinarySearch(playerID) >= 0) continue;

                throw new InvalidOperationException(
                    $"Unexpected Player Input: Frame={accumulator.FrameNumber}, PlayerID={playerID}");
            }
        }
    }

    /// <summary>
    /// FrameInputAssembler 的单帧组装结果。
    /// </summary>
    public readonly struct FrameInputAssemblyResult
    {
        private readonly int[] _predictedPlayerIDs;

        public FrameInputSet InputSet { get; }
        public int PredictedCount { get; }
        public bool HasPrediction => PredictedCount > 0;

        internal FrameInputAssemblyResult(FrameInputSet inputSet, int[] predictedPlayerIDs, int predictedCount)
        {
            InputSet = inputSet;
            _predictedPlayerIDs = predictedPlayerIDs;
            PredictedCount = predictedCount;
        }

        /// <summary>获取本帧第 index 个使用预测输入的 PlayerID。</summary>
        public int GetPredictedPlayerIDAt(int index)
        {
            if ((uint)index >= (uint)PredictedCount) throw new ArgumentOutOfRangeException(nameof(index));
            return _predictedPlayerIDs[index];
        }

        /// <summary>判断指定玩家本帧是否使用预测输入。</summary>
        public bool IsPredicted(int playerID)
        {
            for (int i = 0; i < PredictedCount; i++)
                if (_predictedPlayerIDs[i] == playerID) return true;

            return false;
        }
    }
}