namespace SslServer.Services
{
    using NetCoreServer;
    using Newtonsoft.Json;
    using Shared;
    using Shared.Enums;
    using SslServer.Contracts;
    using SslServer.Utils;
    using System.Net.Sockets;
    using System.Text;

    /// <summary>
    /// We must skip ex.Message in the catch block. Now, for testing purposes it is being added.
    /// </summary>
    public class UpdateSession : SslSession
    {
        private readonly IVersionManager _versionManager;
        private readonly SecureFileTransferService _fileTransferService;
        private readonly UpdateServer _updateServer;
        private StringBuilder _jsonBuffer = new StringBuilder();

        public UpdateSession(
            UpdateServer server,
            IVersionManager versionManager,
            SecureFileTransferService fileTransferHandler)
            : base(server)
        {
            _versionManager = versionManager;
            _fileTransferService = fileTransferHandler;
            _updateServer = server;
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"SSL session with Id {Id} connected!");
        }

        protected override void OnHandshaked()
        {
            Console.WriteLine($"SSL session with Id {Id} handshaked!");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"SSL session with Id {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                string dataAsString = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
                _jsonBuffer.Append(dataAsString);

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
            int position = 0;

            while (position < content.Length)
            {
                // Find the start of a JSON object
                int startPos = content.IndexOf('{', position);
                if (startPos == -1) break;

                // Track brace depth to find matching end brace
                int depth = 0;
                int endPos = -1;

                for (int i = startPos; i < content.Length; i++)
                {
                    char c = content[i];
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            endPos = i;
                            break;
                        }
                    }
                }

                if (endPos != -1)
                {
                    // We found a complete JSON object
                    string jsonMessage = content.Substring(startPos, endPos - startPos + 1);

                    try
                    {
                        // Process this message
                        var task = ProcessMessageAsync(jsonMessage);
                        // We don't await here because we want to continue processing the buffer
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing message: {ex.Message}");
                    }

                    // Move position past this object
                    position = endPos + 1;
                }
                else
                {
                    // Incomplete object, keep in buffer
                    break;
                }
            }

            // Remove processed content from buffer
            if (position > 0)
            {
                _jsonBuffer.Remove(0, position);
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"SSL session caught an error with code {error}");
        }

        private async Task ProcessMessageAsync(string messageJson)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<BaseMessage>(messageJson);

                if (message == null)
                {
                    SendError("Invalid message format");
                    return;
                }

                switch (message.Type)
                {
                    case MessageType.VersionInfo:
                        await CheckVersion(message);
                        break;

                    case MessageType.RequestFile:
                        await HandleFileRequestAsync(message);
                        break;

                    case MessageType.ValidateFile:
                        await HandleFileValidationAsync(message);
                        break;

                    default:
                        SendError($"Unsupported message type: {message.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                SendError(string.Format(ErrorsConstants.ErrorProcessingMessage, ex.Message));
            }
        }

        private void SendVersionInfo()
        {
            try
            {
                string currentVersion = _versionManager.GetCurrentVersion();

                var versionInfo = new VersionInfoMessage
                {
                    Version = currentVersion,
                };

                var message = new BaseMessage
                {
                    Type = MessageType.VersionInfo,
                    TimeStamp = DateTime.UtcNow,
                    Data = System.Text.Json.JsonSerializer.Serialize(versionInfo, JsonHelpers.JsonFormatter)
                };

                string messageJson = System.Text.Json.JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter);
                SendAsync(Encoding.UTF8.GetBytes(messageJson));

                Console.WriteLine($"Sent version info: {messageJson}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending version info: {ex.Message}");
            }
        }


        private async Task CheckVersion(BaseMessage message)
        {
            try
            {
                // The data should be the version name
                var versionInfo = JsonConvert.DeserializeObject<VersionInfoMessage>(message.Data);

                if (versionInfo!.Version == _versionManager.GetCurrentVersion())
                {
                    SendVersionInfo();
                    return;
                }

                if (string.IsNullOrEmpty(versionInfo!.Version))
                    versionInfo.Version = _versionManager.GetCurrentVersion();

                var (success, responseData) = await _fileTransferService
                    .GetFileManifest(versionInfo.Version);

                SendAsync(responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR - {nameof(CheckVersion)}: {ex.Message}");
                SendError(ErrorsConstants.ErrorHandlingFileManifestRequest);
            }
        }

        private async Task HandleFileRequestAsync(BaseMessage message)
        {
            try
            {
                FileRequestMessage? fileRequest = System.Text.Json.JsonSerializer.Deserialize<FileRequestMessage>(
                    message.Data, JsonHelpers.JsonFormatter);

                if (fileRequest == null)
                {
                    SendError("Invalid file request format");
                    return;
                }

                Console.WriteLine($"Handling file request for {fileRequest.FilePath} in version {fileRequest.Version}");

                var (success, responseData) = await _fileTransferService.HandleFileRequest(
                    fileRequest.Version, fileRequest.FilePath);

                SendAsync(responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file request: {ex.Message}");
                SendError("Error handling file request");
            }
        }

        /// <summary>
        /// Validates client file - if the file is corrupted or the version is outdated,
        /// the server will send the new file
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task HandleFileValidationAsync(BaseMessage message)
        {
            try
            {
                var decryptedData = AesUtils.DecryptString(
                    message.Data,
                    Converters.HexStringToByteArray(_updateServer.ServerConfiguration.Secrets!.EncryptionKey),
                    Converters.HexStringToByteArray(_updateServer.ServerConfiguration.Secrets!.EncryptionIV)
                );

                FileValidationClientMessage? validationRequest = JsonConvert
                    .DeserializeObject<FileValidationClientMessage>(decryptedData);

                if (validationRequest == null)
                {
                    SendError(ErrorsConstants.InvalidFileValidationRequestFormat);
                    return;
                }

                var (success, responseData) = await _fileTransferService.ValidateClientFile(
                    validationRequest.Version, validationRequest.FilePath, validationRequest.ClientHash);

                SendAsync(responseData);
            }
            catch (Exception ex)
            {
                SendError(string.Format(ErrorsConstants.ErrorHandlingFileValidation, ex.Message));
            }
        }

        private void SendError(string errorMessage)
        {
            try
            {
                var errorData = new ErrorMessage { Message = errorMessage };

                var message = new BaseMessage
                {
                    Type = MessageType.Error,
                    TimeStamp = DateTime.UtcNow,
                    Data = System.Text.Json.JsonSerializer.Serialize(errorData, JsonHelpers.JsonFormatter)
                };

                string messageJson = System.Text.Json.JsonSerializer.Serialize(message, JsonHelpers.JsonFormatter);
                SendAsync(Encoding.UTF8.GetBytes(messageJson));

                Console.WriteLine($"Sent error message: {errorMessage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending error message: {ex.Message}");
            }
        }
    }
}