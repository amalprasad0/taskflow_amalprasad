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
        [Authorize] // Requires JWT token
        public async Task<IActionResult> GetProjectsByUserId()
        {
            var userId = HttpContext.GetUserId();
            if (userId == null || userId == Guid.Empty)
            {
                if (!HttpContext.IsAuthenticated())
                    return Unauthorized(new Response<object> { Status = false, Message = "Authentication required", Data = null });

                userId = HttpContext.GetUserId();
                if (userId == Guid.Empty)
                    return BadRequest(new Response<object> { Status = false, Message = "Invalid user ID in token", Data = null });
            }

            var response = await _projectService.GetProjectsByUserIdAsync(userId);
            
            if (!response.Status)
                return BadRequest(response);

            var projectList = response.Data?.ToList();
            if (projectList == null || projectList.Count == 0)
                return NotFound(new Response<object> { Status = false, Message = $"No projects found for user ID {userId}", Data = null });

            return Ok(response);
        }
        [HttpPost("/projects")]
        [Authorize]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto project)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.ProjectName))
                return BadRequest(new Response<object> { Status = false, Message = "Project name is required", Data = null });

            // Get user ID from JWT token
            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new Response<object> { Status = false, Message = "Authentication required", Data = null });

            var response = await _projectService.CreateProject(project, userId);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }
        [HttpGet("/project/{projectId}")]
        [Authorize]
        public async Task<IActionResult> GetProjectWithTasks(Guid projectId)
        {
            if (projectId == Guid.Empty)
                return BadRequest(new Response<object> { Status = false, Message = "Invalid project ID", Data = null });

            var response = await _projectService.GetProjectWithTasks(projectId);
            if (!response.Status)
                return BadRequest(response);

            if (response.Data == null || response.Data.Tasks == null)
                return NotFound(new Response<object> { Status = false, Message = $"Project with ID {projectId} not found", Data = null });

            return Ok(response);
        }

        [HttpGet("/projects/{projectId}/tasks")]
        [Authorize]
        public async Task<IActionResult> GetTasksByProjectId(Guid projectId, [FromQuery] Guid? assignee, [FromQuery] string? status)
        {
            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new Response<object> { Status = false, Message = "Authentication required", Data = null });
            if (projectId == Guid.Empty)
                return BadRequest(new Response<object> { Status = false, Message = "Invalid project ID", Data = null });

            var response = await _projectService.GetTasksByProjectIdAsync(projectId, assignee, status);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpDelete("/project/{projectId}")]
        [Authorize]
        public async Task<IActionResult> DeleteProject(Guid projectId)
        {
            if (projectId == Guid.Empty)
                return BadRequest(new Response<object> { Status = false, Message = "Invalid project ID", Data = null });

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new Response<object> { Status = false, Message = "Authentication required", Data = null });

            var deleteProjectDto = new DeleteProjectDto
            {
                ProjectId = projectId,
                UserId = userId
            };

            var response = await _projectService.DeleteProject(deleteProjectDto);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }
        [HttpPost("/project/{projectId}/tasks")]
        [Authorize]
        public async Task<IActionResult> CreateTask(Guid projectId, [FromBody] CreateTaskDto createTaskDto)
        {
            if (projectId == Guid.Empty)
                return BadRequest(new Response<object> { Status = false, Message = "Invalid project ID", Data = null });

            if (createTaskDto == null || string.IsNullOrWhiteSpace(createTaskDto.Title))
                return BadRequest(new Response<object> { Status = false, Message = "Task title is required", Data = null });

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new Response<object> { Status = false, Message = "Authentication required", Data = null });

            var response = await _projectService.CreateTask(createTaskDto, projectId, userId);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }
         [HttpPatch("/task/{taskId}")]
        [Authorize]
        public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskDto updateTaskDto)
        {
            if (updateTaskDto == null)
                return BadRequest(new Response<object> { Status = false, Message = "Invalid task data", Data = null });

            var response = await _projectService.UpdateTask(updateTaskDto, taskId);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }
        [HttpDelete("/task/{taskId}")]
        [Authorize]
        public async Task<IActionResult> DeleteTask(Guid taskId)
        {
            if (taskId == Guid.Empty)
                return BadRequest(new Response<object> { Status = false, Message = "Invalid task ID", Data = null });

            var userId = HttpContext.GetUserId();
            if (userId == Guid.Empty)
                return Unauthorized(new Response<object> { Status = false, Message = "Authentication required", Data = null });

            var response = await _projectService.DeleteTask(taskId, userId);
            if (!response.Status)
                return BadRequest(response);
            return Ok(response);
        }
    }
}
