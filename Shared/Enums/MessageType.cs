namespace Shared.Enums
{
    public enum MessageType
    {
        NewUpdate = 0,
        Error = 1,
        VersionInfo = 2,
        RequestUpdate = 3,
        RequestFile = 4,
        FileTransfer = 5,
        ValidateFile = 6,
        FileValidation = 7,
        Terminate = 8,
        FileManifest = 9,
    }
}
