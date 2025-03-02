namespace SslServer.Services
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NetCoreServer;
    using SslServer.Contracts;
    using SslServer.Data;
    using SslServer.Data.SslServer.Data;
    using SslServer.Models.Internal;
    using SslServer.Utils;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    internal class UpdateServer : SslServer
    {
        private IServiceProvider? _serviceProvider;
        private readonly IServiceCollection _services = new ServiceCollection();
        private readonly ServerConfiguration _serverConfiguration = new();
        private readonly IDbService _dbService;

        public UpdateServer(SslContext context, DnsEndPoint endpoint) : base(context, endpoint)
        {
        }

        public UpdateServer(SslContext context, IPAddress address, int port) : base(context, address, port)
        {
        }

        public UpdateServer(ServerConfiguration serverConfiguration, SslContext context, IPAddress address, int port)
            : this(context, address, port)
        {
            _serverConfiguration = serverConfiguration;
        }

        protected override SslSession CreateSession()
        {
            return new UpdateSession(
                this,
                _serviceProvider!.GetRequiredService<IVersionManager>(),
                _serviceProvider!.GetRequiredService<SecureFileTransferService>()
            );
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL server caught an error with code {error}");
        }

        public static UpdateServer WithConfiguration(Func<ConfigurationBuilder, ServerConfiguration?> configuration)
        {
            var configBuilder = new ConfigurationBuilder();

            var config = configuration(configBuilder) ?? throw new NullReferenceException("Invalid Configuration");

            var context = new SslContext(SslProtocols.Tls13, new X509Certificate2(config.CertPath, config.CertPwd));

            var server = new UpdateServer(config, context, IPAddress.Any, config.Port);

            return server;
        }

        public UpdateServer WithServices(Action<IServiceCollection> configureServices)
        {
            _services.AddSingleton(_serverConfiguration);

            configureServices(_services);

            _serviceProvider = _services.BuildServiceProvider();

            return this;
        }

        public UpdateServer TrackVersions()
        {
            try
            {
                var _versionManager = _serviceProvider!.GetRequiredService<IVersionManager>();

                _versionManager.SetNotificationCallback(message =>
                {
                    this.Multicast(message);
                });

                _versionManager.TrackVersions();
                return this;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TrackVersions: {ex.Message}");
                return this;
            }
        }

        public async Task ListenAsync()
        {
            var _commandProcessor = _serviceProvider!.GetRequiredService<ICommandProcessor>();
            await _commandProcessor.ProcessCommands();
        }
    }
}
