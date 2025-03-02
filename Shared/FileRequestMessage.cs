namespace Shared
{
    /// <summary>
    /// Message for file requests
    /// </summary>
    public class FileRequestMessage
    {
        public string Version { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;
    }

}
