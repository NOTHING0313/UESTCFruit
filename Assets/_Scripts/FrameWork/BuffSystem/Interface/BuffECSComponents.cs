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
