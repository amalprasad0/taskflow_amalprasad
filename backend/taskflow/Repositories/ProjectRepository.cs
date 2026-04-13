using System.ComponentModel.DataAnnotations;
using taskFlow.Exceptions;
using taskFlow.Interfaces;
using taskFlow.Models;
using taskFlow.DTOs;
using System.Data;
using Npgsql;

namespace taskFlow.Repositories
{
    public class ProjectRepository : BaseSqlHandler<Projects>, IProjectService
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
                throw new InvalidOperationException("Error fetching projects", ex);
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
                throw new InvalidOperationException("Error fetching all projects", ex);
            }
        }

        public async Task<Response<CreateProjectResultDto>> CreateProject(CreateProjectDto createProjectDto, Guid userId)
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

                var result = await ExecuteScalarAsync(sql, parameters);
                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("Failed to create project");

                var projectId = result is Guid guid ? guid : Guid.Parse(result.ToString() ?? string.Empty);
                var responseData = new CreateProjectResultDto { ProjectId = projectId };

                return Response<CreateProjectResultDto>.Success(responseData, "Project created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating project: {ex.Message}");
                throw new InvalidOperationException("Error creating project", ex);
            }
        }

        public async Task<Response<ProjectsWithTasks>> GetProjectWithTasks(Guid projectId)
        {
            try
            {
                var sql = @"
                    SELECT 
                        p.id AS Id,
                        p.name AS Name,
                        p.description AS Description,
                        p.created_at AS CreatedAt,
                        p.owner_id AS OwnerId,
                        t.id AS TaskId,
                        t.title AS TaskName,
                        t.description AS TaskDescription,
                        CAST(t.status AS text) AS TaskStatus,
                        t.assignee_id AS TaskAssigneeId,
                        CAST(t.priority AS text) AS TaskPriority,
                        t.due_date AS TaskDueDate,
                        t.created_at AS TaskCreatedAt,
                        t.updated_at AS TaskUpdatedAt,
                        t.project_id AS TaskProjectId,
                        u.name AS AssigneeName,
                        u2.name AS OwnerName
                    FROM projects p
                    LEFT JOIN tasks t ON t.project_id = p.id
                    LEFT JOIN users u ON t.assignee_id = u.id
                    LEFT JOIN users u2 ON p.owner_id = u2.id
                    WHERE p.id = @ProjectId
                ";

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();
                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("ProjectId", projectId);

                using var reader = await command.ExecuteReaderAsync();

                var projectDictionary = new Dictionary<Guid, ProjectsWithTasks>();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(reader.GetOrdinal("Id"));
                    if (!projectDictionary.TryGetValue(id, out var projectWithTasks))
                    {
                        projectWithTasks = new ProjectsWithTasks
                        {
                            Id = id,
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                            OwnerId = reader.GetGuid(reader.GetOrdinal("OwnerId")),
                            OwnerName = reader.IsDBNull(reader.GetOrdinal("OwnerName")) ? null : reader.GetString(reader.GetOrdinal("OwnerName")),
                            Tasks = new List<Tasks>()
                        };
                        projectDictionary.Add(id, projectWithTasks);
                    }

                    if (!reader.IsDBNull(reader.GetOrdinal("TaskId")) && projectWithTasks.Tasks != null)
                    {
                        projectWithTasks.Tasks.Add(new Tasks
                        {
                            Id = reader.GetGuid(reader.GetOrdinal("TaskId")),
                            Name = reader.GetString(reader.GetOrdinal("TaskName")),
                            Description = reader.IsDBNull(reader.GetOrdinal("TaskDescription")) ? null : reader.GetString(reader.GetOrdinal("TaskDescription")),
                            Status = reader.GetString(reader.GetOrdinal("TaskStatus")),
                            AssigneeId = reader.IsDBNull(reader.GetOrdinal("TaskAssigneeId")) ? null : reader.GetGuid(reader.GetOrdinal("TaskAssigneeId")),
                            Priority = reader.IsDBNull(reader.GetOrdinal("TaskPriority")) ? null : reader.GetString(reader.GetOrdinal("TaskPriority")),
                            DueDate = reader.IsDBNull(reader.GetOrdinal("TaskDueDate")) ? null : reader.GetDateTime(reader.GetOrdinal("TaskDueDate")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("TaskCreatedAt")),
                            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("TaskUpdatedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("TaskUpdatedAt")),
                            ProjectId = reader.GetGuid(reader.GetOrdinal("TaskProjectId")),
                            AssigneeName = reader.IsDBNull(reader.GetOrdinal("AssigneeName")) ? null : reader.GetString(reader.GetOrdinal("AssigneeName")),
                        });
                    }
                }

                var projectResult = projectDictionary.Values.FirstOrDefault();
                if (projectResult == null)
                    throw new KeyNotFoundException("Project not found");

                return Response<ProjectsWithTasks>.Success(projectResult, "Project with tasks retrieved successfully");
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching project with tasks: {ex.Message}");
                throw new InvalidOperationException("Error fetching project with tasks", ex);
            }
        }

        // public async Task<Response<bool>> DeleteProject(DeleteProjectDto deleteProjectDto)
        // {
        //     try
        //     {
        //         var sql = "DELETE FROM projects WHERE id = @ProjectId AND owner_id = @UserId";
        //         var rowsAffected = await ExecuteAsync(sql, new { ProjectId = deleteProjectDto.ProjectId, UserId = deleteProjectDto.UserId });
        //         if (rowsAffected > 0)
        //             return Response<bool>.Success(true, "Project deleted successfully");

        //         throw new KeyNotFoundException("Project not found or could not be deleted");
        //     }
        //     catch (KeyNotFoundException)
        //     {
        //         throw;
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"Error deleting project: {ex.Message}");
        //         throw new InvalidOperationException("Error deleting project", ex);
        //     }
        // }

        public async Task<Response<IEnumerable<Tasks>>> GetTasksByProjectIdAsync(Guid projectId, Guid? assigneeId, string? status)
        {
            try
            {
                var projectExistsCheck = await ExecuteScalarAsync("SELECT EXISTS(SELECT 1 FROM projects WHERE id = @ProjectId)", new { ProjectId = projectId });
                if (projectExistsCheck is bool exists && !exists)
                    throw new KeyNotFoundException("Project not found");

                var sql = @"
                    SELECT t.id, t.title as Name, t.description, CAST(t.status AS text) as status, t.assignee_id as AssigneeId,CAST(t.priority AS text) as priority,t.project_id as ProjectId,t.created_at as CreatedAt,t.updated_at as UpdatedAt,t.due_date as DueDate,u.name as AssigneeName,t.created_by as OwnerId,u2.name as OwnerName
                    FROM tasks t
                    LEFT JOIN users u ON t.assignee_id = u.id
                    LEFT JOIN users u2 ON t.created_by = u2.id
                    WHERE t.project_id = @ProjectId
                      AND (CAST(@AssigneeId AS uuid) IS NULL OR t.assignee_id = CAST(@AssigneeId AS uuid))
                      AND (CAST(@Status AS task_status) IS NULL OR t.status = CAST(@Status AS task_status))
                ";

                var parameters = new
                {
                    ProjectId = projectId,
                    AssigneeId = assigneeId,
                    Status = string.IsNullOrWhiteSpace(status) ? null : status
                };

                var tasks = await QueryAsync<Tasks>(sql, parameters);
                return Response<IEnumerable<Tasks>>.Success(tasks, "Tasks retrieved successfully");
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching project tasks: {ex.Message}");
                throw new InvalidOperationException("Error fetching project tasks", ex);
            }
        }

        public async Task<Response<CreateTaskResultDto>> CreateTask(CreateTaskDto createTaskDto, Guid projectId, Guid userId)
        {
            try
            {
                if (createTaskDto.AssigneeId.HasValue)
                {
                    var userExistsSql = "SELECT 1 FROM users WHERE id = @UserId";
                    var userExists = await QueryAsync<int?>(userExistsSql, new { UserId = createTaskDto.AssigneeId });
                    if (userExists.Count() == 0)
                        throw new ValidationException("Invalid assignee ID: User does not exist");
                }

                if (createTaskDto.DueDate.HasValue && createTaskDto.DueDate.Value <= DateTime.UtcNow)
                {
                    throw new ValidationException("Due date must be in the future");
                }

                var sql = @"
                INSERT INTO tasks 
                    (title, description, status, assignee_id, priority, project_id, created_at, updated_at, due_date,created_by) 
                    VALUES 
                    (
                        @Title, 
                        @Description,
                        CAST(@Status AS task_status),
                        CASE 
                            WHEN @AssigneeId IS NOT NULL 
                                AND EXISTS (SELECT 1 FROM users WHERE id = @AssigneeId)
                            THEN @AssigneeId
                            ELSE NULL
                        END,
                        CAST(@Priority AS task_priority),
                        @ProjectId, 
                        @CreatedAt, 
                        @UpdatedAt, 
                        @DueDate,
                        @createdBy
                    )
                RETURNING id;
                ";

                var parameters = new
                {
                    Title = createTaskDto.Title,
                    Description = createTaskDto.Description,
                    Status = createTaskDto.Status,
                    AssigneeId = createTaskDto.AssigneeId,
                    Priority = createTaskDto.Priority,
                    ProjectId = projectId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DueDate = createTaskDto.DueDate,
                    createdBy = userId
                };

                var result = await ExecuteScalarAsync(sql, parameters);
                if (result == null || result == DBNull.Value)
                    throw new InvalidOperationException("Failed to create task");

                var taskId = result is Guid guid ? guid : Guid.Parse(result.ToString() ?? string.Empty);
                var responseData = new CreateTaskResultDto { TaskId = taskId };
                return Response<CreateTaskResultDto>.Success(responseData, "Task created successfully");
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating task: {ex.Message}");
                throw new InvalidOperationException("Error creating task", ex);
            }
        }

        public async Task<Response<UpdateTaskResultDto>> UpdateTask(UpdateTaskDto updateTaskDto, Guid taskId, Guid userId)
        {
            try
            {
                if (updateTaskDto.AssigneeId.HasValue)
                {
                    const string userExistsSql = "SELECT 1 FROM users WHERE id = @UserId";
                    var userExists = await QueryAsync<int>(userExistsSql, new { UserId = updateTaskDto.AssigneeId.Value });
                    if (!userExists.Any())
                        throw new ValidationException("Invalid assignee ID: User does not exist");
                }

                if (updateTaskDto.DueDate.HasValue && updateTaskDto.DueDate.Value <= DateTime.UtcNow)
                    throw new ValidationException("Due date must be in the future");

                const string checkSql = @"
                    SELECT
                        EXISTS (SELECT 1 FROM tasks WHERE id = @TaskId) AS ""Exists"",
                        EXISTS (
                            SELECT 1 FROM tasks 
                            WHERE id = @TaskId 
                              AND (
                                  created_by = @UserId
                                  OR project_id IN (SELECT id FROM projects WHERE owner_id = @UserId)
                              )
                        ) AS ""IsAuthorized"";
                ";
                var checkResult = await QuerySingleAsync<DeleteProjectResult>(checkSql, new { TaskId = taskId, UserId = userId });

                if (checkResult != null && !checkResult.Exists)
                    throw new KeyNotFoundException("Task not found");
                if (checkResult != null && !checkResult.IsAuthorized)
                    throw new ForbiddenException("You do not have permission to update this task");

                const string sql = @"
                    UPDATE tasks
                    SET
                        title       = COALESCE(@Title, title),
                        description = COALESCE(@Description, description),
                        assignee_id = COALESCE(CAST(@AssigneeId AS uuid), assignee_id),
                        priority    = COALESCE(CAST(@Priority AS task_priority), priority),
                        status      = COALESCE(CAST(@Status AS task_status), status),
                        due_date    = COALESCE(CAST(@DueDate AS timestamp), due_date),
                        updated_at  = NOW()
                    WHERE id = @TaskId
                    RETURNING id;
                ";

                var parameters = new
                {
                    TaskId = taskId,
                    Title = updateTaskDto.Title,
                    Description = updateTaskDto.Description,
                    AssigneeId = updateTaskDto.AssigneeId,
                    Priority = updateTaskDto.Priority,
                    Status = updateTaskDto.Status,
                    DueDate = updateTaskDto.DueDate,
                };

                var result = await ExecuteScalarAsync(sql, parameters);

                if (result is null || result == DBNull.Value)
                    throw new InvalidOperationException("Failed to update task after passing checks");

                var updatedId = result is Guid g ? g : Guid.Parse(result.ToString()!);
                var responseData = new UpdateTaskResultDto { TaskId = updatedId };
                return Response<UpdateTaskResultDto>.Success(responseData, "Task updated successfully");
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (ForbiddenException)
            {
                throw;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating task: {ex.Message}");
                throw new InvalidOperationException("An unexpected error occurred while updating the task", ex);
            }
        }

        public async Task<Response<Guid>> DeleteTask(Guid taskId, Guid userId)
        {
            try
            {
                const string checkSql = @"
                    SELECT
                        EXISTS (SELECT 1 FROM tasks WHERE id = @TaskId) AS ""Exists"",
                        EXISTS (
                            SELECT 1 FROM tasks 
                            WHERE id = @TaskId 
                              AND (
                                  created_by = @UserId
                                  OR project_id IN (SELECT id FROM projects WHERE owner_id = @UserId)
                              )
                        ) AS ""IsAuthorized"";
                ";
                var checkResult = await QuerySingleAsync<DeleteProjectResult>(checkSql, new { TaskId = taskId, UserId = userId });

                if (checkResult != null && !checkResult.Exists)
                    throw new KeyNotFoundException("Task not found");
                if (checkResult != null && !checkResult.IsAuthorized)
                    throw new ForbiddenException("You do not have permission to delete this task");

                const string sql = @"
                    DELETE FROM tasks
                    WHERE id = @TaskId
                      AND (
                            created_by = @UserId
                            OR project_id IN (SELECT id FROM projects WHERE owner_id = @UserId)
                          )
                    RETURNING id;
                ";

                var result = await ExecuteScalarAsync(sql, new { TaskId = taskId, UserId = userId });

                if (result is null || result == DBNull.Value)
                    throw new InvalidOperationException("Failed to delete task after passing checks");

                var deletedId = result is Guid g ? g : Guid.Parse(result.ToString()!);
                return Response<Guid>.Success(deletedId, "Task deleted successfully");
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting task: {ex.Message}");
                throw new InvalidOperationException("An unexpected error occurred while deleting the task", ex);
            }
        }
      public async Task<Response<StatsDto>> GetProjectStats(Guid projectId, Guid userId)
        {
            try
            {
                const string checkSql = @"
                    SELECT
                        EXISTS (SELECT 1 FROM projects WHERE id = @ProjectId)                        AS ""Exists"",
                        EXISTS (SELECT 1 FROM projects WHERE id = @ProjectId AND owner_id = @UserId) AS ""IsAuthorized"";
                ";
                var checkResult = await QuerySingleAsync<DeleteProjectResult>(checkSql, new { ProjectId = projectId, UserId = userId });

                if (checkResult != null && !checkResult.Exists)
                    throw new KeyNotFoundException("Project not found");
                if (checkResult != null && !checkResult.IsAuthorized)
                    throw new ForbiddenException("You do not have permission to view stats for this project");

                var sql = @"
                SELECT 
                    (
                        SELECT jsonb_object_agg(status, count)
                        FROM (
                            SELECT status, COUNT(*) AS count 
                            FROM tasks 
                            WHERE project_id = @ProjectId         -- ✅ Fixed: was hardcoded GUID
                            GROUP BY status
                        ) AS status_counts
                    ) AS TasksPerStatus,

                    (
                        SELECT jsonb_object_agg(priority, count)
                        FROM (
                            SELECT priority, COUNT(*) AS count 
                            FROM tasks 
                            WHERE project_id = @ProjectId         -- ✅ Fixed: was hardcoded GUID
                            GROUP BY priority
                        ) AS priority_counts
                    ) AS TasksPerPriority,

                    (
                        SELECT jsonb_object_agg(name, count)
                        FROM (
                            SELECT 
                                COALESCE(u.name, 'Unassigned') AS name,
                                COUNT(*) AS count 
                            FROM tasks t 
                            LEFT JOIN users u ON t.assignee_id = u.id 
                            WHERE t.project_id = @ProjectId       -- ✅ Fixed: was hardcoded GUID
                            GROUP BY name
                        ) AS assignee_counts
                    ) AS TasksPerAssignee
                ";

                // ✅ QueryAsync<StatsDto> now works because TypeHandler handles JSONB deserialization
                var statsResult = await QuerySingleAsync<StatsDto>(sql, new { ProjectId = projectId });

                if (statsResult == null)
                    throw new KeyNotFoundException("Project not found or no tasks available for statistics");

                return Response<StatsDto>.Success(statsResult, "Project statistics retrieved successfully");
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching project statistics: {ex.Message}");
                throw new InvalidOperationException("Error fetching project statistics", ex);
            }
        }
        public async Task<Response<DeleteProjectResultDto>> DeleteProject(Guid projectId,Guid userId)
        {
            try
            {
                
                var sql = @"
                WITH deleted AS (
                    DELETE FROM projects
                    WHERE id = @ProjectId
                    AND owner_id = @UserId
                    RETURNING id
                )
                SELECT 
                    EXISTS (SELECT 1 FROM deleted) AS ""IsDeleted"",
                    EXISTS (
                        SELECT 1 FROM projects WHERE id = @ProjectId
                    ) AS ""Exists"",
                    EXISTS (
                        SELECT 1 FROM projects 
                        WHERE id = @ProjectId AND owner_id = @UserId
                    ) AS ""IsAuthorized"";
                ";
                var result = await QuerySingleAsync<DeleteProjectResult>(sql, new { ProjectId = projectId, UserId = userId });

                if (result != null && result.IsDeleted)
                    return Response<DeleteProjectResultDto>.Success(new DeleteProjectResultDto { IsDeleted = true }, "Project deleted successfully");
                if (result != null && !result.Exists)
                    throw new KeyNotFoundException("Project not found");
                if (result != null && !result.IsAuthorized)
                    throw new ForbiddenException("You do not have permission to delete this project");
               
                throw new KeyNotFoundException("Project not found or you do not have permission to delete it");
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting task: {ex.Message}");
                throw new InvalidOperationException("An unexpected error occurred while deleting the task", ex);
            }
        }
        public async Task<Response<UpdateProjectResultDto>> UpdateProject(UpdateProjectDataDto updateProjectDataDto, Guid projectId, Guid userId)
        {
            try
            {
                const string checkSql = @"
                    SELECT
                        EXISTS (SELECT 1 FROM projects WHERE id = @ProjectId)                        AS Exists,
                        EXISTS (SELECT 1 FROM projects WHERE id = @ProjectId AND owner_id = @UserId) AS IsAuthorized;
                ";

                var check = await QuerySingleAsync<DeleteProjectResult>(checkSql, new { ProjectId = projectId, UserId = userId });

                Console.WriteLine($"[UpdateProject] ProjectId={projectId}, UserId={userId} → Exists={check?.Exists}, IsAuthorized={check?.IsAuthorized}");

                if (check is null || !check.Exists)
                    throw new KeyNotFoundException("Project not found");
                if (!check.IsAuthorized)
                    throw new ForbiddenException("You do not have permission to update this project");

                const string sql = @"
                    UPDATE projects
                    SET name        = COALESCE(@Name, name),
                        description = COALESCE(@Description, description)
                    WHERE id = @ProjectId
                      AND owner_id = @UserId
                    RETURNING id;
                ";
                var result = await ExecuteScalarAsync(sql, new { Name = updateProjectDataDto.Name, Description = updateProjectDataDto.Description, ProjectId = projectId, UserId = userId });
                if (result is null || result == DBNull.Value)
                    throw new InvalidOperationException("Update failed unexpectedly");

                return Response<UpdateProjectResultDto>.Success(new UpdateProjectResultDto { IsUpdated = true }, "Project updated successfully");
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (ForbiddenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An unexpected error occurred while updating the project", ex);
            }
        }
    }
}
