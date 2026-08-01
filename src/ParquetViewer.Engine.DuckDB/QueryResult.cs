using DuckDB.NET.Data;

namespace ParquetViewer.Engine.DuckDB
{
    internal class QueryResult : IAsyncEnumerable<DuckDBDataReader>, IDisposable
    {
        private readonly DuckDBDataReader _reader;

        public QueryResult(DuckDBDataReader reader)
        {
            _reader = reader;
        }

        public void Dispose()
        {
            try
            {
                //Synchronous Dispose, not DisposeAsync: the ValueTask returned by the async overload was
                //discarded here, so disposal was not guaranteed to finish and any failure inside it escaped
                //the catch below rather than being reported by it.
                _reader.Dispose();
            }
            catch (Exception ex)
            {
                //Disposal failures shouldn't take down the caller, but a reader that won't close is worth
                //knowing about since it means we're holding a duckdb resource open.
                System.Diagnostics.Trace.TraceError($"Failed to dispose the DuckDB data reader: {ex}");
            }
        }

        public async Task<DuckDBDataReader> GetSingleAsync()
        {
            if (await _reader.ReadAsync())
            {
                return _reader;
            }
            throw new InvalidOperationException("No rows found.");
        }

        public async IAsyncEnumerator<DuckDBDataReader> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (!await _reader.ReadAsync())
            {
                yield break;
            }

            yield return _reader;

            while (await _reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return _reader;
            }
        }
    }
}