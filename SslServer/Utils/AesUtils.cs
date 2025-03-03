namespace SslServer.Utils
{
    using System.Security.Cryptography;
    using System.Text;

    public static class AesUtils
    {
        public static string EncryptString(string plainText, byte[] key, byte[] iv)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = EncryptBytes(plainBytes, key, iv);

            return Convert.ToBase64String(encryptedBytes);
        }

        public static string DecryptString(string encryptedText, byte[] key, byte[] iv)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] decryptedBytes = DecryptBytes(encryptedBytes, key, iv);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public static byte[] EncryptBytes(byte[] data, byte[] key, byte[] iv)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be empty", nameof(data));

            if (key == null || key.Length != 32) // AES-256 requires 32-byte key
                throw new ArgumentException("Key must be 32 bytes for AES-256", nameof(key));

            if (iv == null || iv.Length != 16) // AES requires 16-byte IV
                throw new ArgumentException("IV must be 16 bytes", nameof(iv));

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var memoryStream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                    cryptoStream.FlushFinalBlock();
                    return memoryStream.ToArray();
                }
            }
        }

        public static byte[] DecryptBytes(byte[] encryptedData, byte[] key, byte[] iv)
        {
            if (encryptedData == null || encryptedData.Length == 0)
                throw new ArgumentException("Data cannot be empty", nameof(encryptedData));

            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes for AES-256", nameof(key));

            if (iv == null || iv.Length != 16)
                throw new ArgumentException("IV must be 16 bytes", nameof(iv));

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var memoryStream = new MemoryStream(encryptedData))
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                using (var resultStream = new MemoryStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        resultStream.Write(buffer, 0, bytesRead);
                    }
                    return resultStream.ToArray();
                }
            }
        }
    }
}