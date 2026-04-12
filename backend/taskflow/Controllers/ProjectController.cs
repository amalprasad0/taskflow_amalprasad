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

        [HttpGet("projects")]
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
        [HttpPost("projects")]
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
    }
}
