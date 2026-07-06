/*
 * 文件说明：IRollbackSimulation 定义回滚模拟协调器接口。
 * 设计约束：实现者负责输入预测、快照保存、状态回滚与历史帧重模拟。
 */

using FrameWork.RollBackSystem;

namespace Simulation.Contracts
{
    public interface IRollbackSimulation<TInput>
    {
        int CurrentFrame { get; }

        void Step(TInput input);

        RollbackStepResult TryStep(int frame, TInput input);

        void SaveSnapshot();

        void ReceiveAuthoritativeInput(
            int frame,
            TInput input);

        bool RollbackTo(int frame);

        RollbackRestoreResult TryRollbackTo(int frame);

        void ResimulateTo(int targetFrame);

        RollbackResimulateResult TryResimulateTo(int targetFrame);
    }
}
