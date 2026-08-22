using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class ViewRollbackLifecycleNUnitTests
    {
        [Test]
        public void CreatedEntity_RollbackBeforeCreation_ReleasesOrphanView()
            =>ViewRollbackLifecycleValidationTestBootstrap.RunCreatedEntityRemovedByRollbackReleasesViewStatic();

        [Test]
        public void DestroyedEntity_RollbackBeforeDestroy_RecoversView()
            =>ViewRollbackLifecycleValidationTestBootstrap.RunDestroyedEntityRestoredByRollbackRecoversViewStatic();

        [Test]
        public void PooledViewDestroy_RollbackBeforeDestroy_RestoresSingleReusedView()
            =>ViewRollbackLifecycleValidationTestBootstrap.RunPooledViewDestroyRollbackRestoresSingleViewStatic();

        [Test]
        public void ConsumedViewEvent_RollbackResimulate_DoesNotReplay()
            =>ViewRollbackLifecycleValidationTestBootstrap.RunConsumedViewEventIsNotReplayedAfterRollbackStatic();
    }
}
