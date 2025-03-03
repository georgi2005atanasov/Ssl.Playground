namespace SslServer.Services
{
    using NetCoreServer;
    using Shared.Enums;
    using Shared;
    using SslServer.Utils;
    using System.Text.Json;
    using SslServer.Contracts;
    using SslServer.Data;

    public class VersionManager : IVersionManager
    {
        private static string _currentVersion = string.Empty;
        private FileCache? _cache = new FileCache();
        private Action<string>? _notificationCallback;
        private readonly IDbService _dbService;

        public VersionManager(IDbService dbService)
        {
            this._dbService = dbService;
        }

        public string GetCurrentVersion() => _currentVersion;

        public void SetNotificationCallback(Action<string> callback)
        {
            _notificationCallback = callback;
        }

        public void TrackVersions()
        {
            string versionsPath = Path.Combine(Directory.GetCurrentDirectory(), DirectoriesConstants.VERSIONS);
            Console.WriteLine($"Monitoring folder: {versionsPath}");

            SetupFileSystemWatcher(versionsPath);
            LoadExistingVersion(versionsPath);
        }

        private void SetupFileSystemWatcher(string versionsPath)
        {
            var watcher = new FileSystemWatcher
            {
                Path = versionsPath,
                NotifyFilter = NotifyFilters.DirectoryName,
                Filter = "*",
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            // Handle new directories
            watcher.Created += async (sender, e) =>
            {
                try
                {
                    if (Directory.Exists(e.FullPath))
                    {
                        Console.WriteLine($"New version folder detected: {e.Name}");
                        await UpdateVersionCacheAsync(e.FullPath, e.Name ?? "New Version");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling new version: {ex.Message}");
                }
            };
        }

        private void LoadExistingVersion(string versionsPath)
        {
            var existingVersions = new DirectoryInfo(versionsPath)
                .GetDirectories();

            if (existingVersions.Length > 0)
            {
                var latestVersion = existingVersions
                    .OrderBy(f => f.LastWriteTime)
                    .Last();

                string versionName = Path.GetFileName(latestVersion.FullName);
                Console.WriteLine($"Loading latest version: {versionName}");
                // Use Task.Run to avoid blocking since we're in a synchronous method
                Task.Run(() => UpdateVersionCacheAsync(latestVersion.FullName, versionName)).Wait();
            }
            else
            {
                Console.WriteLine("No existing versions found");
            }
        }

        private async Task UpdateVersionCacheAsync(string versionPath, string versionName)
        {
            try
            {
                // Remove previous version if it exists
                if (!string.IsNullOrEmpty(_currentVersion))
                {
                    Console.WriteLine($"Removing previous version: {_currentVersion}");
                    _cache!.RemovePath(_currentVersion); // Use the version name, not the path
                }

                _currentVersion = versionName;

                // Save the version in the database
                await _dbService.SaveVersionAsync(versionName, DateTime.UtcNow);

                // Process and save all files in the version directory
                await ProcessVersionFilesAsync(versionPath, versionName);

                // Cache handler
                FileCache.InsertHandler customHandler = (cache, key, value, timeout) =>
                {
                    return cache.Add(key, value, timeout);
                };

                // Insert the version path
                bool success = _cache!.InsertPath(
                    path: versionPath,
                    prefix: $"/version/{versionName}",
                    filter: "*.*",
                    timeout: TimeSpan.Zero,
                    handler: customHandler
                );

                if (_notificationCallback != null)
                {
                    _notificationCallback(JsonSerializer.Serialize(new BaseMessage
                    {
                        Type = MessageType.NewUpdate,
                        TimeStamp = DateTime.UtcNow,
                        Data = JsonSerializer.Serialize(new NewVersionMessage
                        {
                            Version = versionName,
                        }, JsonHelpers.JsonFormatter)
                    }, JsonHelpers.JsonFormatter));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating version cache: {ex.Message}");
            }
        }

        private async Task ProcessVersionFilesAsync(string versionPath, string versionName)
        {
            try
            {
                var files = Directory.GetFiles(versionPath, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"Processing {files.Length} files in version {versionName}");

                foreach (var filePath in files)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        string relativePath = filePath.Substring(versionPath.Length).TrimStart('\\', '/');
                        string sha256 = FileHashUtility.CalculateSha256(filePath);
                        string fileSize = FileHashUtility.GetFileSize(filePath);

                        var fileRecord = new Data.Models.File
                        {
                            FileName = fileName,
                            FilePath = relativePath,
                            Sha256 = sha256,
                            FileSize = fileSize,
                            UploadedOn = DateTime.UtcNow
                        };

                        await _dbService.SaveFileAsync(fileRecord, versionName);
                        Console.WriteLine($"File {relativePath} saved to database with hash {sha256}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing version files: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a file against its stored hash in the database
        /// </summary>
        public async Task<bool> ValidateFileHash(string filePath, string versionName)
        {
            try
            {
                // Get the file record from the database
                var fileRecord = await _dbService.GetFileAsync(filePath, versionName);
                if (fileRecord == null)
                {
                    Console.WriteLine($"File {filePath} not found in database for version {versionName}");
                    return false;
                }

                // Get the full path to the file
                string fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Versions",
                    versionName,
                    filePath.TrimStart('\\', '/')
                );

                // Validate the file hash
                bool isValid = FileHashUtility.ValidateFileHash(fullPath, fileRecord.Sha256);

                if (!isValid)
                    Console.WriteLine($"Hash validation failed for {filePath}");

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating file hash: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _cache?.Dispose();
            _cache = null;
        }
    }
}
