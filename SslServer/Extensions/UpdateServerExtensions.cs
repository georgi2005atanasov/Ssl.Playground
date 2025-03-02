namespace SslServer.Extensions
{
    using SslServer.Services;

    internal static class UpdateServerExtensions
    {
        public static UpdateServer StartSsl(this UpdateServer server)
        {
            var started = server.Start();

            if (!started)
                throw new InvalidOperationException("The server cannot be started!");

            return server;
        }
    }
}
