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

    internal class UpdateSession : SslSession
    {
        private readonly IVersionManager _versionManager;
        private readonly SecureFileTransferService _fileTransferHandler;

        public UpdateSession(
            SslServer server,
            IVersionManager versionManager,
            SecureFileTransferService fileTransferHandler)
            : base(server)
        {
            _versionManager = versionManager;
            _fileTransferHandler = fileTransferHandler;
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} connected!");
        }

        protected override void OnHandshaked()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} handshaked!");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat SSL session with Id {Id} disconnected!");
        }

        /// <summary>
        /// For this demonstration I am gonna use ugly switch statement
        /// I think there is definately a better approach (MessageProcessor)
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            try
            {
                var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);

                Task.Run(async () => await ProcessMessageAsync(message));
            }
            catch (Exception ex)
            {
                // TODO: logs
                Console.WriteLine(ex.Message);
            }
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat SSL session caught an error with code {error}");
        }

        private async Task ProcessMessageAsync(string messageJson)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<BaseMessage>(messageJson);

                if (message == null /*|| message.Type == MessageType.Terminate*/)
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
                Console.WriteLine($"Error processing message: {ex.Message}");
                SendError("Error processing message");
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

                //var (success, responseData) = await _fileTransferHandler.GetFileManifest(versionInfo.Version);

                //SendAsync(responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file manifest request: {ex.Message}");
                SendError("Error handling file manifest request");
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

                // Handle the file request using the file transfer handler
                var (success, responseData) = await _fileTransferHandler.HandleFileRequest(
                    fileRequest.Version, fileRequest.FilePath);


                SendAsync(responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file request: {ex.Message}");
                SendError("Error handling file request");
            }
        }

        private async Task HandleFileValidationAsync(BaseMessage message)
        {
            try
            {
                FileValidationRequestMessage? validationRequest = JsonConvert
                    .DeserializeObject<FileValidationRequestMessage>(message.Data);

                if (validationRequest == null)
                {
                    SendError("Invalid file validation request format");
                    return;
                }

                Console.WriteLine($"Handling file validation for {validationRequest.FilePath} with hash {validationRequest.ClientHash}");

                // Handle the validation request using the file transfer handler
                var (success, responseData) = await _fileTransferHandler.ValidateClientFile(
                    validationRequest.Version, validationRequest.FilePath, validationRequest.ClientHash);

                SendAsync(responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling file validation: {ex.Message}");
                SendError("Error handling file validation");
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