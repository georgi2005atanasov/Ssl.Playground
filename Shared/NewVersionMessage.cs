namespace Shared
{
    public class NewVersionMessage : IMessageData
    {
        public string Version { get; set; } = string.Empty;
    }
}
