using DuckDB.NET.Data;

namespace ParquetViewer.Engine.DuckDB
{
    public class DuckDBHandle : IDisposable
    {
        public string ParquetFilePath { get; }
        public DuckDBConnection Connection { get; }

        private DuckDBHandle(DuckDBConnection connection, string parquetPath)
        {
            ParquetFilePath = parquetPath;
            Connection = connection;
        }

        public static async Task<DuckDBHandle> OpenAsync(string parquetPath)
        {
            if (!File.Exists(parquetPath)) //handles null
                throw new FileNotFoundException(parquetPath);

            var connection = new DuckDBConnection("Data Source=:memory:");
            try
            {
                await connection.OpenAsync();
                return new DuckDBHandle(connection, parquetPath);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                Connection.Dispose();
            }
            catch (Exception ex)
            {
                //Disposal failures shouldn't take down the caller, but a connection that won't close is
                //worth knowing about since it means we're leaking a duckdb handle.
                System.Diagnostics.Trace.TraceError($"Failed to dispose the DuckDB connection: {ex}");
            }
        }
    }
}