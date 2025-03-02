namespace Shared
{
    using Shared.Enums;

    public class BaseMessage
    {
        public MessageType Type { get; set; }

        public DateTime TimeStamp { get; set; }

        public string Data { get; set; } = string.Empty;
    }
}
