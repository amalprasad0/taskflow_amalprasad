namespace taskFlow.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(object id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> QueryAsync(string sql, object? parameters = null);
        Task<int> CreateAsync(string sql, object parameters);
        Task<int> UpdateAsync(string sql, object parameters);
        Task<int> DeleteAsync(string sql, object parameters);
        Task<int> ExecuteAsync(string sql, object? parameters = null);
    }
}
