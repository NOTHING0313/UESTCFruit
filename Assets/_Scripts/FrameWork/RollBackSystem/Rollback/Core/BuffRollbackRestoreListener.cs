/*
 * 文件说明：BuffSystem 的 Rollback restore listener 适配器。
 * 设计约束：本阶段只提供入口，不自动查找或反射接入 BuffSystemCore。
 */

using BuffSystem;
using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    internal sealed class BuffRollbackRestoreListener : IRollbackRestoreListener
    {
        private readonly BuffSystemCore _buffSystem;

        public BuffRollbackRestoreListener(BuffSystemCore buffSystem)
        {
            _buffSystem = buffSystem;
        }

        public void OnRollbackWorldRestored(World world, int restoredFrame)
        {
            _buffSystem?.OnWorldRestored(world);
        }

        public void OnRollbackResimulated(World world, int currentFrame)
        {
        }
    }
}
