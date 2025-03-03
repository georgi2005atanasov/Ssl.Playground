namespace SslClient.Clients
{
    using Microsoft.Extensions.Configuration;
    using NetCoreServer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Shared;
    using Shared.Enums;
    using SslClient.Extensions;
    using SslClient.Models.Internal;
    using SslClient.Utils;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Text;

    public class UpdateClient : NetCoreServer.SslClient
    {
        private bool _stop;
        private static ClientConfiguration _clientConfiguration = new();
        private readonly string _installationDirectory;

        private readonly byte[] _encryptionKey;
        private readonly byte[] _encryptionIV;
        private StringBuilder _jsonBuffer = new StringBuilder();

        public UpdateClient(
            SslContext context, 
            string address, 
            int port, 
            string installationDirectory,
            byte[] encryptionKey,
            byte[] encryptionIV
        )
            : base(context, address, port)
        {
            _installationDirectory = installationDirectory;
            _encryptionKey = encryptionKey;
            _encryptionIV = encryptionIV;

            // Create installation directory if it doesn't exist
            if (!Directory.Exists(_installationDirectory))
            {
                Directory.CreateDirectory(_installationDirectory);
            }
        }

        protected override void OnConnected()
        {
            Console.WriteLine("TCP Connection established.");
        }

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        /// <summary>
        /// When the Ssl Connection is established, a message in JSON format
        /// is sent to the server about the client's latest version
        /// </summary>
        protected override void OnHandshaked()
        {
            Console.WriteLine($"SSL client handshaked a new session with Id {Id}");

            var message = new BaseMessage
            {
                TimeStamp = DateTime.UtcNow,
                Type = Shared.Enums.MessageType.VersionInfo,
                Data = System.Text.Json.JsonSerializer.Serialize(ClientHelpers.GetCurrentVersion()),
            };

            this.SendAsync(System.Text.Json.JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter));
        }

        protected override void OnDisconnected()
        {
            _stop = true;
            Console.WriteLine($"Chat SSL client disconnected a session with Id {Id}");
            // Wait for a while...
            Thread.Sleep(1000);

            // Try to connect again
            if (!_stop)
                ConnectAsync();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                // Convert buffer to string and append to our buffer
                string dataAsString = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
                _jsonBuffer.Append(dataAsString);

                // Process any complete messages
                ProcessBufferedMessages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing received data: {ex.Message}");
            }
        }

        private void ProcessBufferedMessages()
        {
            string content = _jsonBuffer.ToString();

            // Look for complete JSON objects that start with { and end with }}
            int position = 0;
            while (position < content.Length)
            {
                // Find the start of a JSON object
                int startPos = content.IndexOf('{', position);
                if (startPos == -1) break;

                // Find the matching end by counting braces
                int depth = 0;
                int endPos = startPos;
                bool complete = false;

                // Scan through the content
                for (int i = startPos; i < content.Length; i++)
                {
                    char c = content[i];
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            endPos = i + 1;
                            complete = true;
                            break;
                        }
                    }
                }

                if (complete)
                {
                    string jsonMessage = content.Substring(startPos, endPos - startPos);

                    if (IsValidJson(jsonMessage))
                    {
                        Task.Run(async () => await ProcessMessageAsync(jsonMessage));

                        position = endPos;
                    }
                    else
                    {
                        position = startPos + 1;
                        Console.WriteLine("Found invalid JSON pattern, skipping");
                    }
                }
                else
                {
                    Console.WriteLine("Buffering incomplete JSON message");
                    break;
                }
            }

            if (position > 0)
            {
                _jsonBuffer.Remove(0, position);
            }

            if (_jsonBuffer.Length > 10_000_000) // 10 MB limit
            {
                Console.WriteLine("Buffer exceeded size limit, clearing");
                _jsonBuffer.Clear();
            }
        }

        private bool IsValidJson(string json)
        {
            try
            {
                JToken.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL client caught an error with code {error}");
        }

        public static UpdateClient WithConfiguration(Func<ConfigurationBuilder, ClientConfiguration?> configuration)
        {
            var configBuilder = new ConfigurationBuilder();

            _clientConfiguration = configuration(configBuilder) ?? throw new NullReferenceException("Invalid Configuration");

            var context = new SslContext(SslProtocols.Tls13, (sender, certificate, chain, sslPolicyErrors) => true);

            var client = new UpdateClient(
                context,
                _clientConfiguration.Server.IpAddress,
                _clientConfiguration.Server.Port,
                DirectoriesConstants.INSTALLED_VERSIONS,
                HexStringToByteArray(_clientConfiguration.Secrets!.EncryptionKey),
                HexStringToByteArray(_clientConfiguration.Secrets.EncryptionIV)
            );

            return client;
        }

        private async Task ProcessMessageAsync(string messageJson)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<BaseMessage>(messageJson);

                // check for invalid message format
                if (message == null)
                    return;

                switch (message.Type)
                {
                    case MessageType.NewUpdate:
                        HandleNewVersion(message);
                        break;

                    case MessageType.VersionInfo:
                        HandleVersionInfo(message);
                        break;

                    case MessageType.FileManifest:
                        await HandleFileManifestAsync(message);
                        break;

                    case MessageType.FileTransfer:
                        await HandleFileResponseAsync(message);
                        break;

                    case MessageType.ValidateFile:
                        HandleValidationResponse(message);
                        break;

                    case MessageType.Error:
                        HandleError(message);
                        break;

                    default:
                        Console.WriteLine($"Received unsupported message type: {message.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private void HandleNewVersion(BaseMessage message)
        {
            try
            {
                var newVersion = JsonConvert.DeserializeObject<NewVersionMessage>(message.Data);

                Console.WriteLine($"In order to continue using this program, you need to download the latest version available, which is {newVersion?.Version}");

                Console.Write($"Proceed? Yes/No ");
                var isDownloading = Console.ReadLine();

                if (isDownloading != null && isDownloading.ToLower() == "yes")
                {
                    RequestFileManifest(newVersion!.Version);
                }
                else
                {
                    UpdateClientExtensions.CancellationTokenSource.Cancel();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling new version: {ex.Message}");
            }
        }

        private void HandleVersionInfo(BaseMessage message)
        {
            try
            {
                var versionInfo = JsonConvert.DeserializeObject<VersionInfoMessage>(message.Data);

                if (versionInfo == null)
                {
                    Console.WriteLine("Received invalid version info data");
                    return;
                }

                Console.WriteLine($"Server version: {versionInfo.Version}");

                // Compare with local version and decide if update is needed
                string localVersion = ClientHelpers.GetCurrentVersion().CurrentVersion;

                if (localVersion != versionInfo.Version)
                {
                    RequestFileManifest(localVersion);
                }
                else
                {
                    Console.WriteLine("Client is up to date");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling version info: {ex.Message}");
            }
        }

        private async Task HandleFileManifestAsync(BaseMessage message)
        {
            try
            {
                var fileManifest = JsonConvert.DeserializeObject<FileManifestMessage>(message.Data);

                if (fileManifest == null || fileManifest.Files == null || fileManifest.Files.Count == 0)
                {
                    Console.WriteLine("Received empty or invalid file manifest");
                    return;
                }

                Console.WriteLine($"Received file manifest for version {fileManifest.Version} with {fileManifest.Files.Count} files");

                // Process each file in the manifest
                foreach (var file in fileManifest.Files)
                {
                    Console.WriteLine($"Processing file: {file.FilePath}");

                    // Check if file already exists and has correct hash
                    string fullPath = Path.Combine(_installationDirectory, file.FilePath);
                    bool needsDownload = true;

                    if (File.Exists(fullPath))
                    {
                        string localHash = await CalculateFileHashAsync(fullPath);

                        // Validate the file with the server
                        await ValidateFileWithServerAsync(fileManifest.Version, file.FilePath, localHash);

                        // If validation fails, the response will trigger a download
                        // So we can skip the explicit download request here
                        needsDownload = false;
                    }

                    if (needsDownload)
                    {
                        // Request the file from server
                        RequestFile(fileManifest.Version, file.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file manifest: {ex.Message}");
            }
        }

        private async Task HandleFileResponseAsync(BaseMessage message)
        {
            try
            {
                var fileResponse = JsonConvert.DeserializeObject<FileTransferServerMessage>(message.Data);

                if (fileResponse == null || string.IsNullOrEmpty(fileResponse.FileContent))
                    return;

                Console.WriteLine($"Received file: {fileResponse.FilePath}");

                // Create directory structure if needed
                string fullPath = Path.Combine(_installationDirectory, fileResponse.Version, fileResponse.FilePath);
                string directory = Path.GetDirectoryName(fullPath) ?? "";

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                byte[] fileBytes = Convert.FromBase64String(fileResponse.FileContent);
                await File.WriteAllBytesAsync(fullPath, fileBytes);

                Console.WriteLine($"File saved: {fullPath}");

                // Validate the installed file
                string fileHash = await CalculateFileHashAsync(fullPath);
                await ValidateFileWithServerAsync(fileResponse.Version, fileResponse.FilePath, fileHash);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file response: {ex.Message}");
            }
        }

        private void HandleValidationResponse(BaseMessage message)
        {
            try
            {
                var validationResponse = JsonConvert.DeserializeObject<FileValidationServerMessage>(message.Data);

                if (validationResponse == null)
                {
                    Console.WriteLine("Received invalid validation response");
                    return;
                }

                if (validationResponse.IsValid)
                {
                    Console.WriteLine($"File validation successful: {validationResponse.FilePath}");
                }
                else
                {
                    Console.WriteLine($"File validation failed for {validationResponse.FilePath}, requesting download");

                    // Request file download since validation failed
                    RequestFile(validationResponse.Version, validationResponse.FilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling validation response: {ex.Message}");
            }
        }

        private void HandleError(BaseMessage message)
        {
            try
            {
                var errorMessage = JsonConvert.DeserializeObject<ErrorMessage>(message.Data);

                if (errorMessage == null)
                {
                    Console.WriteLine("Received invalid error message");
                    return;
                }

                Console.WriteLine($"Server error: {errorMessage.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling error message: {ex.Message}");
            }
        }

        private void RequestFileManifest(string currentVersion)
        {
            try
            {
                var versionInfo = new VersionInfoMessage
                {
                    Version = currentVersion
                };

                var message = new BaseMessage
                {
                    TimeStamp = DateTime.UtcNow,
                    Type = MessageType.VersionInfo,
                    Data = System.Text.Json.JsonSerializer.Serialize(versionInfo, JsonHelpers.JsonFormatter)
                };

                string messageJson = System.Text.Json.JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter);
                SendAsync(messageJson);

                Console.WriteLine($"Requested file manifest. Current version {currentVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting file manifest: {ex.Message}");
            }
        }

        private void RequestFile(string version, string filePath)
        {
            try
            {
                var fileRequest = new FileRequestMessage
                {
                    Version = version,
                    FilePath = filePath
                };

                var message = new BaseMessage
                {
                    TimeStamp = DateTime.UtcNow,
                    Type = MessageType.RequestFile,
                    Data = System.Text.Json.JsonSerializer.Serialize(fileRequest, JsonHelpers.JsonFormatter)
                };

                string messageJson = System.Text.Json.JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter);
                SendAsync(messageJson);

                Console.WriteLine($"Requested file: {filePath}, version: {version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting file: {ex.Message}");
            }
        }

        private async Task ValidateFileWithServerAsync(string version, string filePath, string hash)
        {
            try
            {
                var validationRequest = new FileValidationClientMessage
                {
                    Version = version,
                    FilePath = filePath,
                    ClientHash = hash
                };

                var message = new BaseMessage
                {
                    TimeStamp = DateTime.UtcNow,
                    Type = MessageType.ValidateFile,
                    Data = AesUtils.EncryptString(JsonConvert.SerializeObject(validationRequest), _encryptionKey, _encryptionIV)
                };

                string messageJson = JsonConvert.SerializeObject(message);
                SendAsync(Encoding.UTF8.GetBytes(messageJson));

                Console.WriteLine($"Sent validation request for file: {filePath}, hash: {hash}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending validation request: {ex.Message}");
            }

            // Give a small delay to let server respond
            await Task.Delay(100);
        }

        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);

            byte[] hashBytes = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            if (hex.StartsWith("0x"))
                hex = hex.Substring(2);

            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even number of characters");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        // Helper method to convert byte array to hex string
        private static string ByteArrayToHexString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
