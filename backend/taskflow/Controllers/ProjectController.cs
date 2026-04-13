using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using taskFlow.Interfaces;
using taskFlow.Models;
using taskFlow.Services;

namespace taskFlow.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet("/projects")]
        [Authorize]
        public async Task<IActionResult> GetProjectsByUserId()
        {
            var userId = HttpContext.GetUserId();
            if (!HttpContext.IsAuthenticated())
                throw new UnauthorizedAccessException("Authentication required");

            if (userId == Guid.Empty)
                throw new ValidationException("Invalid user ID in token");

            var response = await _projectService.GetProjectsByUserIdAsync(userId);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            var projectList = response.Data?.ToList();
            if (projectList == null || projectList.Count == 0)
                throw new KeyNotFoundException($"No projects found for user ID {userId}");

            return Ok(response);
        }

        [HttpPost("/projects")]
        [Authorize]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto project)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.ProjectName))
                throw new ValidationException("Project name is required");

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("Authentication required");

            var response = await _projectService.CreateProject(project, userId);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            return Ok(response);
        }

        [HttpGet("/project/{projectId}")]
        [Authorize]
        public async Task<IActionResult> GetProjectWithTasks(Guid projectId)
        {
            if (projectId == Guid.Empty)
                throw new ValidationException("Invalid project ID");

            var response = await _projectService.GetProjectWithTasks(projectId);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            if (response.Data == null || response.Data.Tasks == null)
                throw new KeyNotFoundException($"Project with ID {projectId} not found");

            return Ok(response);
        }

        [HttpGet("/projects/{projectId}/tasks")]
        [Authorize]
        public async Task<IActionResult> GetTasksByProjectId(Guid projectId, [FromQuery] Guid? assignee, [FromQuery] string? status)
        {
            if (projectId == Guid.Empty)
                throw new ValidationException("Invalid project ID");

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("Authentication required");

            var response = await _projectService.GetTasksByProjectIdAsync(projectId, assignee, status);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            return Ok(response);
        }

        [HttpDelete("/project/{projectId}")]
        [Authorize]
        public async Task<IActionResult> DeleteProject(Guid projectId)
        {
            if (projectId == Guid.Empty)
                throw new ValidationException("Invalid project ID");

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("Authentication required");

            var deleteProjectDto = new DeleteProjectDto
            {
                ProjectId = projectId,
                UserId = userId
            };

            var response = await _projectService.DeleteProject(deleteProjectDto);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            return Ok(response);
        }

        [HttpPost("/project/{projectId}/tasks")]
        [Authorize]
        public async Task<IActionResult> CreateTask(Guid projectId, [FromBody] CreateTaskDto createTaskDto)
        {
            if (projectId == Guid.Empty)
                throw new ValidationException("Invalid project ID");

            if (createTaskDto == null || string.IsNullOrWhiteSpace(createTaskDto.Title))
                throw new ValidationException("Task title is required");

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("Authentication required");

            var response = await _projectService.CreateTask(createTaskDto, projectId, userId);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            return Ok(response);
        }

        [HttpPatch("/task/{taskId}")]
        [Authorize]
        public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskDto updateTaskDto)
        {
            if (updateTaskDto == null)
                throw new ValidationException("Invalid task data");

            var response = await _projectService.UpdateTask(updateTaskDto, taskId);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            return Ok(response);
        }

        [HttpDelete("/task/{taskId}")]
        [Authorize]
        public async Task<IActionResult> DeleteTask(Guid taskId)
        {
            if (taskId == Guid.Empty)
                throw new ValidationException("Invalid task ID");

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                throw new UnauthorizedAccessException("Authentication required");

            var response = await _projectService.DeleteTask(taskId, userId);
            if (!response.Status)
                throw new InvalidOperationException(response.Message);

            return Ok(response);
        }
    }
}
