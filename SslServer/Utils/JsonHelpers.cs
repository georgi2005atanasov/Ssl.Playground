namespace SslServer.Utils
{
    using System.Text.Json;

    internal static class JsonHelpers
    {
        internal static readonly JsonSerializerOptions JsonFormatter = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
