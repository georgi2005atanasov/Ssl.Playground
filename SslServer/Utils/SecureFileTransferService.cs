namespace SslServer.Utils
{
    using Shared.Enums;
    using Shared;
    using SslServer.Contracts;
    using SslServer.Data;
    using System.Text.Json;

    internal class SecureFileTransferService
    {
        private readonly IVersionManager _versionManager;
        private readonly IDbService _dbService;

        public SecureFileTransferService(IVersionManager versionManager, IDbService dbService)
        {
            _versionManager = versionManager ?? throw new ArgumentNullException(nameof(versionManager));
            _dbService = dbService ?? throw new ArgumentNullException(nameof(dbService));
        }

        public async Task<(bool Success, byte[] ResponseData)> HandleFileRequest(string versionName, string filePath)
        {
            try
            {
                bool isValid = await _versionManager.ValidateFileHash(filePath, versionName);
                if (!isValid)
                {
                    var errorMessage = $"File hash validation failed for {filePath}";
                    Console.WriteLine(errorMessage);
                    return (false, CreateErrorResponse(MessageType.Error, errorMessage));
                }

                // Get the full path to the file
                string fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Versions",
                    versionName,
                    filePath.TrimStart('\\', '/')
                );

                if (!File.Exists(fullPath))
                {
                    var errorMessage = $"File not found: {filePath}";
                    Console.WriteLine(errorMessage);
                    return (false, CreateErrorResponse(MessageType.Error, errorMessage));
                }

                // Get file info from the database
                var fileInfo = await _dbService.GetFileAsync(filePath, versionName);
                if (fileInfo == null)
                {
                    var errorMessage = $"File info not found in database: {filePath}";
                    Console.WriteLine(errorMessage);
                    return (false, CreateErrorResponse(MessageType.Error, errorMessage));
                }

                // Read the file content
                byte[] fileContent = await File.ReadAllBytesAsync(fullPath);

                // Create file transfer message
                var transferMessage = new FileTransferMessage
                {
                    FileName = fileInfo.FileName,
                    FilePath = fileInfo.FilePath,
                    FileSize = fileInfo.FileSize,
                    Sha256Hash = fileInfo.Sha256,
                    FileContent = Convert.ToBase64String(fileContent)
                };

                Console.WriteLine($"Sending file: {fileInfo.FileName}, Size: {fileInfo.FileSize}, Hash: {fileInfo.Sha256}");

                // Create response
                var response = CreateResponse(MessageType.FileTransfer, transferMessage);

                return (true, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file request: {ex.Message}");
                return (false, CreateErrorResponse(MessageType.Error, "Internal server error"));
            }
        }

        public async Task<(bool Success, byte[] ResponseData)> ValidateClientFile(string versionName, string filePath, string clientHash)
        {
            try
            {
                Console.WriteLine($"Validating client hash for {filePath}: {clientHash}");

                // Get file info from the database
                var fileInfo = await _dbService.GetFileAsync(filePath, versionName);
                if (fileInfo == null)
                {
                    var errorMessage = $"File info not found in database: {filePath}";
                    Console.WriteLine(errorMessage);
                    return (false, CreateErrorResponse(MessageType.Error, errorMessage));
                }

                // Compare the hashes
                bool isValid = string.Equals(fileInfo.Sha256, clientHash, StringComparison.OrdinalIgnoreCase);

                // Create validation response
                var validationMessage = new FileValidationMessage
                {
                    FilePath = filePath,
                    IsValid = isValid,
                    ExpectedHash = fileInfo.Sha256,
                    ClientHash = clientHash
                };

                Console.WriteLine($"Hash validation result: {(isValid ? "Valid" : "Invalid")}");

                var response = CreateResponse(MessageType.FileValidation, validationMessage);

                return (true, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating client file: {ex.Message}");
                return (false, CreateErrorResponse(MessageType.Error, "Internal server error"));
            }
        }

        /// <summary>
        /// Gets a manifest of all files in a version
        /// </summary>
        public async Task<(bool Success, byte[] ResponseData)> GetFileManifest(string versionName)
        {
            try
            {
                Console.WriteLine($"Generating file manifest for version {versionName}");

                // Get all files for the version from the database
                var files = await _dbService.GetFilesForVersionAsync(versionName);
                if (files == null || files.Count == 0)
                {
                    var errorMessage = $"No files found for version {versionName}";
                    Console.WriteLine(errorMessage);
                    return (false, CreateErrorResponse(MessageType.Error, errorMessage));
                }

                // Calculate total size
                long totalSizeBytes = 0;
                var manifestEntries = new List<FileManifestEntry>();

                foreach (var file in files)
                {
                    // Parse size string to get approximate byte count
                    string sizeValue = file.FileSize.Split(' ')[0];
                    string sizeUnit = file.FileSize.Split(' ')[1];

                    decimal size = decimal.Parse(sizeValue);
                    long sizeInBytes = ConvertToBytes(size, sizeUnit);
                    totalSizeBytes += sizeInBytes;

                    // Add entry to manifest
                    manifestEntries.Add(new FileManifestEntry
                    {
                        FileName = file.FileName,
                        RelativePath = file.FilePath,
                        FileSize = file.FileSize,
                        Sha256Hash = file.Sha256
                    });
                }

                // Create the manifest message
                var manifestMessage = new FileManifestMessage
                {
                    Version = versionName,
                    FileCount = files.Count,
                    TotalSize = FormatFileSize(totalSizeBytes),
                    Files = manifestEntries
                };

                // Create response
                var response = CreateResponse(MessageType.FileManifest, manifestMessage);

                return (true, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating file manifest: {ex.Message}");
                return (false, CreateErrorResponse(MessageType.Error, "Error generating file manifest"));
            }
        }

        private static byte[] CreateResponse<T>(MessageType messageType, T data)
        {
            var message = new BaseMessage
            {
                Type = messageType,
                TimeStamp = DateTime.UtcNow,
                Data = JsonSerializer.Serialize(data, JsonHelpers.JsonFormatter)
            };

            string messageJson = JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter);
            return System.Text.Encoding.UTF8.GetBytes(messageJson);
        }

        private static byte[] CreateErrorResponse(MessageType messageType, string errorMessage)
        {
            var errorData = new ErrorMessage { Message = errorMessage };
            return CreateResponse(messageType, errorData);
        }

        private long ConvertToBytes(decimal size, string unit)
        {
            return unit.ToUpperInvariant() switch
            {
                "B" => (long)size,
                "KB" => (long)(size * 1024),
                "MB" => (long)(size * 1024 * 1024),
                "GB" => (long)(size * 1024 * 1024 * 1024),
                "TB" => (long)(size * 1024 * 1024 * 1024 * 1024),
                _ => (long)size
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal size = bytes;

            while (Math.Round(size / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                size /= 1024;
                counter++;
            }

            return $"{size:n2} {suffixes[counter]}";
        }
    }
}
