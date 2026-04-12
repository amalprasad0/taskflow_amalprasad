using taskFlow.Interfaces;
using taskFlow.Models;

namespace taskFlow.Repositories
{
    public class ProjectRepository : DapperRepository<Projects>, IProjectService
    {
        public ProjectRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<Response<IEnumerable<Projects?>>> GetProjectsByUserIdAsync(Guid id)
        {
            try
            {
                var sql = @"
                    SELECT DISTINCT 
                        projects.id AS Id,
                        projects.name AS Name,
                        projects.description AS Description,
                        projects.created_at AS CreatedAt,
                        projects.owner_id AS OwnerId
                    FROM projects
                    LEFT JOIN tasks ON tasks.project_id = projects.id
                    WHERE 
                        projects.owner_id = @UserId
                        OR tasks.assignee_id = @UserId
                ";
                var projects = await QueryAsync(sql, new { UserId = id });
                return Response<IEnumerable<Projects?>>.Success(projects, "Projects retrieved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching projects by user ID: {ex.Message}");
                return Response<IEnumerable<Projects?>>.Failure($"Error fetching projects: {ex.Message}");
            }
        }

        public async Task<Response<IEnumerable<Projects?>>> GetAllProjects()
        {
            try
            {
                var projects = await GetAllAsync();
                return Response<IEnumerable<Projects?>>.Success(projects, "All projects retrieved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all projects: {ex.Message}");
                return Response<IEnumerable<Projects?>>.Failure($"Error fetching all projects: {ex.Message}");
            }
        }

        public async Task<Response<int>> CreateProject(CreateProjectDto createProjectDto, Guid userId)
        {
            try
            {
                var sql = @"
                    INSERT INTO projects (name, description, created_at, owner_id) 
                    VALUES (@Name, @Description, @CreatedAt, @OwnerId)
                    RETURNING id;
                ";

                var parameters = new
                {
                    Name = createProjectDto.ProjectName,
                    Description = createProjectDto.ProjectDescription,
                    CreatedAt = DateTime.UtcNow,
                    OwnerId = userId
                };

                var rowsAffected = await CreateAsync(sql, parameters);
                return Response<int>.Success(rowsAffected, "Project created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating project: {ex.Message}");
                return Response<int>.Failure($"Error creating project: {ex.Message}");
            }
        }
    }
}
