namespace SslServer.Data
{
    /// <summary>
    /// Represents a database parameter
    /// </summary>
    public class DbParameter
    {
        public string Name { get; set; } = string.Empty;

        public object? Value { get; set; }

        public DbParameter(string name, object? value)
        {
            Name = name;
            Value = value;
        }
    }
}
