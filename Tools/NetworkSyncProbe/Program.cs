using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;

internal static class Program
{
    private const uint Magic = 0x42525055u;
    private const ushort Version = 1;
    private const byte RequestType = 1;
    private const byte ResponseType = 2;
    private const int PacketSize = 16;
    private const int DefaultPort = 28015;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return ShowUsage();

            return args[0].ToLowerInvariant() switch
            {
                "server" => RunServer(args),
                "client" => RunClient(args),
                _ => ShowUsage()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"NetworkSyncProbe Main Error: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    private static int RunServer(string[] args)
    {
        int port = GetIntArg(args, "--port", DefaultPort, 1, ushort.MaxValue);
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        udp.Client.ReceiveTimeout = 500;

        bool running = true;
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            running = false;
        };

        Console.WriteLine("[NETWORK SYNC UDP PROBE SERVER]");
        Console.WriteLine($"Bind      = 0.0.0.0:{port}");
        Console.WriteLine($"Protocol  = UPRB V{Version}");
        Console.WriteLine("Status    = LISTENING");
        Console.WriteLine("Press Ctrl+C To Stop");

        while (running)
        {
            IPEndPoint remote = new(IPAddress.Any, 0);
            byte[] data;

            try
            {
                data = udp.Receive(ref remote);
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }

            if (!TryReadPacket(data, RequestType, out ulong nonce))
            {
                Console.WriteLine($"REJECT from={remote} bytes={data.Length}");
                continue;
            }

            Console.WriteLine($"REQUEST from={remote} nonce={nonce} bytes={data.Length}");

            byte[] response = CreatePacket(ResponseType, nonce);
            udp.Send(response, response.Length, remote);

            Console.WriteLine($"RESPONSE to={remote} nonce={nonce} bytes={response.Length}");
        }

        Console.WriteLine("Status    = STOPPED");
        return 0;
    }

    private static int RunClient(string[] args)
    {
        string host = GetStringArg(args, "--host")
            ?? throw new ArgumentException("Missing Required Argument: --host");

        int port = GetIntArg(args, "--port", DefaultPort, 1, ushort.MaxValue);
        int count = GetIntArg(args, "--count", 5, 1, 100);
        int timeoutMs = GetIntArg(args, "--timeout", 2000, 100, 60000);

        IPAddress serverAddress = ResolveIPv4(host);
        IPEndPoint serverEndPoint = new(serverAddress, port);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.ReceiveTimeout = timeoutMs;
        udp.Connect(serverEndPoint);

        Console.WriteLine("[NETWORK SYNC UDP PROBE CLIENT]");
        Console.WriteLine($"Server    = {serverEndPoint}");
        Console.WriteLine($"Count     = {count}");
        Console.WriteLine($"Timeout   = {timeoutMs} ms");

        int success = 0;
        double totalRttMs = 0d;

        for (int i = 1; i <= count; i++)
        {
            ulong nonce = unchecked((ulong)Random.Shared.NextInt64());
            byte[] request = CreatePacket(RequestType, nonce);

            Stopwatch stopwatch = Stopwatch.StartNew();
            udp.Send(request, request.Length);

            try
            {
                IPEndPoint remote = new(IPAddress.Any, 0);
                byte[] response = udp.Receive(ref remote);
                stopwatch.Stop();

                if (!remote.Address.Equals(serverAddress) || remote.Port != port)
                {
                    Console.WriteLine($"[{i}/{count}] FAIL UnexpectedEndpoint={remote}");
                    continue;
                }

                if (!TryReadPacket(response, ResponseType, out ulong responseNonce))
                {
                    Console.WriteLine($"[{i}/{count}] FAIL InvalidResponse Bytes={response.Length}");
                    continue;
                }

                if (responseNonce != nonce)
                {
                    Console.WriteLine($"[{i}/{count}] FAIL NonceMismatch Expected={nonce} Actual={responseNonce}");
                    continue;
                }

                success++;
                totalRttMs += stopwatch.Elapsed.TotalMilliseconds;

                Console.WriteLine($"[{i}/{count}] PASS RTT={stopwatch.Elapsed.TotalMilliseconds:F2} ms Nonce={nonce}");
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut)
            {
                stopwatch.Stop();
                Console.WriteLine($"[{i}/{count}] TIMEOUT After={timeoutMs} ms");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Result    = {success}/{count} PASS");

        if (success > 0)
            Console.WriteLine($"Avg RTT   = {totalRttMs / success:F2} ms");

        return success == count ? 0 : 2;
    }

    private static byte[] CreatePacket(byte type, ulong nonce)
    {
        var data = new byte[PacketSize];

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0, 4), Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), Version);
        data[6] = type;
        data[7] = 0;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(8, 8), nonce);

        return data;
    }

    private static bool TryReadPacket(byte[] data, byte expectedType, out ulong nonce)
    {
        nonce = 0;

        if (data == null || data.Length != PacketSize) return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4)) != Magic) return false;
        if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4, 2)) != Version) return false;
        if (data[6] != expectedType || data[7] != 0) return false;

        nonce = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(8, 8));
        return true;
    }

    private static IPAddress ResolveIPv4(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException($"IPv4 Required: {address}");

            return address;
        }

        IPAddress? resolved = Dns.GetHostAddresses(host)
            .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

        return resolved ?? throw new InvalidOperationException($"No IPv4 Address Found: {host}");
    }

    private static string? GetStringArg(string[] args, string name)
    {
        for (int i = 1; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];

        return null;
    }

    private static int GetIntArg(string[] args, string name, int defaultValue, int min, int max)
    {
        string? raw = GetStringArg(args, name);
        if (raw == null) return defaultValue;

        if (!int.TryParse(raw, out int value) || value < min || value > max)
            throw new ArgumentOutOfRangeException(name, $"Expected {min}~{max}, Actual={raw}");

        return value;
    }

    private static int ShowUsage()
    {
        Console.WriteLine("NetworkSyncProbe");
        Console.WriteLine();
        Console.WriteLine("Server:");
        Console.WriteLine("  NetworkSyncProbe server --port 28015");
        Console.WriteLine();
        Console.WriteLine("Client:");
        Console.WriteLine("  NetworkSyncProbe client --host <IPv4> --port 28015 --count 5 --timeout 2000");
        return 1;
    }
}