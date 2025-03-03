namespace SslServer.Utils
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    public static class FileHashUtility
    {
        public static string CalculateSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);

            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public static bool ValidateFileHash(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            string actualHash = CalculateSha256(filePath);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetFileSize(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            long bytes = fileInfo.Length;

            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }
    }
}
