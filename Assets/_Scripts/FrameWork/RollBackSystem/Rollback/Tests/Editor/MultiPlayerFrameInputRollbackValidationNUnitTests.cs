using NUnit.Framework;

namespace FrameWork.RollBackSystem.Tests
{
    [TestFixture]
    public sealed class MultiPlayerFrameInputRollbackValidationNUnitTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        [TestCase(30)]
        [TestCase(60)]
        public void TwoPlayers_Player2PredictionMismatch_RollbackConvergesBothPlayers(int rollbackDepth)
            => MultiPlayerFrameInputRollbackValidationTestBootstrap.RunTwoPlayerRollbackTestStatic(rollbackDepth);
    }
}