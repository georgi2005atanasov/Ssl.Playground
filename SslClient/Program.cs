namespace SslClient
{
    using SslClient.Clients;
    using SslClient.Models.Internal;
    using Microsoft.Extensions.Configuration;
    using SslClient.Extensions;

    class Program
    {
        public static async Task Main()
            => await UpdateClient
                .WithConfiguration(x =>
                    x.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build()
                    .GetSection("ClientConfiguration")
                    .Get<ClientConfiguration>())
                .ConnectSsl();
    }
}

