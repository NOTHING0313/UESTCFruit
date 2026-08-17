using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class UdpLoopbackNUnitTests
    {
        private const string LoopbackAddress = "127.0.0.1";
        private const int ReceiveTimeoutMs = 2000;

        [Test]
        public void Bind_EphemeralLoopbackPort_Succeeds()
        {
            using var transport = CreateTransport();

            Assert.AreEqual(IPAddress.Loopback, transport.LocalEndPoint.Address);
            Assert.Greater(transport.LocalEndPoint.Port, 0);
        }

        [Test]
        public void RawDatagram_ClientToServer_PreservesBytes()
        {
            using var server = CreateTransport();
            using var client = CreateTransport();

            byte[] source = { 0x11, 0x22, 0x33, 0x44, 0x55 };

            client.Send(source, server.LocalEndPoint);

            UdpReceivedDatagram received = WaitReceive(server);

            CollectionAssert.AreEqual(source, received.Data);
            Assert.AreEqual(client.LocalEndPoint.Port, received.RemoteEndPoint.Port);
            Assert.AreEqual(IPAddress.Loopback, received.RemoteEndPoint.Address);
        }

        [Test]
        public void ClientInputPacket_UDP_RoundTrip_PreservesProtocolData()
        {
            using var server = CreateTransport();
            using var client = CreateTransport();

            PlayerInputSnapshot input = CreateInput(120, 2, 1f, -1f);
            var source = new ClientInputPacket(0x11223344u, 77u, input);

            byte[] bytes = NetworkPacketSerializer.SerializeClientInput(in source);
            client.Send(bytes, server.LocalEndPoint);

            UdpReceivedDatagram datagram = WaitReceive(server);

            Assert.IsTrue(
                NetworkPacketSerializer.TryDeserializeClientInput(
                    datagram.Data,
                    out ClientInputPacket result,
                    out NetworkPacketDecodeError error),
                error.ToString());

            Assert.AreEqual(source.SessionId, result.SessionId);
            Assert.AreEqual(source.Sequence, result.Sequence);
            Assert.IsTrue(new PlayerInputSnapshotComparer().IsEqual(source.Input, result.Input));
        }

        [Test]
        public void ServerAuthorityFramePacket_UDP_RoundTrip_PreservesFrameInputSet()
        {
            using var server = CreateTransport();
            using var client = CreateTransport();

            FrameInputSet inputSet = new FrameInputSet(300, new[]
            {
                CreateInput(300,4,-1f,0f),
                CreateInput(300,2,1f,0f),
                CreateInput(300,1,0f,1f),
                CreateInput(300,3,0f,-1f)
            });

            var source = new ServerAuthorityFramePacket(
                0xAABBCCDDu,
                1234u,
                inputSet);

            byte[] bytes = NetworkPacketSerializer.SerializeServerAuthorityFrame(in source);
            server.Send(bytes, client.LocalEndPoint);

            UdpReceivedDatagram datagram = WaitReceive(client);

            Assert.IsTrue(
                NetworkPacketSerializer.TryDeserializeServerAuthorityFrame(
                    datagram.Data,
                    out ServerAuthorityFramePacket result,
                    out NetworkPacketDecodeError error),
                error.ToString());

            Assert.AreEqual(source.SessionId, result.SessionId);
            Assert.AreEqual(source.Sequence, result.Sequence);
            Assert.IsTrue(new FrameInputSetComparer().IsEqual(source.InputSet, result.InputSet));
        }

        [Test]
        public void MultiplePackets_PreserveDatagramBoundaries()
        {
            using var server = CreateTransport();
            using var client = CreateTransport();

            byte[] packetA = { 0xA1, 0x01, 0x02 };
            byte[] packetB = { 0xB2, 0x03, 0x04, 0x05, 0x06 };

            client.Send(packetA, server.LocalEndPoint);
            client.Send(packetB, server.LocalEndPoint);

            UdpReceivedDatagram first = WaitReceive(server);
            UdpReceivedDatagram second = WaitReceive(server);

            var received = new Dictionary<byte, byte[]>
            {
                [first.Data[0]] = first.Data,
                [second.Data[0]] = second.Data
            };

            Assert.AreEqual(2, received.Count);
            CollectionAssert.AreEqual(packetA, received[0xA1]);
            CollectionAssert.AreEqual(packetB, received[0xB2]);
        }

        [Test]
        public void InvalidProtocolDatagram_IsReceivedAndRejectedByProtocolLayer()
        {
            using var server = CreateTransport();
            using var client = CreateTransport();

            var packet = new ClientInputPacket(
                1u,
                1u,
                CreateInput(100, 1, 1f, 0f));

            byte[] bytes = NetworkPacketSerializer.SerializeClientInput(in packet);
            bytes[0] ^= 0xFF;

            client.Send(bytes, server.LocalEndPoint);

            UdpReceivedDatagram datagram = WaitReceive(server);

            Assert.IsFalse(
                NetworkPacketSerializer.TryReadHeader(
                    datagram.Data,
                    out _,
                    out NetworkPacketDecodeError error));

            Assert.AreEqual(NetworkPacketDecodeError.InvalidMagic, error);
        }

        [Test]
        public void Transport_Disposed_FurtherOperationsAreRejected()
        {
            var transport = CreateTransport();
            IPEndPoint target = transport.LocalEndPoint;

            transport.Dispose();

            Assert.IsTrue(transport.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => transport.TryReceive(out _));
            Assert.Throws<ObjectDisposedException>(() => transport.Send(new byte[] { 1 }, target));
        }

        [Test]
        public void OversizedDatagram_IsRejectedBeforeSocketSend()
        {
            using var transport = CreateTransport();

            var data = new byte[NetworkProtocolConstants.MaxDatagramSize + 1];

            Assert.Throws<ArgumentOutOfRangeException>(
                () => transport.Send(data, transport.LocalEndPoint));
        }

        private static UdpTransport CreateTransport()
        {
            return new UdpTransport(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize));
        }

        private static UdpReceivedDatagram WaitReceive(IUdpTransport transport)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < ReceiveTimeoutMs)
            {
                if (transport.TryReceive(out UdpReceivedDatagram datagram))
                    return datagram;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"UDP Loopback Receive Timeout: LocalEndPoint={transport.LocalEndPoint}, Timeout={ReceiveTimeoutMs}ms");

            return default;
        }

        private static PlayerInputSnapshot CreateInput(int frame, int playerID, float moveX, float moveY)
        {
            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = moveY,
                mouseX = 12.5f + playerID,
                mouseY = -20.25f - playerID,
                mouseDeltaX = 0.5f,
                mouseDeltaY = -0.25f,
                scrollX = 1f,
                scrollY = -1f
            };
        }
    }
}