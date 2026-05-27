/*
 * 文件说明：ISimulation 定义最基础的逻辑模拟接口。
 * 设计约束：实现者应保证输入驱动逻辑更新，不依赖外部实时状态。
 */

namespace FrameWork.RollBackSystem.Interfaces
{
    public interface ISimulation<TInput>
    {
        void Simulate(in TInput input);
    }
}