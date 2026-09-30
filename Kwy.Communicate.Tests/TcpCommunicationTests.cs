using Kwy.Communicate.TcpSerial;
using Kwy.Communicate.TcpSerial.Configs;
using System.Net;
using System.Net.Sockets;

namespace Kwy.Communicate.Tests;

public sealed class TcpCommunicationTests
{
    [Fact]
    public async Task Loopback_RoundTripsBytes()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            await using var client = new TcpCommunication(new TcpConfig
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                AutoReconnect = false,
                ReceiveTimeout = 2000,
                SendTimeout = 2000
            });

            await client.ConnectAsync();
            using TcpClient server = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
            NetworkStream serverStream = server.GetStream();

            byte[] outbound = [1, 2, 3, 4];
            await client.WriteAsync(outbound);
            var serverBuffer = new byte[4];
            await serverStream.ReadExactlyAsync(serverBuffer);

            Assert.Equal(outbound, serverBuffer);

            byte[] inbound = [5, 6, 7];
            await serverStream.WriteAsync(inbound);
            var clientBuffer = new byte[3];
            int received = await client.ReadAsync(clientBuffer);

            Assert.Equal(3, received);
            Assert.Equal(inbound, clientBuffer);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ReadAsync_UsesConfiguredTimeout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        try
        {
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            await using var client = new TcpCommunication(new TcpConfig
            {
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                AutoReconnect = false,
                ReceiveTimeout = 100
            });

            await client.ConnectAsync();
            using TcpClient server = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));

            await Assert.ThrowsAsync<TimeoutException>(async () =>
                await client.ReadAsync(new byte[1]));
        }
        finally
        {
            listener.Stop();
        }
    }
}
