using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class FrameCommandRollbackValidationNUnitTests
    {
        [Test]
        public void FrameCommand_BeforeAndAfterTick_TimingIsPreserved() => FrameCommandRollbackValidationTestBootstrap.RunFrameCommandTimingTestStatic();

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void FrameCommand_RollbackReplay_ConvergesToReferenceWorld(int rollbackDepth) => FrameCommandRollbackValidationTestBootstrap.RunFrameCommandRollbackReplayTestStatic(rollbackDepth);
    }
}