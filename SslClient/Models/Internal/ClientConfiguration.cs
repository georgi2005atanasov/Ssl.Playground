namespace SslClient.Models.Internal
{
    public class ClientConfiguration
    {
        public ServerConfiguration Server { get; set; } = new();

        public Secrets? Secrets { get; set; }
    }
}
