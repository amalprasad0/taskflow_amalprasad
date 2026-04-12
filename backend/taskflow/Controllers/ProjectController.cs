using Microsoft.AspNetCore.Mvc;
using taskFlow.Interfaces;
using taskFlow.Models;

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
        public async Task<IActionResult> GetProjectsByUserId([FromQuery] Guid userId)
        {
            var response = await _projectService.GetProjectsByUserIdAsync(userId);
            
            if (!response.Status)
                return BadRequest(response);

            var projectList = response.Data?.ToList();
            if (projectList == null || projectList.Count == 0)
                return NotFound(new Response<object> { Status = false, Message = $"No projects found for user ID {userId}", Data = null });

            return Ok(response);
        }
        [HttpPost("projects")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto project)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.ProjectName) || string.IsNullOrWhiteSpace(project.CreatedBy))
                return BadRequest(new Response<object> { Status = false, Message = "Project name and owner ID are required", Data = null });

            var response = await _projectService.CreateProject(project);
            if (!response.Status)
                return BadRequest(response);

            return Ok(response);
        }
    }
}
