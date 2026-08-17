using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class RandomRepeatedRollbackValidationNUnitTests
    {
        [Test]
        public void RandomRepeatedRollback_100000Frames_500PlusCorrections_RemainsConverged()
            => RandomRepeatedRollbackValidationTestBootstrap.RunRandomRepeatedRollbackStressTestStatic();
    }
}