namespace SslClient.Utils
{
    using Shared;
    using System.Security.Cryptography;

    public static class ClientHelpers
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

        public static async Task<string> CalculateFileHashAsync(string filePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);
            byte[] hashBytes = await sha256.ComputeHashAsync(stream);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            if (hex.StartsWith("0x"))
                hex = hex.Substring(2);

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even number of characters");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        public static string ByteArrayToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
