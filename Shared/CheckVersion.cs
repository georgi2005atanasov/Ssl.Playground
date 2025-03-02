namespace Shared
{
    public class CheckVersion : IMessageData
    {
        public string CurrentVersion { get; set; } = string.Empty;
    }
}
