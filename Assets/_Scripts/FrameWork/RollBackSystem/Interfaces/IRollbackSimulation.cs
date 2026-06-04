/*
 * 文件说明：IRollbackSimulation 定义回滚模拟协调器接口。
 * 设计约束：实现者负责输入预测、快照保存、状态回滚与历史帧重模拟。
 */

namespace Simulation.Contracts
{
    public interface IRollbackSimulation<TInput>
    {
        int CurrentFrame { get; }

        void Step(TInput input);

        void SaveSnapshot();

        void ReceiveAuthoritativeInput(
            int frame,
            TInput input);

        bool RollbackTo(int frame);

        void ResimulateTo(int targetFrame);
    }
}