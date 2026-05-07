using Contracts;   // PlayerInput

namespace Drivers
{
    /// <summary>
    /// 仿真驱动器抽象接口（4号提供，内部使用）。
    /// 用于切换实时模式 / 回滚模式，上层 Bootstrap 只依赖此接口。
    /// </summary>
    public interface ISimulationDriver
    {
        int CurrentFrame { get; }
        void Step(in PlayerInput input);
    }
}