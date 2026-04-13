using taskFlow.Models;
using taskFlow.DTOs;

namespace taskFlow.Interfaces
{
    public interface IProjectService
    {
        public Task<Response<IEnumerable<Projects?>>> GetProjectsByUserIdAsync(Guid id);
        public Task<Response<IEnumerable<Projects?>>> GetAllProjects();
        public Task<Response<CreateProjectResultDto>> CreateProject(CreateProjectDto createProjectDto, Guid userId);
        public Task<Response<ProjectsWithTasks>> GetProjectWithTasks(Guid projectId);
        // public Task<Response<bool>> DeleteProject(DeleteProjectDto deleteProjectDto);
        public Task<Response<IEnumerable<Tasks>>> GetTasksByProjectIdAsync(Guid projectId, Guid? assigneeId, string? status);
        public Task<Response<CreateTaskResultDto>> CreateTask(CreateTaskDto createTaskDto, Guid projectId, Guid userId);
        public  Task<Response<UpdateTaskResultDto>> UpdateTask(UpdateTaskDto updateTaskDto, Guid taskId, Guid userId);
        public  Task<Response<Guid>> DeleteTask(Guid taskId,Guid userId);
        public Task<Response<StatsDto>> GetProjectStats(Guid projectId, Guid userId);
        public  Task<Response<DeleteProjectResultDto>> DeleteProject(Guid projectId,Guid userId);
        public  Task<Response<UpdateProjectResultDto>> UpdateProject(UpdateProjectDataDto updateProjectDataDto,Guid projectId,Guid userId);

    }
}