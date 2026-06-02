/*
 * IFrameCommandSource 定义帧命令回放源。
 *
 * 回滚重模拟时，除了重放输入，还必须重放同帧的外部命令
 * （如 BuffFrameCommand、GameplayFrameCommand 等），否则
 * Buff 添加/移除、技能等状态会丢失或漂移。
 *
 * 实现者负责：
 * - 在逻辑帧执行前，将已缓存的该帧命令应用到 World
 * - 在正常推进时，消费新的帧命令并缓存供未来回放
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public interface IFrameCommandSource
    {
        /// <summary>
        /// 回放指定帧的所有已缓存命令到 World。
        /// 用于回滚重模拟路径。
        /// </summary>
        void ReplayCommandsToWorld(
            World world,
            int frame);

        /// <summary>
        /// 应用指定帧的新命令到 World。
        /// 用于正常推进路径，并同时缓存命令供未来回放。
        /// </summary>
        void ApplyCommandsToWorld(
            World world,
            int frame);
    }
}
