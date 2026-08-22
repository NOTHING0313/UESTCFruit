using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkInputClientPumpNUnitTests
    {
        [Test]
        public void Tick_DrainsAllQueuedAuthorities_AndDispatchesOnce()
        {
            var client=new FakeNetworkInputClient();
            client.Enqueue(CreateAuthority(1,1));
            client.Enqueue(CreateAuthority(2,2));

            using var pump=new NetworkInputClientPump(client);
            var frames=new List<int>();
            pump.AuthorityReceived+=packet=>frames.Add(packet.InputSet.frameNumber);

            int received=pump.Tick();

            Assert.AreEqual(2,received);
            Assert.AreEqual(2,pump.ReceivedAuthorityCount);
            CollectionAssert.AreEqual(new[] { 1,2 },frames);
            Assert.AreEqual(1,client.TickCount);
        }

        [Test]
        public void SendInput_DelegatesToClient()
        {
            var client=new FakeNetworkInputClient();
            using var pump=new NetworkInputClientPump(client);

            var input=new PlayerInputSnapshot(7,1) { moveX=1f };
            pump.SendInput(in input);

            Assert.AreEqual(1,client.SentInputs.Count);
            Assert.AreEqual(7,client.SentInputs[0].frameNumber);
        }

        [Test]
        public void ConnectionStateChanged_ForwardsClientState()
        {
            var client=new FakeNetworkInputClient();
            using var pump=new NetworkInputClientPump(client);
            var states=new List<NetworkInputClientConnectionState>();
            pump.ConnectionStateChanged+=states.Add;

            client.SetConnectionState(NetworkInputClientConnectionState.Faulted);

            Assert.AreEqual(NetworkInputClientConnectionState.Faulted,pump.ConnectionState);
            CollectionAssert.AreEqual(new[] { NetworkInputClientConnectionState.Faulted },states);
        }

        [Test]
        public void Tick_TransportError_Throws()
        {
            var client=new FakeNetworkInputClient { HasTransportErrorValue=true,LastTransportErrorValue="Injected" };
            using var pump=new NetworkInputClientPump(client);

            InvalidOperationException exception=Assert.Throws<InvalidOperationException>(()=>pump.Tick());
            StringAssert.Contains("Injected",exception.Message);
        }

        private static ServerAuthorityFramePacket CreateAuthority(int frame,uint sequence)
        {
            var input=new PlayerInputSnapshot(frame,1) { moveX=frame };
            return new ServerAuthorityFramePacket(0x11223344u,sequence,new FrameInputSet(frame,new[] { input }));
        }

        private sealed class FakeNetworkInputClient : INetworkInputClient
        {
            private readonly Queue<ServerAuthorityFramePacket> _authorities=new();

            public NetworkInputTransportMode TransportMode => NetworkInputTransportMode.RawUdp;
            public uint SessionId => 0x11223344u;
            public int PlayerID => 1;
            public bool IsReady => ConnectionState==NetworkInputClientConnectionState.Connected;
            public NetworkInputClientConnectionState ConnectionState { get; private set; }=NetworkInputClientConnectionState.Connected;
            public IPEndPoint LocalEndPoint => new(IPAddress.Loopback,12345);
            public uint LastSentSequence { get; private set; }
            public NetworkInputExchangeRejectReason LastRejectReason { get; set; }
            public NetworkPacketDecodeError LastDecodeError { get; set; }
            public bool HasTransportError => HasTransportErrorValue;
            public string LastTransportError => LastTransportErrorValue;
            public bool HasTransportErrorValue { get; set; }
            public string LastTransportErrorValue { get; set; }
            public int TickCount { get; private set; }
            public List<PlayerInputSnapshot> SentInputs { get; }=new();

            public event Action<NetworkInputClientConnectionState> ConnectionStateChanged;

            public void SetConnectionState(NetworkInputClientConnectionState state)
            {
                if(ConnectionState==state) return;
                ConnectionState=state;
                ConnectionStateChanged?.Invoke(state);
            }

            public void Enqueue(ServerAuthorityFramePacket packet)=>_authorities.Enqueue(packet);

            public void Tick()=>TickCount++;

            public void SendInput(in PlayerInputSnapshot input)
            {
                SentInputs.Add(input);
                LastSentSequence++;
            }

            public bool TryReceiveAuthority(out ServerAuthorityFramePacket packet)
            {
                if(_authorities.Count==0)
                {
                    packet=default;
                    return false;
                }

                packet=_authorities.Dequeue();
                return true;
            }

            public void Dispose(){}
        }
    }
}
