namespace SslServer.Contracts
{
    public interface ISecureFileTransferService
    {
        Task<(bool Success, string ResponseData)> HandleFileRequest(string versionName, string filePath);

        Task<(bool Success, string ResponseData)> ValidateClientFile(string versionName, string filePath, string clientHash);

        Task<(bool Success, string ResponseData)> GetFileManifest(string versionName);
    }
}
