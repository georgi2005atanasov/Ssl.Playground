namespace Shared
{
    /// <summary>
    /// Message for file validation results
    /// </summary>
    public class FileValidationMessage
    {
        public string FilePath { get; set; } = string.Empty;

        public bool IsValid { get; set; }

        public string ExpectedHash { get; set; } = string.Empty;

        public string ClientHash { get; set; } = string.Empty;
    }
}
