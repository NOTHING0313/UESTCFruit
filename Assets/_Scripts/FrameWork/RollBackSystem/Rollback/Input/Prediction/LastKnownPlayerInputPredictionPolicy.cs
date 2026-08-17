using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 缺失输入时保持最近连续状态，同时清空一次性输入。
    /// </summary>
    public sealed class LastKnownPlayerInputPredictionPolicy : IPlayerInputPredictionPolicy
    {
        public PlayerInputSnapshot Predict(int frameNumber, int playerID, bool hasLastKnownInput, in PlayerInputSnapshot lastKnownInput)
        {
            if (!hasLastKnownInput) return new PlayerInputSnapshot(frameNumber, playerID);

            return new PlayerInputSnapshot(frameNumber, playerID)
            {
                moveX = lastKnownInput.moveX,
                moveY = lastKnownInput.moveY,
                mouseX = lastKnownInput.mouseX,
                mouseY = lastKnownInput.mouseY,
                heldButtons = lastKnownInput.heldButtons
            };
        }
    }
}