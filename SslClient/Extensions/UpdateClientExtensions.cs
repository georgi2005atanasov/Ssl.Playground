namespace SslClient.Extensions
{
    using SslClient.Clients;

    public static class UpdateClientExtensions
    {
        public static CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

        public static async Task<UpdateClient> ConnectSsl(this UpdateClient client)
        {
            bool started = client.ConnectAsync();
            if (!started)
                throw new InvalidOperationException("The client cannot be started!");

            // Wait indefinitely until the cancellation token is cancelled.
            try
            {
                await Task.Delay(Timeout.Infinite, CancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // This exception is expected when the cancellation token is cancelled.
            }

            // When cancellation is requested, disconnect the client.
            client.DisconnectAsync();
            client.DisconnectAndStop();

            return client;
        }
    }
}
