using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 定义玩家当前帧真实输入缺失时的预测策略。
    /// </summary>
    public interface IPlayerInputPredictionPolicy
    {
        /// <summary>根据玩家最近真实输入生成当前帧预测输入。</summary>
        PlayerInputSnapshot Predict(int frameNumber, int playerID, bool hasLastKnownInput, in PlayerInputSnapshot lastKnownInput);
    }
}