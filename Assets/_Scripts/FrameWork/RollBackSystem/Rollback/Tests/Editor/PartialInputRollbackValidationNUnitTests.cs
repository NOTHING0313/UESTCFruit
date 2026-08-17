using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class PartialInputRollbackValidationNUnitTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void Player2InputMissing_LastKnownPrediction_AuthorityArrives_RollbackConverges(int authoritativeDelay)
            => PartialInputRollbackValidationTestBootstrap.RunPartialInputRollbackTestStatic(authoritativeDelay);
    }
}