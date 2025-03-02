namespace SslClient.Extensions
{
    using SslClient.Clients;

    public static class UpdateClientExtensions
    {
        public static UpdateClient ConnectSsl(this UpdateClient client)
        {
            var started = client.ConnectAsync();

            if (!started)
                throw new InvalidOperationException("The client cannot be started!");

            // before doing anything else, make sure to send a json message
            // to the server, giving info about the current version


            // Perform text input
            for (; ; )
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Disconnect the client
                if (line == "!")
                {
                    Console.Write("Client disconnecting...");
                    client.DisconnectAsync();
                    Console.WriteLine("Done!");
                    continue;
                }
            }

            // Disconnect the client
            client.DisconnectAndStop();

            return client;
        }
    }
}
