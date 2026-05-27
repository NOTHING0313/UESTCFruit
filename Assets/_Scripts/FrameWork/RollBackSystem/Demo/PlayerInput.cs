/*
 * 文件说明：PlayerInput 表示单帧玩家输入数据。
 * 设计约束：输入数据必须可缓存、可回放、可序列化，用于回滚与重模拟。
 */

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 第一版玩家输入。
    /// </summary>
    public readonly struct PlayerInput
    {
        public readonly int Horizontal;

        public readonly int Vertical;

        public readonly bool CastSkill;

        public PlayerInput(
            int horizontal,
            int vertical,
            bool castSkill)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            CastSkill = castSkill;
        }
    }
}