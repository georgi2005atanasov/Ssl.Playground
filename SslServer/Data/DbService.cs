﻿namespace SslServer.Data
{
    namespace SslServer.Data
    {
        using System;
        using System.Data;
        using System.Threading.Tasks;
        using Microsoft.Data.SqlClient;
        using global::SslServer.Models.Internal;

        public class DbService(ServerConfiguration configuration) : IDbService, IDisposable
        {
            private readonly string _connectionString = configuration.DbConnection
                    ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found");

            private async Task<SqlConnection> CreateAndOpenConnectionAsync()
            {
                var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                return connection;
            }

            public async Task<int> ExecuteNonQueryAsync(string commandText, CommandType commandType = CommandType.Text, params DbParameter[] parameters)
            {
                using var connection = await CreateAndOpenConnectionAsync();
                using var command = CreateCommand(connection, commandText, commandType, parameters);

                return await command.ExecuteNonQueryAsync();
            }

            public async Task<T> ExecuteScalarAsync<T>(string commandText, CommandType commandType = CommandType.Text, params DbParameter[] parameters)
            {
                using var connection = await CreateAndOpenConnectionAsync();
                using var command = CreateCommand(connection, commandText, commandType, parameters);

                var result = await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                    return default!;

                return (T)Convert.ChangeType(result, typeof(T));
            }

            public async Task<IDataReader> ExecuteReaderAsync(string commandText, CommandType commandType = CommandType.Text, params DbParameter[] parameters)
            {
                var connection = await CreateAndOpenConnectionAsync();
                var command = CreateCommand(connection, commandText, commandType, parameters);

                try
                {
                    return await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            }

            public async Task SaveFileAsync(Models.File file, string versionName)
            {
                const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM Files WHERE FilePath = @FilePath AND VersionName = @VersionName)
                BEGIN
                    INSERT INTO Files (FileName, FilePath, Sha256, FileSize, UploadedOn, VersionName)
                    VALUES (@FileName, @FilePath, @Sha256, @FileSize, @UploadedOn, @VersionName)
                END
                ELSE
                BEGIN
                    UPDATE Files
                    SET FileName = @FileName,
                        Sha256 = @Sha256,
                        FileSize = @FileSize,
                        UploadedOn = @UploadedOn
                    WHERE FilePath = @FilePath AND VersionName = @VersionName
                END";

                var parameters = new DbParameter[]
                {
                    new DbParameter("@FileName", file.FileName),
                    new DbParameter("@FilePath", file.FilePath),
                    new DbParameter("@Sha256", file.Sha256),
                    new DbParameter("@FileSize", file.FileSize),
                    new DbParameter("@UploadedOn", file.UploadedOn),
                    new DbParameter("@VersionName", versionName)
                };

                await ExecuteNonQueryAsync(sql, CommandType.Text, parameters);
            }

            public async Task<Models.File?> GetFileOrDefaultAsync(string filePath, string versionName)
            {
                const string sql = @"
                SELECT FileName, FilePath, Sha256, FileSize, UploadedOn
                FROM Files
                WHERE FilePath = @FilePath AND VersionName = @VersionName";

                var parameters = new DbParameter[]
                {
                new DbParameter("@FilePath", filePath),
                new DbParameter("@VersionName", versionName)
                };

                using var reader = await ExecuteReaderAsync(sql, CommandType.Text, parameters) as SqlDataReader;

                if (reader != null && await reader.ReadAsync())
                {
                    return new Models.File
                    {
                        FileName = reader.GetString(0),
                        FilePath = reader.GetString(1),
                        Sha256 = reader.GetString(2),
                        FileSize = reader.GetString(3),
                        UploadedOn = reader.GetDateTime(4)
                    };
                }

                return null;
            }

            public async Task SaveVersionAsync(string versionName, DateTime uploadedOn)
            {
                const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM Versions WHERE VersionName = @VersionName)
                BEGIN
                    INSERT INTO Versions (VersionName, UploadedOn)
                    VALUES (@VersionName, @UploadedOn)
                END";

                var parameters = new DbParameter[]
                {
                    new DbParameter("@VersionName", versionName),
                    new DbParameter("@UploadedOn", uploadedOn)
                };

                await ExecuteNonQueryAsync(sql, CommandType.Text, parameters);
            }

            public async Task<List<Models.File>> GetFilesForVersionAsync(string versionName)
            {
                const string sql = @"
                SELECT FileName, FilePath, Sha256, FileSize, UploadedOn
                FROM Files
                WHERE VersionName = @VersionName
                ORDER BY FilePath";

                var parameters = new DbParameter[]
                {
                    new DbParameter("@VersionName", versionName)
                };

                using var connection = await CreateAndOpenConnectionAsync();
                using var command = CreateCommand(connection, sql, CommandType.Text, parameters);
                using var reader = await command.ExecuteReaderAsync();

                var files = new List<Models.File>();

                while (await reader.ReadAsync())
                {
                    files.Add(new Models.File
                    {
                        FileName = reader.GetString(0),
                        FilePath = reader.GetString(1),
                        Sha256 = reader.GetString(2),
                        FileSize = reader.GetString(3),
                        UploadedOn = reader.GetDateTime(4)
                    });
                }

                return files;
            }

            private SqlCommand CreateCommand(SqlConnection connection, string commandText, CommandType commandType, DbParameter[] parameters)
            {
                var command = connection.CreateCommand();
                command.CommandText = commandText;
                command.CommandType = commandType;

                foreach (var parameter in parameters)
                {
                    var sqlParameter = command.CreateParameter();
                    sqlParameter.ParameterName = parameter.Name;
                    sqlParameter.Value = parameter.Value ?? DBNull.Value;

                    command.Parameters.Add(sqlParameter);
                }

                return command;
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}
