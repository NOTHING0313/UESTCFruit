using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class LocalSyncValidationNUnitTests
    {
        [TestCase(100000)]
        public void TwinWorld_SameInitialStateAndInputs_StrictlyEqual(int frameCount) => LocalSyncValidationTestBootstrap.RunTwinWorldDeterminismTestStatic(frameCount);

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void Rollback_AuthoritativeCorrection_ConvergesToReferenceWorld(int rollbackDepth) => LocalSyncValidationTestBootstrap.RunRollbackReferenceEquivalenceTestStatic(rollbackDepth);
    }
}