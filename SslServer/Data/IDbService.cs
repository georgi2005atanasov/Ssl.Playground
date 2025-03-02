namespace SslServer.Data
{
    using System.Data;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for database operations
    /// </summary>
    public interface IDbService
    {
        /// <summary>
        /// Executes a command against the database and returns the number of rows affected
        /// </summary>
        Task<int> ExecuteNonQueryAsync(string commandText, CommandType commandType = CommandType.Text, params DbParameter[] parameters);

        /// <summary>
        /// Executes a command against the database and returns the first column of the first row
        /// </summary>
        Task<T> ExecuteScalarAsync<T>(string commandText, CommandType commandType = CommandType.Text, params DbParameter[] parameters);

        /// <summary>
        /// Executes a command against the database and returns a data reader
        /// </summary>
        Task<IDataReader> ExecuteReaderAsync(string commandText, CommandType commandType = CommandType.Text, params DbParameter[] parameters);

        /// <summary>
        /// Saves a new file record to the database
        /// </summary>
        Task SaveFileAsync(Data.Models.File file, string versionName);

        /// <summary>
        /// Gets a file record from the database by file path and version
        /// </summary>
        Task<Data.Models.File?> GetFileAsync(string filePath, string versionName);

        /// <summary>
        /// Saves a new version record to the database
        /// </summary>
        Task SaveVersionAsync(string versionName, DateTime uploadedOn);

        /// <summary>
        /// Gets all files for a specific version
        /// </summary>
        Task<List<Data.Models.File>> GetFilesForVersionAsync(string versionName);
    }
}
