namespace SslClient.Utils
{
    using Shared;

    internal static class ClientHelpers
    {
        public static CheckVersion GetCurrentVersion()
        {
            var latestVersion = new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "InstalledVersions"))
                .GetDirectories()
                .OrderBy(f => f.LastWriteTime)
                .LastOrDefault();

            var data = new CheckVersion
            {
                CurrentVersion = latestVersion != null ?
                    Path.GetFileName(latestVersion.FullName) :
                    string.Empty,
            };

            return data;
        }
    }
}
