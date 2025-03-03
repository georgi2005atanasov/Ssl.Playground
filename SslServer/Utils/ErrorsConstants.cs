namespace SslServer.Utils
{
    public static class ErrorsConstants
    {
        public const string FileNotFoundInDatabase = "File info not found in database: {0}";
        public const string FileHashValidationFailed = "File hash validation failed for {0}";
        public const string FileNotFound = "File not found: {0}";
        public const string ErrorHandlingFileRequest = "Error handling file request: {0}";
        public const string ErrorValidatingClientFile = "Error validating client file: {0}";
        public const string ErrorGeneratingManifestFile = "Error generating file manifest";
        public const string NoFilesFoundForVersion = "No files found for version {0}";
        public const string ErrorProcessingMessage = "Error processing message: {0}";
        public const string ErrorHandlingFileManifestRequest = "Error handling file manifest request";
        public const string ErrorHandlingFileValidation = "Error handling file validation";
        public const string InvalidFileValidationRequestFormat = "Invalid file validation request format";
    }
}
