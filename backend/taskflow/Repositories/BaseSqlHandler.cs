using System.Data;
using Npgsql;
using System.Reflection;

namespace taskFlow.Repositories
{
    public class BaseSqlHandler<T> where T : new()
    {
        protected readonly string _connectionString;
        private readonly string _tableName;

        public BaseSqlHandler(string connectionString)
        {
            _connectionString = connectionString;
            _tableName = typeof(T).Name.ToLower() + "s"; // Simple pluralization, adjust as needed
        }

        public async Task<List<T>> GetAllAsync()
        {
            var sql = $"SELECT * FROM {_tableName}";
            return await QueryAsync(sql);
        }

        public async Task<T?> GetByIdAsync(Guid id)
        {
            var sql = $"SELECT * FROM {_tableName} WHERE id = @Id";
            return await QuerySingleAsync(sql, new { Id = id });
        }

        public async Task<int> CreateAsync(T entity)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Id" && p.CanRead); // Assume Id is auto-generated

            var columns = string.Join(", ", properties.Select(p => p.Name.ToLower()));
            var values = string.Join(", ", properties.Select(p => "@" + p.Name));

            var sql = $"INSERT INTO {_tableName} ({columns}) VALUES ({values})";
            return await ExecuteAsync(sql, entity);
        }

        public async Task<int> UpdateAsync(T entity)
        {
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Id" && p.CanRead);

            var setClause = string.Join(", ", properties.Select(p => $"{p.Name.ToLower()} = @{p.Name}"));
            var sql = $"UPDATE {_tableName} SET {setClause} WHERE id = @Id";
            return await ExecuteAsync(sql, entity);
        }

        public async Task<int> DeleteAsync(Guid id)
        {
            var sql = $"DELETE FROM {_tableName} WHERE id = @Id";
            return await ExecuteAsync(sql, new { Id = id });
        }

        public async Task<List<T>> QueryAsync(string sql, object? parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<T>();

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            while (await reader.ReadAsync())
            {
                var item = new T();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var prop = properties.FirstOrDefault(p => string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));
                    if (prop != null && prop.CanWrite)
                    {
                        var value = reader.GetValue(i);
                        if (value == DBNull.Value)
                        {
                            value = null;
                        }
                        try
                        {
                            if (prop.PropertyType == typeof(Guid) && value is string str)
                            {
                                value = Guid.Parse(str);
                            }
                            else if (prop.PropertyType == typeof(DateTime) && value is string dtStr)
                            {
                                value = DateTime.Parse(dtStr);
                            }
                            else
                            {
                                value = Convert.ChangeType(value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                            }
                        }
                        catch
                        {
                            // If conversion fails, set to null or handle as needed
                            value = null;
                        }
                        prop.SetValue(item, value);
                    }
                }
                results.Add(item);
            }

            return results;
        }

        public async Task<List<TModel>> QueryAsync<TModel>(string sql, object? parameters = null) where TModel : new()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<TModel>();
            var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            while (await reader.ReadAsync())
            {
                var item = new TModel();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var prop = properties.FirstOrDefault(p => string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));
                    if (prop != null && prop.CanWrite)
                    {
                        var value = reader.GetValue(i);
                        if (value == DBNull.Value)
                        {
                            value = null;
                        }
                        try
                        {
                            if (prop.PropertyType == typeof(Guid) && value is string str)
                            {
                                value = Guid.Parse(str);
                            }
                            else if (prop.PropertyType == typeof(DateTime) && value is string dtStr)
                            {
                                value = DateTime.Parse(dtStr);
                            }
                            else
                            {
                                value = Convert.ChangeType(value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                            }
                        }
                        catch
                        {
                            value = null;
                        }
                        prop.SetValue(item, value);
                    }
                }
                results.Add(item);
            }

            return results;
        }

        public async Task<List<TModel>> QueryAsync<TModel>(string sql, Dictionary<string, object> parameters) where TModel : new()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<TModel>();
            var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            while (await reader.ReadAsync())
            {
                var item = new TModel();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var prop = properties.FirstOrDefault(p => string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));
                    if (prop != null && prop.CanWrite)
                    {
                        var value = reader.GetValue(i);
                        if (value == DBNull.Value)
                        {
                            value = null;
                        }
                        try
                        {
                            if (prop.PropertyType == typeof(Guid) && value is string str)
                            {
                                value = Guid.Parse(str);
                            }
                            else if (prop.PropertyType == typeof(DateTime) && value is string dtStr)
                            {
                                value = DateTime.Parse(dtStr);
                            }
                            else
                            {
                                value = Convert.ChangeType(value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                            }
                        }
                        catch
                        {
                            value = null;
                        }
                        prop.SetValue(item, value);
                    }
                }
                results.Add(item);
            }

            return results;
        }

        public async Task<TModel?> QuerySingleAsync<TModel>(string sql, object? parameters = null) where TModel : new()
        {
            var results = await QueryAsync<TModel>(sql, parameters);
            return results.FirstOrDefault();
        }

        public async Task<T?> QuerySingleAsync(string sql, object? parameters = null)
        {
            var results = await QueryAsync(sql, parameters);
            return results.FirstOrDefault();
        }

        public async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object> ExecuteScalarAsync(string sql, object? parameters = null)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);

            if (parameters != null)
            {
                AddParameters(command, parameters);
            }

            return await command.ExecuteScalarAsync();
        }

        private void AddParameters(NpgsqlCommand command, object parameters)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var value = prop.GetValue(parameters);
                command.Parameters.AddWithValue(prop.Name, value ?? DBNull.Value);
            }
        }
    }
}