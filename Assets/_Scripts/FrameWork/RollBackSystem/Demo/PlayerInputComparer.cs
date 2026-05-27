/*
 * 文件说明：
 * PlayerInputComparer 用于比较两个 PlayerInput 是否一致。
 *
 * 设计目标：
 * 1. 为回滚系统提供输入一致性校验。
 * 2. 检测本地预测与服务器输入差异。
 * 3. 输入不同则触发 Rollback。
 *
 * 使用场景：
 * - RollbackCoordinator
 * - RollbackInputResolver
 * - 输入预测校验
 */

namespace FrameWork.RollBackSystem
{
    public sealed class PlayerInputComparer
        : Interfaces.IInputComparer<PlayerInput>
    {
        /// <summary>
        /// 判断两个玩家输入是否一致。
        /// </summary>
        public bool IsEqual(
            PlayerInput a,
            PlayerInput b)
        {
            return
                a.Horizontal == b.Horizontal
                && a.Vertical == b.Vertical
                && a.CastSkill == b.CastSkill;
        }
    }
}