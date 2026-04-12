using taskFlow.Models;
using taskFlow.DTOs;

namespace taskFlow.Interfaces
{
    public interface IProjectService
    {
        public Task<Response<IEnumerable<Projects?>>> GetProjectsByUserIdAsync(Guid id);
        public Task<Response<IEnumerable<Projects?>>> GetAllProjects();
        public Task<Response<int>> CreateProject(CreateProjectDto createProjectDto, Guid userId);
        public Task<Response<ProjectsWithTasks>> GetProjectWithTasks(Guid projectId);
    }
}