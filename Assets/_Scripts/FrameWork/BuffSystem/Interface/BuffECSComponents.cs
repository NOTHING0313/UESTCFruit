using ECSFrameWork;

namespace BuffSystem
{
    /// <summary>
    /// 保存单个运行中 Buff 的 ECS Component；普通 Buff 可保存多层，并行 Buff 每层一个实体。
    /// </summary>
    public struct BuffRuntimeComponent : IComponentData
    {
        public Entity target;
        public Entity source;
        public int configId;
        public int runtimeHandle;
        public int stack;
        public int durationFrames;
        public int remainingFrames;
        public int tickIntervalFrames;
        public int elapsedFrames;
        public int ticks;
        public int maxStack;
        public int priority;
        public bool unlimited;
        public bool isForever;
        public BuffInstanceType buffType;

        public BuffRuntimeComponent(AddBuffCommand command, in BuffDefinition definition, int runtimeHandle, int stack)
        {
            target = command.Target;
            source = command.Source;
            configId = command.ConfigId;
            this.runtimeHandle = runtimeHandle;
            this.stack = stack > 0 ? stack : 1;
            durationFrames = definition.DurationFrames;
            remainingFrames = definition.IsForever ? 0 : definition.DurationFrames;
            tickIntervalFrames = definition.TickIntervalFrames;
            elapsedFrames = 0;
            ticks = 0;
            maxStack = definition.MaxStack;
            priority = definition.Priority;
            unlimited = definition.Unlimited;
            isForever = definition.IsForever;
            buffType = definition.BuffType;
        }
    }

    /// <summary>
    /// 添加 Buff 的单帧 ECS 请求组件；BuffSystemCore 消费后销毁请求实体。
    /// </summary>
    /// <summary>
    /// Phase 3B 预留：压缩并行 Buff 的单层固定帧运行时数据，当前尚未接入运行时主流程。
    /// </summary>
    public struct CompressedParallelBuffLayer
    {
        public int layerId;
        public int expireFrame;
        public int elapsedFrames;
        public int ticks;
        public int layerRuntimeHandle;
    }

    /// <summary>
    /// Phase 3B 预留：压缩并行 Buff 层的固定容量值类型容器；不使用数组、List 或 Dictionary 作为回滚真状态。
    /// </summary>
    public struct CompressedParallelBuffLayerBuffer
    {
        public const int Capacity = 16;

        private CompressedParallelBuffLayer _layer0;
        private CompressedParallelBuffLayer _layer1;
        private CompressedParallelBuffLayer _layer2;
        private CompressedParallelBuffLayer _layer3;
        private CompressedParallelBuffLayer _layer4;
        private CompressedParallelBuffLayer _layer5;
        private CompressedParallelBuffLayer _layer6;
        private CompressedParallelBuffLayer _layer7;
        private CompressedParallelBuffLayer _layer8;
        private CompressedParallelBuffLayer _layer9;
        private CompressedParallelBuffLayer _layer10;
        private CompressedParallelBuffLayer _layer11;
        private CompressedParallelBuffLayer _layer12;
        private CompressedParallelBuffLayer _layer13;
        private CompressedParallelBuffLayer _layer14;
        private CompressedParallelBuffLayer _layer15;

        public void Clear()
        {
            this = default;
        }

