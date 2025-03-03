namespace SslServer.Utils
{
    using System.Text.Json;

    public static class JsonHelpers
    {
        public static readonly JsonSerializerOptions JsonFormatter = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        //internal static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions
        //{
        //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        //    WriteIndented = false,
        //    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        //    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        //};
    }
}
