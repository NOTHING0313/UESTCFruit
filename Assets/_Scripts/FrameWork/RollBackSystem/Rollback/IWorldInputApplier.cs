/*
 * 文件说明：
 * IWorldInputApplier 用于把输入数据写入 ECS World。
 *
 * 设计目标：
 * 1. 解耦输入结构与 ECS 组件实现。
 * 2. 允许不同输入类型拥有不同应用逻辑。
 * 3. 避免 RollbackCoordinator 直接依赖 ECS 组件。
 * 4. 保证输入写入流程可替换、可测试。
 *
 * 使用场景：
 * - WorldRollbackAdapter
 * - 输入同步
 * - 回滚重模拟
 * - ECS 输入组件更新
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public interface IWorldInputApplier<TInput>
    {
        /// <summary>
        /// 把输入数据写入 ECS World。
        /// </summary>
        void Apply(
            World world,
            TInput input);
    }
}