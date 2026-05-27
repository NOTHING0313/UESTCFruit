/*
 * 文件说明：
 * RollbackRunnerAdapter 用于适配 SimulateRunner。
 *
 * 设计目标：
 * 1. 解耦 RollbackCoordinator 与 ECS Runner。
 * 2. 为未来替换 Runner 提供统一接口层。
 * 3. 提供按帧 Tick 与帧同步能力。
 *
 * 使用场景：
 * - RollbackCoordinator
 * - Rollback Resimulate
 * - ECS 回滚系统
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackRunnerAdapter
    {
        private readonly SimulateRunner _runner;

        public RollbackRunnerAdapter(
            SimulateRunner runner)
        {
            _runner = runner;
        }

        /// <summary>
        /// 执行指定逻辑帧。
        /// </summary>
        public void TickFrame(
            int frame,
            bool isRollback)
        {
            _runner.TickFrame(
                frame,
                isRollback);
        }

        /// <summary>
        /// 同步 Runner 当前帧号。
        /// </summary>
        public void SetFrame(
            int frame)
        {
            _runner.SetFrameCount(frame);
        }
    }
}