using ECSFrameWork;
using BuffSystem;

namespace Contracts
{
    public interface IViewBridge
    {
        void PlayEffect(in ViewEffectCommand command);
        void SyncBuffUI(Entity target, IBuffSystem buffSystem);
    }
}