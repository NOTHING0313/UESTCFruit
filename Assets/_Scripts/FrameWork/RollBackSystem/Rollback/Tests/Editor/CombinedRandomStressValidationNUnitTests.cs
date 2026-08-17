using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class CombinedRandomStressValidationNUnitTests
    {
        [Test]
        public void CombinedRandomStress_10000Frames_50PlusRollbacks_AllSubsystemsRemainConverged()
            => CombinedRandomStressValidationTestBootstrap.RunCombinedRandomStressTestStatic();
    }
}