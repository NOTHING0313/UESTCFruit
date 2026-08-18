using NUnit.Framework;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class MultiClientServerInputExchangeNUnitTests
    {
        [Test]
        public void TwoClients_Player1ThenPlayer2_ServerWaitsAndBroadcastsSameAuthority()
            => MultiClientServerInputExchangeValidationTestBootstrap.RunPlayer1ThenPlayer2Static();

        [Test]
        public void TwoClients_Player2ThenPlayer1_ServerWaitsAndBroadcastsSameAuthority()
            => MultiClientServerInputExchangeValidationTestBootstrap.RunPlayer2ThenPlayer1Static();

        [Test]
        public void TwoClients_100Frames_AlternatingArrivalOrder_BothRemainAuthorityConsistent()
            => MultiClientServerInputExchangeValidationTestBootstrap.Run100FramesAlternatingArrivalOrderStatic();

        [Test]
        public void TwoClients_DuplicatePendingInput_DoesNotCompleteFrameEarly()
            => MultiClientServerInputExchangeValidationTestBootstrap.RunDuplicatePendingInputStatic();

        [Test]
        public void TwoClients_MissingSecondPlayer_ServerDoesNotBroadcastIncompleteAuthority()
            => MultiClientServerInputExchangeValidationTestBootstrap.RunMissingSecondPlayerStatic();

        [Test]
        public void TwoClients_CrossFrameOutOfOrderCompletion_FramesRemainIndependent()
            => MultiClientServerInputExchangeValidationTestBootstrap.RunCrossFrameOutOfOrderCompletionStatic();
    }
}