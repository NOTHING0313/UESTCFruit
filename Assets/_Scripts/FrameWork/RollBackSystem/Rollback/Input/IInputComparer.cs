/*
 * 文件说明：
 * IInputComparer 用于比较两个输入是否一致。
 *
 * 设计目标：
 * 1. 解耦输入比较逻辑。
 * 2. 支持不同输入结构的自定义比较。
 * 3. 用于预测输入与服务器输入校验。
 *
 * 使用场景：
 * - RollbackInputResolver
 * - RollbackCoordinator
 * - 输入预测误差检测
 */

namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IInputComparer<TInput>
    {
        /// <summary>
        /// 判断两个输入是否一致。
        /// </summary>
        bool IsEqual(
            TInput a,
            TInput b);
    }
}