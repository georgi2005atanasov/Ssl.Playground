namespace SslServer
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using SslServer.Contracts;
    using SslServer.Data;
    using SslServer.Data.SslServer.Data;
    using SslServer.Extensions;
    using SslServer.Models.Internal;
    using SslServer.Services;

    class Program
    {
        public static async Task Main()
            => await UpdateServer
                .WithConfiguration(x =>
                    x.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build()
                    .GetSection("ServerConfiguration")
                    .Get<ServerConfiguration>())
                .WithServices(services =>
                {
                    services.AddSingleton<IVersionManager, VersionManager>();
                    services.AddSingleton<ICommandProcessor, CommandProcessor>();
                    services.AddSingleton<IDbService, DbService>();
                    services.AddSingleton<SecureFileTransferService>();
                })
                .StartSsl()
                .TrackVersions()
                .ListenAsync();
    }
}