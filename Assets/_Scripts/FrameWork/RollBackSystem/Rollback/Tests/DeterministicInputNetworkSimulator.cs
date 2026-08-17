using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 确定性输入网络模拟器，仅用于测试延迟、乱序与重复包。
    /// </summary>
    public sealed class DeterministicInputNetworkSimulator
    {
        private readonly int _maxDelayFrames;
        private readonly int _duplicateChancePercent;
        private readonly Dictionary<int, List<FakeInputPacket>> _deliveries = new();
        private readonly Dictionary<int, int> _latestDeliveredFrameByPlayer = new();
        private uint _randomState;

        public int UniquePacketCount { get; private set; }
        public int DelayedUniquePacketCount { get; private set; }
        public int DuplicatePacketCount { get; private set; }
        public int DeliveredPacketCount { get; private set; }
        public int DeliveredDuplicatePacketCount { get; private set; }
        public int OutOfOrderUniquePacketCount { get; private set; }
        public int MaxObservedUniqueDelayFrames { get; private set; }
        public int LastScheduledArrivalFrame { get; private set; }
        public bool HasPendingPackets => _deliveries.Count > 0;

        public DeterministicInputNetworkSimulator(uint seed, int maxDelayFrames, int duplicateChancePercent)
        {
            if (maxDelayFrames < 0) throw new ArgumentOutOfRangeException(nameof(maxDelayFrames));
            if (duplicateChancePercent < 0 || duplicateChancePercent > 100) throw new ArgumentOutOfRangeException(nameof(duplicateChancePercent));

            _randomState = seed;
            _maxDelayFrames = maxDelayFrames;
            _duplicateChancePercent = duplicateChancePercent;
        }

        /// <summary>把一个权威 FrameInputSet 拆成独立玩家输入包并安排未来到达时间。</summary>
        public void ScheduleFrame(FrameInputSet inputSet)
        {
            if (!inputSet.IsCreated) throw new InvalidOperationException("Fake Network Input Set Is Not Created");

            for (int i = 0; i < inputSet.Count; i++)
            {
                PlayerInputSnapshot input = inputSet.GetInputAt(i);

                if (input.frameNumber != inputSet.frameNumber)
                    throw new InvalidOperationException($"Fake Network Frame Mismatch: SetFrame={inputSet.frameNumber}, PlayerID={input.playerID}, InputFrame={input.frameNumber}");

                int delay = NextRange(0, _maxDelayFrames);
                int arrivalFrame = input.frameNumber + delay;

                SchedulePacket(new FakeInputPacket(input, input.frameNumber, arrivalFrame, false, delay));

                UniquePacketCount++;
                if (delay > 0) DelayedUniquePacketCount++;
                if (delay > MaxObservedUniqueDelayFrames) MaxObservedUniqueDelayFrames = delay;

                if (NextRange(0, 99) >= _duplicateChancePercent) continue;

                int duplicateExtraDelay = NextRange(0, _maxDelayFrames);
                int duplicateArrivalFrame = arrivalFrame + duplicateExtraDelay;

                SchedulePacket(new FakeInputPacket(input, input.frameNumber, duplicateArrivalFrame, true, delay + duplicateExtraDelay));
                DuplicatePacketCount++;
            }
        }

        /// <summary>取出指定网络帧到达的全部输入包。</summary>
        public void Deliver(int networkFrame, List<FakeInputPacket> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();

            if (!_deliveries.TryGetValue(networkFrame, out List<FakeInputPacket> packets)) return;

            _deliveries.Remove(networkFrame);
            output.AddRange(packets);

            for (int i = 0; i < packets.Count; i++)
            {
                FakeInputPacket packet = packets[i];
                DeliveredPacketCount++;

                if (packet.IsDuplicate)
                {
                    DeliveredDuplicatePacketCount++;
                    continue;
                }

                int playerID = packet.Input.playerID;
                int inputFrame = packet.Input.frameNumber;

                if (_latestDeliveredFrameByPlayer.TryGetValue(playerID, out int latest))
                {
                    if (inputFrame < latest) OutOfOrderUniquePacketCount++;
                    else if (inputFrame > latest) _latestDeliveredFrameByPlayer[playerID] = inputFrame;
                }
                else _latestDeliveredFrameByPlayer.Add(playerID, inputFrame);
            }
        }

        private void SchedulePacket(FakeInputPacket packet)
        {
            if (!_deliveries.TryGetValue(packet.ArrivalFrame, out List<FakeInputPacket> packets))
            {
                packets = new List<FakeInputPacket>();
                _deliveries.Add(packet.ArrivalFrame, packets);
            }

            packets.Add(packet);
            if (packet.ArrivalFrame > LastScheduledArrivalFrame) LastScheduledArrivalFrame = packet.ArrivalFrame;
        }

        private int NextRange(int minInclusive, int maxInclusive)
        {
            _randomState = NextRandom(_randomState);
            return minInclusive + (int)(_randomState % (uint)(maxInclusive - minInclusive + 1));
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }
    }

    /// <summary>Fake Network 中单个玩家输入包。</summary>
    public readonly struct FakeInputPacket
    {
        public readonly PlayerInputSnapshot Input;
        public readonly int SendFrame;
        public readonly int ArrivalFrame;
        public readonly bool IsDuplicate;
        public readonly int TotalDelayFrames;

        public FakeInputPacket(PlayerInputSnapshot input, int sendFrame, int arrivalFrame, bool isDuplicate, int totalDelayFrames)
        {
            Input = input;
            SendFrame = sendFrame;
            ArrivalFrame = arrivalFrame;
            IsDuplicate = isDuplicate;
            TotalDelayFrames = totalDelayFrames;
        }
    }
}