namespace SslServer.Data.Models
{
    using global::SslServer.Data.Models.Base;

    public class File : BaseFile
    {
        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string Sha256 { get; set; } = string.Empty;

        public string FileSize { get; set; } = string.Empty;
    }
}