        public CompressedParallelBuffLayer Get(int index)
        {
            switch (index)
            {
                case 0: return _layer0;
                case 1: return _layer1;
                case 2: return _layer2;
                case 3: return _layer3;
                case 4: return _layer4;
                case 5: return _layer5;
                case 6: return _layer6;
                case 7: return _layer7;
                case 8: return _layer8;
                case 9: return _layer9;
                case 10: return _layer10;
                case 11: return _layer11;
                case 12: return _layer12;
                case 13: return _layer13;
                case 14: return _layer14;
                case 15: return _layer15;
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public void Set(int index, in CompressedParallelBuffLayer layer)
        {
            switch (index)
            {
                case 0:
                    _layer0 = layer;
                    break;
                case 1:
                    _layer1 = layer;
                    break;
                case 2:
                    _layer2 = layer;
                    break;
                case 3:
                    _layer3 = layer;
                    break;
                case 4:
                    _layer4 = layer;
                    break;
                case 5:
                    _layer5 = layer;
                    break;
                case 6:
                    _layer6 = layer;
                    break;
                case 7:
                    _layer7 = layer;
                    break;
                case 8:
                    _layer8 = layer;
                    break;
                case 9:
                    _layer9 = layer;
                    break;
                case 10:
                    _layer10 = layer;
                    break;
                case 11:
                    _layer11 = layer;
                    break;
                case 12:
                    _layer12 = layer;
                    break;
                case 13:
                    _layer13 = layer;
                    break;
                case 14:
                    _layer14 = layer;
                    break;
                case 15:
                    _layer15 = layer;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public void RemoveAt(int index, int count)
        {
            ValidateActiveIndex(index, count);

            for (int i = index; i < count - 1; i++)
                Set(i, Get(i + 1));

            Set(count - 1, default);
        }

        public int FindEarliestIndex(int count)
        {
            ValidateActiveCount(count);

            if (count == 0)
                return -1;

            int bestIndex = 0;
            CompressedParallelBuffLayer best = Get(0);

            for (int i = 1; i < count; i++)
            {
                CompressedParallelBuffLayer current = Get(i);

                if (CompareEarliest(current, best) < 0)
                {
                    best = current;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public int FindLatestIndex(int count)
        {
            ValidateActiveCount(count);

            if (count == 0)
                return -1;

            int bestIndex = 0;
            CompressedParallelBuffLayer best = Get(0);

            for (int i = 1; i < count; i++)
            {
                CompressedParallelBuffLayer current = Get(i);

                if (CompareLatest(current, best) < 0)
                {
                    best = current;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public int FindExpiredEarliestIndex(int count, int frameNumber)
        {
            ValidateActiveCount(count);

            int bestIndex = -1;
            CompressedParallelBuffLayer best = default;

            for (int i = 0; i < count; i++)
            {
                CompressedParallelBuffLayer current = Get(i);

                if (current.expireFrame == int.MaxValue || current.expireFrame > frameNumber)
                    continue;

                if (bestIndex < 0 || CompareEarliest(current, best) < 0)
                {
                    best = current;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public bool AppendLayer(int count, in CompressedParallelBuffLayer layer)
        {
            if (count < 0 || count >= Capacity)
                return false;

            Set(count, in layer);
            return true;
        }

        public void RefreshLayer(int index, int count, int frameNumber, int durationFrames, bool isForever)
        {
            ValidateActiveIndex(index, count);

            CompressedParallelBuffLayer layer = Get(index);
            layer.expireFrame = isForever ? int.MaxValue : frameNumber + durationFrames;
            layer.elapsedFrames = 0;
            layer.ticks = 0;
            Set(index, in layer);
        }

        private static void ValidateActiveCount(int count)
        {
            if (count < 0 || count > Capacity)
                throw new System.ArgumentOutOfRangeException(nameof(count));
        }

        private static void ValidateActiveIndex(int index, int count)
        {
            ValidateActiveCount(count);

            if (index < 0 || index >= count)
                throw new System.ArgumentOutOfRangeException(nameof(index));
        }

        private static int CompareEarliest(CompressedParallelBuffLayer left, CompressedParallelBuffLayer right)
        {
            int expireCompare = left.expireFrame.CompareTo(right.expireFrame);

            if (expireCompare != 0)
                return expireCompare;

            int layerCompare = left.layerId.CompareTo(right.layerId);

            if (layerCompare != 0)
                return layerCompare;

            return left.layerRuntimeHandle.CompareTo(right.layerRuntimeHandle);
        }

        private static int CompareLatest(CompressedParallelBuffLayer left, CompressedParallelBuffLayer right)
        {
            int expireCompare = right.expireFrame.CompareTo(left.expireFrame);

            if (expireCompare != 0)
                return expireCompare;

            int layerCompare = left.layerId.CompareTo(right.layerId);

            if (layerCompare != 0)
                return layerCompare;

            return left.layerRuntimeHandle.CompareTo(right.layerRuntimeHandle);
        }
    }

    /// <summary>
    /// Phase 3B 预留：一个 Runtime Entity 聚合多个并行层的压缩运行时组件；当前不创建、不写入 World、不参与查询或 Tick。
    /// </summary>
    public struct CompressedParallelBuffRuntimeComponent : IComponentData
    {
        public Entity target;
        public Entity source;
        public int configId;
        public int compressedRuntimeHandle;
        public int priority;
        public int layerCount;
        public int nextLayerId;
        public CompressedParallelBuffLayerBuffer layers;
    }

    public struct AddBuffRequestComponent : IComponentData
    {
        public AddBuffCommand command;

        public AddBuffRequestComponent(AddBuffCommand command)
        {
            this.command = command;
        }
    }

    /// <summary>
    /// 移除 Buff 层数的单帧 ECS 请求组件；BuffSystemCore 消费后销毁请求实体。
    /// </summary>
    public struct RemoveBuffRequestComponent : IComponentData
    {
        public RemoveBuffCommand command;

        public RemoveBuffRequestComponent(RemoveBuffCommand command)
        {
            this.command = command;
        }
    }

    /// <summary>
    /// Buff 帧命令便捷入口，用于 Tick 外确定性调度。
    /// </summary>
    public static class BuffFrameCommandExtensions
    {
        public static void AddBuffAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, in AddBuffCommand command)
        {
            buffer.AddBuffAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick, in command);
        }

        public static void AddBuffAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, in AddBuffCommand command)
        {
            if (buffer == null || !command.IsValid)
                return;

            CreateEntityFrameCommand entityCommand = buffer.CreateEntityAtFrame(frameNumber, timing);
            entityCommand?.WithComponent(new AddBuffRequestComponent(command));
        }

        public static void RemoveBuffAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, in RemoveBuffCommand command)
        {
            buffer.RemoveBuffAtFrame(frameNumber, SimulationFrameCommandTiming.BeforeTick, in command);
        }

        public static void RemoveBuffAtFrame(this SimulationFrameCommandBuffer buffer, int frameNumber, SimulationFrameCommandTiming timing, in RemoveBuffCommand command)
        {
            if (buffer == null || !command.IsValid)
                return;

            CreateEntityFrameCommand entityCommand = buffer.CreateEntityAtFrame(frameNumber, timing);
            entityCommand?.WithComponent(new RemoveBuffRequestComponent(command));
        }
    }
}
