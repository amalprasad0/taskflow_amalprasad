public interface IProjectService
{
    public Task<Projects?> GetProjectByIdAsync(uint id);
    public Task<IEnumerable<Projects?>> GetAllProjects();
}