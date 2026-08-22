using NUnit.Framework;
using System;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class ViewRollbackBoundaryNUnitTests
    {
        [Test]
        public void Snapshot_BeforeViewSpawn_DoesNotCapturePrefabViewRequest()
            =>ViewRollbackBoundaryValidationTestBootstrap.RunSnapshotBeforeSpawnBoundaryStatic();

        [Test]
        public void Snapshot_AfterViewSpawn_DoesNotCaptureViewComponent()
            =>ViewRollbackBoundaryValidationTestBootstrap.RunSnapshotAfterSpawnBoundaryStatic();

        [Test]
        public void Checksum_IsIndependentFromUnityViewID()
            =>ViewRollbackBoundaryValidationTestBootstrap.RunChecksumIgnoresViewIdentityStatic();

        [Test]
        public void Rollback_BeforeInitialViewSpawn_LeavesSingleConsistentView()
            =>ViewRollbackBoundaryValidationTestBootstrap.RunRollbackBeforeInitialViewSpawnConsistencyStatic();
    }
}
