namespace SslServer.Utils
{
    public static class Converters
    {
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

        public static long ConvertToBytes(decimal size, string unit)
        {
            return unit.ToUpperInvariant() switch
            {
                "B" => (long)size,
                "KB" => (long)(size * 1024),
                "MB" => (long)(size * 1024 * 1024),
                "GB" => (long)(size * 1024 * 1024 * 1024),
                "TB" => (long)(size * 1024 * 1024 * 1024 * 1024),
                _ => (long)size
            };
        }

    }
}
