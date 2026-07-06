using BuffSystem;
using ECSFrameWork;

namespace View
{
    public sealed class BuffUIRefreshSystem : FixedStepSystemBase
    {
        private readonly BuffUIViewPresenter _presenter;
        private readonly IBuffSystem _buffSystem;

        public override SystemTickSequence sequence => SystemTickSequence.view + 1;

        public BuffUIRefreshSystem(BuffUIViewPresenter presenter, IBuffSystem buffSystem)
        {
            _presenter = presenter;
            _buffSystem = buffSystem;
        }

        public override void Tick(in SimulationContext context)
        {
            if (context.isRollback)
                return;

            _presenter?.RefreshAll(_buffSystem);
        }
    }
}