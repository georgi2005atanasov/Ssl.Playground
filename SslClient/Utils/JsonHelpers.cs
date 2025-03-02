namespace SslClient.Utils
{
    using System.Text.Json;

    internal class JsonHelpers
    {
        internal static readonly JsonSerializerOptions JsonFormatter = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
