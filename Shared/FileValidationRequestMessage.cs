﻿namespace Shared
{
    /// <summary>
    /// Message for file validation requests
    /// </summary>
    public class FileValidationRequestMessage
    {
        public string Version { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string ClientHash { get; set; } = string.Empty;
    }
}
