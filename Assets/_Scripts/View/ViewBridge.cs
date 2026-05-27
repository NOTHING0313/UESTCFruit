using BuffSystem;
using Contracts;
using ECSFrameWork;

namespace View
{
    public class ViewBridge : IViewBridge
    {
        private readonly World _world;
        private readonly ViewManager _viewManager;
        private readonly IBuffSystem _buffSystem;

        public ViewBridge(World world, ViewManager viewManager, IBuffSystem buffSystem)
        {
            _world = world;
            _viewManager = viewManager;
            _buffSystem = buffSystem;
        }

        public void PlayEffect(in ViewEffectCommand command) { /* TODO */ }
        public void SyncBuffUI(Entity target, IBuffSystem buffSystem) { /* TODO */ }
    }
}