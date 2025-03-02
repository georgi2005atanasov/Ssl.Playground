namespace SslServer.Models.Internal
{
    using SslClient.Models.Internal;

    public class ServerConfiguration
    {
        public string IpAddress { get; set; } = string.Empty;

        public int Port { get; set; }

        public string CertPath { get; set; } = string.Empty;

        public string CertPwd { get; set; } = string.Empty;

        public string DbConnection { get; set; } = string.Empty;

        public Secrets? Secrets { get; set; }
    }
}
