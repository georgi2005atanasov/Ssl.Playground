namespace Shared
{
    /// <summary>
    /// Entry in the file manifest representing a single file
    /// </summary>
    public class FileManifestEntry
    {
        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string FileSize { get; set; } = string.Empty;

        //public string Sha256Hash { get; set; } = string.Empty;
    }
}
