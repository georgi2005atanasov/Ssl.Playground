namespace SslServer.Services
{
    using Shared.Enums;
    using Shared;
    using SslServer.Contracts;
    using SslServer.Data;
    using System.Text.Json;
    using SslServer.Utils;

    public class SecureFileTransferService(IVersionManager versionManager, IDbService dbService) : ISecureFileTransferService
    {
        private readonly IVersionManager _versionManager = versionManager ??
                throw new ArgumentNullException(nameof(versionManager));
        private readonly IDbService _dbService = dbService ??
                throw new ArgumentNullException(nameof(dbService));

        public async Task<(bool Success, string ResponseData)> HandleFileRequest(string versionName, string filePath)
        {
            try
            {
                bool isValid = await _versionManager.ValidateFileHash(filePath, versionName);

                if (!isValid)
                    return (
                        false, 
                        CreateErrorResponse(
                            MessageType.Error, 
                            string.Format(ErrorsConstants.FileHashValidationFailed, filePath
                        )
                    ));

                // Get the full path to the file
                string fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    DirectoriesConstants.VERSIONS,
                    versionName,
                    filePath.TrimStart('\\', '/')
                );

                if (!File.Exists(fullPath))
                    return (
                        false,
                        CreateErrorResponse(
                            MessageType.Error,
                            string.Format(ErrorsConstants.FileNotFound, filePath)
                        )
                    );

                // Get file info from the database
                var fileInfo = await _dbService
                    .GetFileOrDefaultAsync(filePath, versionName);

                if (fileInfo == null)
                    return (
                        false, 
                        CreateErrorResponse(
                            MessageType.Error, 
                            string.Format(ErrorsConstants.FileNotFoundInDatabase, filePath)
                        )
                    );

                // Read the file content
                byte[] fileContent = await File.ReadAllBytesAsync(fullPath);

                // Create file transfer message
                var transferMessage = new FileTransferServerMessage
                {
                    Version = versionName,
                    FileName = fileInfo.FileName,
                    FilePath = fileInfo.FilePath,
                    FileSize = fileInfo.FileSize,
                    FileContent = Convert.ToBase64String(fileContent)
                };

                // Create response
                var response = CreateResponse(MessageType.FileTransfer, transferMessage);

                return (true, response);
            }
            catch (Exception ex)
            {
                return (
                    false, 
                    CreateErrorResponse(
                        MessageType.Error, 
                        string.Format(ErrorsConstants.ErrorHandlingFileRequest, ex.Message)
                    )
                );
            }
        }

        public async Task<(bool Success, string ResponseData)> ValidateClientFile(string latestVersion, string filePath, string clientHash)
        {
            try
            {
                // Get file info from the database
                var fileInfo = await _dbService.GetFileOrDefaultAsync(filePath, latestVersion);

                if (fileInfo == null)
                    return (
                        false, 
                        CreateErrorResponse(
                            MessageType.Error, 
                            string.Format(ErrorsConstants.FileNotFound, filePath)
                        )
                    );

                bool isValid = string.Equals(fileInfo.Sha256, clientHash, StringComparison.OrdinalIgnoreCase);

                // Create validation response
                var validationMessage = new FileValidationServerMessage
                {
                    Version = latestVersion,
                    FilePath = filePath,
                    IsValid = isValid,
                    ExpectedHash = fileInfo.Sha256,
                    ClientHash = clientHash
                };

                // may also be encrypted
                var response = CreateResponse(MessageType.ValidateFile, validationMessage);

                return (true, response);
            }
            catch (Exception ex)
            {
                return (
                    false, 
                    CreateErrorResponse(
                        MessageType.Error, 
                        string.Format(ErrorsConstants.ErrorValidatingClientFile, ex.Message)
                    )
                );
            }
        }

        public async Task<(bool Success, string ResponseData)> GetFileManifest(string versionName)
        {
            try
            {
                var files = await _dbService.GetFilesForVersionAsync(versionName);
                if (files == null || files.Count == 0)
                    return (
                        false, 
                        CreateErrorResponse(
                            MessageType.Error, 
                            string.Format(ErrorsConstants.NoFilesFoundForVersion, versionName)
                        )
                    );

                long totalSizeBytes = 0;
                var manifestEntries = new List<FileManifestEntry>();

                foreach (var file in files)
                {
                    string sizeValue = file.FileSize.Split(' ')[0];
                    string sizeUnit = file.FileSize.Split(' ')[1];

                    decimal size = decimal.Parse(sizeValue);
                    long sizeInBytes = Converters.ConvertToBytes(size, sizeUnit);
                    totalSizeBytes += sizeInBytes;

                    manifestEntries.Add(new FileManifestEntry
                    {
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        FileSize = file.FileSize,
                    });
                }

                var manifestMessage = new FileManifestMessage
                {
                    Version = versionName,
                    FileCount = files.Count,
                    TotalSize = Formatters.FormatFileSize(totalSizeBytes),
                    Files = manifestEntries
                };

                var response = CreateResponse(MessageType.FileManifest, manifestMessage);

                return (true, response);
            }
            catch (Exception)
            {
                return (
                    false,
                    CreateErrorResponse(
                        MessageType.Error, 
                        ErrorsConstants.ErrorGeneratingManifestFile
                    )
                );
            }
        }

        private static string CreateResponse<T>(MessageType messageType, T data)
        {
            var message = new BaseMessage
            {
                Type = messageType,
                TimeStamp = DateTime.UtcNow,
                Data = JsonSerializer.Serialize(data)
            };

            string messageJson = JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter);
            return messageJson;
        }

        private static string CreateErrorResponse(MessageType messageType, string errorMessage)
        {
            var errorData = new ErrorMessage { Message = errorMessage };
            return CreateResponse(messageType, errorData);
        }
    }
}
