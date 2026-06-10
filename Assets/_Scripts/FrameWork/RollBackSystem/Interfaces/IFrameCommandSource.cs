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
 *
 * 回滚重模拟的帧命令时序：
 *   Simulate → BeforeTick 命令 → Tick → AfterTick 命令
 * 与 TimeSimulator 的正常推进时序一致。
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public interface IFrameCommandSource
    {
        /// <summary>
        /// 应用指定帧、指定时机的命令到 World。
        /// isReplay=true 时为回滚重放（跳过已应用检查），
        /// isReplay=false 时为正常推进（同一帧同一时机只执行一次）。
        /// </summary>
        void ApplyCommandsAtTiming(
            World world,
            int frame,
            SimulationFrameCommandTiming timing,
            bool isReplay);
    }
}
