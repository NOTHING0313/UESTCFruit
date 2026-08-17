using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class RepeatedRollbackValidationNUnitTests
    {
        [Test]
        public void RepeatedRollback_10000Frames_100PlusCorrections_RemainsConverged()
            => RepeatedRollbackValidationTestBootstrap.RunRepeatedRollbackStressTestStatic();
    }
}