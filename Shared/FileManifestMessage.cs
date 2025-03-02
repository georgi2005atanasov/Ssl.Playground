namespace Shared
{
    /// <summary>
    /// Message containing the manifest of available files in a version
    /// </summary>
    public class FileManifestMessage
    {
        public string Version { get; set; } = string.Empty;

        public int FileCount { get; set; }

        public string TotalSize { get; set; } = string.Empty;

        public List<FileManifestEntry> Files { get; set; } = new List<FileManifestEntry>();
    }
}
