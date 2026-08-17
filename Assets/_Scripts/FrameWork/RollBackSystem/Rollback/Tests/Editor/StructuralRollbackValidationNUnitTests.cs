using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class StructuralRollbackValidationNUnitTests
    {
        [Test]
        public void StructuralCommands_NormalTimeline_ProducesExpectedState() => StructuralRollbackValidationTestBootstrap.RunStructuralCommandTimelineTestStatic();

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void StructuralChanges_RollbackReplay_ConvergesToReferenceWorld(int rollbackDepth) => StructuralRollbackValidationTestBootstrap.RunStructuralRollbackReplayTestStatic(rollbackDepth);
    }
}