namespace Shared
{
    public class FileTransferServerMessage
    {
        public string Version { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string FileSize { get; set; } = string.Empty;

        public string FileContent { get; set; } = string.Empty; // Base64 encoded
    }
}
