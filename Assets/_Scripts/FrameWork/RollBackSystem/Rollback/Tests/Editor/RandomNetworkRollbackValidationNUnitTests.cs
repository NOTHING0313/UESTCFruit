using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class RandomNetworkRollbackValidationNUnitTests
    {
        [Test]
        public void FourPlayers_10000Frames_RandomLatencyJitterDuplicateOutOfOrder_RemainsConverged()
        {
            RandomNetworkRollbackValidationTestBootstrap.RandomNetworkSimulationReport report =
                RandomNetworkRollbackValidationTestBootstrap.RunRandomNetworkStressTestStatic();

            TestContext.WriteLine(report.ToDisplayString());

            Assert.AreEqual(10000, report.TotalFrames);
            Assert.AreEqual(4, report.PlayerCount);
            Assert.AreEqual(10000, report.AuthoritySubmittedCount);
            Assert.Greater(report.RollbackCount, 0);
        }
    }
}