namespace Shared
{
    public class FileTransferMessage
    {
        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string FileSize { get; set; } = string.Empty;

        public string Sha256Hash { get; set; } = string.Empty;

        public string FileContent { get; set; } = string.Empty; // Base64 encoded
    }
}
