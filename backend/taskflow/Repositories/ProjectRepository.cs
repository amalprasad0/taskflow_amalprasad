public class ProjectRepository: IProjectService
{
    public ProjectRepository()
    {

    }
    public async Task<Projects?> GetProjectByIdAsync(uint id)
    {
        try
        {
            return null;
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error fetching project by ID: {ex.Message}");
            return null;
        }
    }
    public async Task<IEnumerable<Projects>> GetAllProjects()
    {
        try
        {
            // Implementation here
            return Enumerable.Empty<Projects>();
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error fetching all projects: {ex.Message}");
            return Enumerable.Empty<Projects>();
        }
    }
}