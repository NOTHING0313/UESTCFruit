using NUnit.Framework;
using System;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkSessionStallPolicyNUnitTests
    {
        [Test]
        public void Connected_AuthorityProgress_KeepsSimulationRunning()
        {
            var policy=new NetworkSessionStallPolicy(1d);
            policy.Reset(0d,NetworkInputClientConnectionState.Connected,0);

            Assert.IsTrue(policy.Evaluate(0.25d,NetworkInputClientConnectionState.Connected,1));
            Assert.IsTrue(policy.Evaluate(0.75d,NetworkInputClientConnectionState.Connected,1));
            Assert.IsTrue(policy.Evaluate(1.00d,NetworkInputClientConnectionState.Connected,2));

            Assert.AreEqual(NetworkSessionStallReason.None,policy.StallReason);
            Assert.AreEqual(2,policy.LastAuthorityCount);
            Assert.AreEqual(1.00d,policy.LastAuthorityProgressTime,1e-9);
        }

        [Test]
        public void AuthorityStops_TimeoutStalls_NewAuthorityRecovers()
        {
            var policy=new NetworkSessionStallPolicy(1d);
            policy.Reset(10d,NetworkInputClientConnectionState.Connected,100);

            Assert.IsTrue(policy.Evaluate(10.99d,NetworkInputClientConnectionState.Connected,100));

            Assert.IsFalse(policy.Evaluate(11.00d,NetworkInputClientConnectionState.Connected,100));
            Assert.AreEqual(NetworkSessionStallReason.AuthorityTimeout,policy.StallReason);
            Assert.IsFalse(policy.ShouldRunSimulation);

            Assert.IsTrue(policy.Evaluate(11.25d,NetworkInputClientConnectionState.Connected,101));
            Assert.AreEqual(NetworkSessionStallReason.None,policy.StallReason);
            Assert.IsTrue(policy.ShouldRunSimulation);
            Assert.AreEqual(11.25d,policy.LastAuthorityProgressTime,1e-9);
        }

        [TestCase(NetworkInputClientConnectionState.Connecting)]
        [TestCase(NetworkInputClientConnectionState.Disconnected)]
        [TestCase(NetworkInputClientConnectionState.Faulted)]
        public void TransportUnavailable_StallsImmediately(NetworkInputClientConnectionState state)
        {
            var policy=new NetworkSessionStallPolicy(1d);
            policy.Reset(0d,NetworkInputClientConnectionState.Connected,5);

            Assert.IsFalse(policy.Evaluate(0.1d,state,5));
            Assert.AreEqual(NetworkSessionStallReason.TransportUnavailable,policy.StallReason);
        }

        [Test]
        public void TransportReturnsConnected_WithinHeartbeatWindow_Recovers()
        {
            var policy=new NetworkSessionStallPolicy(1d);
            policy.Reset(0d,NetworkInputClientConnectionState.Connected,5);

            Assert.IsFalse(policy.Evaluate(0.2d,NetworkInputClientConnectionState.Disconnected,5));
            Assert.IsTrue(policy.Evaluate(0.5d,NetworkInputClientConnectionState.Connected,5));
            Assert.AreEqual(NetworkSessionStallReason.None,policy.StallReason);
        }

        [Test]
        public void AuthorityCountRegression_Throws()
        {
            var policy=new NetworkSessionStallPolicy(1d);
            policy.Reset(0d,NetworkInputClientConnectionState.Connected,10);

            InvalidOperationException exception=Assert.Throws<InvalidOperationException>(
                ()=>policy.Evaluate(0.1d,NetworkInputClientConnectionState.Connected,9));

            StringAssert.Contains("Authority Count Regressed",exception.Message);
        }
    }
}
