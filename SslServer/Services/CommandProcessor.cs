namespace SslServer.Services
{
    using SslServer.Contracts;

    internal class CommandProcessor : ICommandProcessor
    {
        private Func<bool>? _restart;

        public void SetRestartCallback(Func<bool>? restart) 
            => _restart = restart;

        public async Task ProcessCommands()
        {
            while (true)
            {
                string? line = await Task.Run(Console.ReadLine);
                if (string.IsNullOrEmpty(line)) break;

                try
                {
                    HandleCommand(line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] {ex.Message}");
                }
            }
        }

        private void HandleCommand(string line)
        {
            if (line == "!")
            {
                Console.Write("Server restarting...");
                _restart!();
                Console.WriteLine("Done!");
                return;
            }
        }
    }
}
