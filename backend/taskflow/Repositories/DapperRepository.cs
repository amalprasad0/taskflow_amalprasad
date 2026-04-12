using Dapper;
using Npgsql;
using System.Data;

namespace taskFlow.Repositories
{
    public class DapperRepository<T> : IRepository<T> where T : class
    {
        private readonly string _connectionString;

        public DapperRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<T?> GetByIdAsync(object id)
        {
            using var connection = CreateConnection();
            var tableName = typeof(T).Name;
            var sql = $"SELECT * FROM {tableName} WHERE id = @Id";
            return await connection.QueryFirstOrDefaultAsync<T>(sql, new { Id = id });
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            using var connection = CreateConnection();
            var tableName = typeof(T).Name;
            var sql = $"SELECT * FROM {tableName}";
            return await connection.QueryAsync<T>(sql);
        }

        public async Task<IEnumerable<T>> QueryAsync(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters);
        }

        public async Task<int> CreateAsync(string sql, object parameters)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }

        public async Task<int> UpdateAsync(string sql, object parameters)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }

        public async Task<int> DeleteAsync(string sql, object parameters)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }

        public async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }
    }
}
