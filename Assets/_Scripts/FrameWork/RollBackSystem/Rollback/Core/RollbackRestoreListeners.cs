/*
 * 文件说明：Rollback restore/resimulate 后置通知入口。
 * 设计约束：Coordinator 不直接依赖 ECS World，真实 World 只在 Adapter/Bootstrap 边界暴露给 listener。
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    public interface IRollbackRestoreListener
    {
        void OnRollbackWorldRestored(World world, int restoredFrame);

        void OnRollbackResimulated(World world, int currentFrame);
    }

    internal interface IRollbackWorldRestoreNotifier
    {
        void NotifyRollbackResimulated(int currentFrame);
    }
}
