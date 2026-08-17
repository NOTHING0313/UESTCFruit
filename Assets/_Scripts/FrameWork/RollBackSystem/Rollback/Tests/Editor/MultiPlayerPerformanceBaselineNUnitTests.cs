using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    [Category("Performance")]
    public sealed class MultiPlayerPerformanceBaselineNUnitTests
    {
        [Explicit("Manual Multiplayer Performance Baseline")]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        public void NormalFrame_PerformanceBaseline(int playerCount)
        {
            MultiPlayerPerformanceBaselineTestBootstrap.NormalPerformanceReport report =
                MultiPlayerPerformanceBaselineTestBootstrap.RunNormalBaseline(playerCount);

            TestContext.WriteLine(report.ToDisplayString());
            Assert.Greater(report.Total.AverageUs, 0d);
        }

        [Explicit("Manual Multiplayer Rollback Performance Baseline")]
        [TestCase(2, 6)]
        [TestCase(2, 30)]
        [TestCase(2, 60)]
        [TestCase(4, 60)]
        [TestCase(8, 60)]
        public void Rollback_PerformanceBaseline(int playerCount, int rollbackDepth)
        {
            MultiPlayerPerformanceBaselineTestBootstrap.RollbackPerformanceReport report =
                MultiPlayerPerformanceBaselineTestBootstrap.RunRollbackBaseline(playerCount, rollbackDepth);

            TestContext.WriteLine(report.ToDisplayString());
            Assert.Greater(report.Rollback.AverageUs, 0d);
        }
    }
}