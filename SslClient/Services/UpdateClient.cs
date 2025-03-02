namespace SslClient.Clients
{
    using Microsoft.Extensions.Configuration;
    using NetCoreServer;
    using Shared;
    using SslClient.Models.Internal;
    using SslClient.Utils;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Text;

    public class UpdateClient(SslContext context, string address, int port) : NetCoreServer.SslClient(context, address, port)
    {
        private bool _stop;
        private static ClientConfiguration _clientConfiguration = new();
        
        protected override void OnConnected()
        {
            Console.WriteLine("TCP Connection established.");
        }

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        /// <summary>
        /// When the Ssl Connection is established, a message in JSON format
        /// is sent to the server about the client's latest version
        /// </summary>
        protected override void OnHandshaked()
        {
            Console.WriteLine($"SSL client handshaked a new session with Id {Id}");

            var message = new BaseMessage
            {
                TimeStamp = DateTime.UtcNow,
                Type = Shared.Enums.MessageType.VersionInfo,
                Data = System.Text.Json.JsonSerializer.Serialize(ClientHelpers.GetCurrentVersion()),
            };

            this.SendAsync(System.Text.Json.JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter));
        }

        protected override void OnDisconnected()
        {
            _stop = true;
            Console.WriteLine($"Chat SSL client disconnected a session with Id {Id}");
            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL client caught an error with code {error}");
        }

        public static UpdateClient WithConfiguration(Func<ConfigurationBuilder, ClientConfiguration?> configuration)
        {
            var configBuilder = new ConfigurationBuilder();

            _clientConfiguration = configuration(configBuilder) ?? throw new NullReferenceException("Invalid Configuration");

            var context = new SslContext(SslProtocols.Tls13, (sender, certificate, chain, sslPolicyErrors) => true);

            var client = new UpdateClient(context, _clientConfiguration.Server.IpAddress, _clientConfiguration.Server.Port);

            return client;
        }
    }
}
