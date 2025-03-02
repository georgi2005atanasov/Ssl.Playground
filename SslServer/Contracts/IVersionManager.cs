namespace SslServer.Contracts
{
    internal interface IVersionManager : IDisposable
    {
        void SetNotificationCallback(Action<string> callback);

        void TrackVersions();

        string GetCurrentVersion();

        Task<bool> ValidateFileHash(string filePath, string versionName);
    }
}
