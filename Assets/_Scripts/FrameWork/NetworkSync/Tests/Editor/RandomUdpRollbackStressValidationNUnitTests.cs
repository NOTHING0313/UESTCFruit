using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class RandomUdpRollbackStressValidationNUnitTests
    {
        [Test]
        public void TwoPlayers_2000Frames_RandomDelayedRealUdpAuthority_RemainsConverged()
        {
            RandomUdpRollbackStressValidationTestBootstrap.RandomUdpRollbackStressReport report =
                RandomUdpRollbackStressValidationTestBootstrap.RunRandomUdpRollbackStressStatic();

            TestContext.WriteLine(report.ToDisplayString());

            Assert.AreEqual(2000, report.TotalFrames);
            Assert.AreEqual(2000, report.AuthorityReceivedCount);
            Assert.AreEqual(2000, report.ServerAuthorityFrameCount);
            Assert.Greater(report.MispredictedFrameCount, 0);
            Assert.AreEqual(report.MispredictedFrameCount, report.MismatchAuthorityCorrectionCount);
        }
    }
}