namespace SslClient.Utils
{
    using System.Text.Json;

    public class JsonHelpers
    {
        public static readonly JsonSerializerOptions JsonFormatter = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}
