namespace SslServer.Utils
{
    public static class Formatters
    {
        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal size = bytes;

            while (Math.Round(size / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                size /= 1024;
                counter++;
            }

            return $"{size:n2} {suffixes[counter]}";
        }
    }
}
