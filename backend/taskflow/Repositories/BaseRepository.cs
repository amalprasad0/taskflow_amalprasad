// Repositories/BaseRepository.cs
using Npgsql;
using System.Data;

namespace taskFlow.Repositories
{
    public abstract class BaseRepository
    {
        private readonly string _connectionString;

        protected BaseRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ─────────────────────────────────────────────────────────────
        //  CONNECTION
        // ─────────────────────────────────────────────────────────────

        protected NpgsqlConnection CreateConnection()
            => new NpgsqlConnection(_connectionString);

        // ─────────────────────────────────────────────────────────────
        //  PARAMETERS — converts anonymous object to NpgsqlParameters
        // ─────────────────────────────────────────────────────────────

        private static void AddParameters(NpgsqlCommand command, object? parameters)
        {
            if (parameters == null) return;

            foreach (var prop in parameters.GetType().GetProperties())
            {
                var value = prop.GetValue(parameters);
                command.Parameters.AddWithValue(
                    $"@{prop.Name}",
                    value ?? DBNull.Value
                );
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  NULLABLE READERS — safe column reading helpers
        // ─────────────────────────────────────────────────────────────

        protected static T? ReadNullable<T>(NpgsqlDataReader reader, string column) 
            where T : struct
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) 
                ? null 
                : reader.GetFieldValue<T>(ordinal);
        }

        protected static string? ReadNullableString(NpgsqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) 
                ? null 
                : reader.GetString(ordinal);
        }

        protected static DateTime? ReadNullableDateTime(NpgsqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) 
                ? null 
                : reader.GetDateTime(ordinal);
        }

        protected static Guid? ReadNullableGuid(NpgsqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) 
                ? null 
                : reader.GetGuid(ordinal);
        }

        // ─────────────────────────────────────────────────────────────
        //  READ — multiple rows
        // ─────────────────────────────────────────────────────────────

        protected async Task<IEnumerable<T>> QueryAsync<T>(
            string sql,
            object? parameters,
            Func<NpgsqlDataReader, T> map)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);
            AddParameters(command, parameters);

            var results = new List<T>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                results.Add(map(reader));

            return results;
        }

        // ─────────────────────────────────────────────────────────────
        //  READ — single row
        // ─────────────────────────────────────────────────────────────

        protected async Task<T?> QueryFirstOrDefaultAsync<T>(
            string sql,
            object? parameters,
            Func<NpgsqlDataReader, T> map) where T : class
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);
            AddParameters(command, parameters);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return map(reader);

            return null;
        }

        // ─────────────────────────────────────────────────────────────
        //  READ — scalar value (COUNT, id, etc.)
        // ─────────────────────────────────────────────────────────────

        protected async Task<T?> ExecuteScalarAsync<T>(
            string sql,
            object? parameters = null)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);
            AddParameters(command, parameters);

            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value
                ? default
                : (T)Convert.ChangeType(result, typeof(T));
        }

        // ─────────────────────────────────────────────────────────────
        //  WRITE — INSERT / UPDATE / DELETE (returns rows affected)
        // ─────────────────────────────────────────────────────────────

        protected async Task<int> ExecuteAsync(
            string sql,
            object? parameters = null)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);
            AddParameters(command, parameters);

            return await command.ExecuteNonQueryAsync();
        }

        // ─────────────────────────────────────────────────────────────
        //  WRITE — INSERT with RETURNING id
        // ─────────────────────────────────────────────────────────────

        protected async Task<Guid> InsertAndGetIdAsync(
            string sql,
            object? parameters = null)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(sql, connection);
            AddParameters(command, parameters);

            var result = await command.ExecuteScalarAsync();
            return result == null
                ? throw new Exception("Insert failed — no ID returned")
                : (Guid)result;
        }

        // ─────────────────────────────────────────────────────────────
        //  READ — two queries on same connection (Project + Tasks)
        // ─────────────────────────────────────────────────────────────

        protected async Task<(T1? first, IEnumerable<T2> second)> QueryMultipleAsync<T1, T2>(
            string firstSql,
            string secondSql,
            object? parameters,
            Func<NpgsqlDataReader, T1> mapFirst,
            Func<NpgsqlDataReader, T2> mapSecond) where T1 : class
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            // First query
            T1? first = null;
            using (var cmd1 = new NpgsqlCommand(firstSql, connection))
            {
                AddParameters(cmd1, parameters);
                using var reader1 = await cmd1.ExecuteReaderAsync();
                if (await reader1.ReadAsync())
                    first = mapFirst(reader1);
            }

            // Second query — same connection
            var second = new List<T2>();
            using (var cmd2 = new NpgsqlCommand(secondSql, connection))
            {
                AddParameters(cmd2, parameters);
                using var reader2 = await cmd2.ExecuteReaderAsync();
                while (await reader2.ReadAsync())
                    second.Add(mapSecond(reader2));
            }

            return (first, second);
        }

        // ─────────────────────────────────────────────────────────────
        //  TRANSACTION — multiple writes that must succeed together
        // ─────────────────────────────────────────────────────────────

        protected async Task<bool> ExecuteInTransactionAsync(
            Func<NpgsqlConnection, NpgsqlTransaction, Task> operations)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await operations(connection, transaction);
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Transaction failed: {ex.Message}");
                return false;
            }
        }
    }
}