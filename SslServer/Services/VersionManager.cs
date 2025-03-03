using System.Text.Json;
using NetCoreServer;
using Shared;
using Shared.Enums;
using SslServer.Contracts;
using SslServer.Data;
using SslServer.Utils;

namespace SslServer.Services
{
    public class VersionManager : IVersionManager, IDisposable
    {
        private static string _currentVersion = string.Empty;
        private FileCache? _cache = new FileCache();
        private Action<string>? _notificationCallback;
        private readonly IDbService _dbService;
        private FileSystemWatcher? _watcher;
        private readonly Dictionary<string, CancellationTokenSource> _debounceTokens = new Dictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);

        public VersionManager(IDbService dbService)
        {
            _dbService = dbService;
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
            _watcher = new FileSystemWatcher
            {
                Path = versionsPath,
                NotifyFilter = NotifyFilters.DirectoryName,
                Filter = "*",
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            // Use the debounce method for Created and Renamed events.
            _watcher.Created += async (sender, e) => await DebounceAndHandleFolder(e.FullPath, e.Name!);
            _watcher.Renamed += async (sender, e) => await DebounceAndHandleFolder(e.FullPath, e.Name!);
        }

        private async Task DebounceAndHandleFolder(string folderPath, string folderName)
        {
            // Cancel any previous pending processing for this folder.
            if (_debounceTokens.TryGetValue(folderPath, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }

            // Create a new cancellation token for the debounce.
            var cts = new CancellationTokenSource();
            _debounceTokens[folderPath] = cts;
            try
            {
                // Wait 5 seconds to allow the folder copy to complete.
                await Task.Delay(5000, cts.Token);
                if (Directory.Exists(folderPath))
                {
                    Console.WriteLine($"New version folder detected: {folderName}");
                    await UpdateVersionCacheAsync(folderPath, string.IsNullOrEmpty(folderName) ? "New Version" : folderName);
                }
            }
            catch (TaskCanceledException)
            {
                // A new event has reset the timer.
            }
            finally
            {
                _debounceTokens.Remove(folderPath);
                cts.Dispose();
            }
        }

        private void LoadExistingVersion(string versionsPath)
        {
            var existingVersions = new DirectoryInfo(versionsPath).GetDirectories();

            if (existingVersions.Length > 0)
            {
                // Use CreationTime to find the newest folder.
                var latestVersion = existingVersions.OrderBy(f => f.CreationTime).Last();
                string versionName = latestVersion.Name;
                Console.WriteLine($"Loading latest version: {versionName}");
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
                // Remove previous version if it exists.
                if (!string.IsNullOrEmpty(_currentVersion))
                {
                    Console.WriteLine($"Removing previous version: {_currentVersion}");
                    _cache!.RemovePath(_currentVersion);
                }

                _currentVersion = versionName;

                // Save the version in the database.
                await _dbService.SaveVersionAsync(versionName, DateTime.UtcNow);

                // Process and save all files in the version directory.
                await ProcessVersionFilesAsync(versionPath, versionName);

                // Cache handler.
                FileCache.InsertHandler customHandler = (cache, key, value, timeout) =>
                {
                    return cache.Add(key, value, timeout);
                };

                // Insert the version path.
                bool success = _cache!.InsertPath(
                    path: versionPath,
                    prefix: $"/version/{versionName}",
                    filter: "*.*",
                    timeout: TimeSpan.Zero,
                    handler: customHandler
                );

                // Notify using callback.
                _notificationCallback?.Invoke(JsonSerializer.Serialize(new BaseMessage
                {
                    Type = MessageType.NewUpdate,
                    TimeStamp = DateTime.UtcNow,
                    Data = JsonSerializer.Serialize(new NewVersionMessage
                    {
                        Version = versionName,
                    }, JsonHelpers.JsonFormatter)
                }, JsonHelpers.JsonFormatter));
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

        public async Task<bool> ValidateFileHash(string filePath, string versionName)
        {
            try
            {
                var fileRecord = await _dbService.GetFileAsync(filePath, versionName);
                if (fileRecord == null)
                {
                    Console.WriteLine($"File {filePath} not found in database for version {versionName}");
                    return false;
                }

                string fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Versions",
                    versionName,
                    filePath.TrimStart('\\', '/')
                );

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

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            foreach (var cts in _debounceTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _debounceTokens.Clear();
        }
    }
}
