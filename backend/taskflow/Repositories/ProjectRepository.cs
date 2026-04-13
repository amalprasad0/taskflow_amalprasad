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

        public async Task<Response<int>> CreateProject(CreateProjectDto createProjectDto, Guid userId)
        {
            try
            {
                var sql = @"
                    INSERT INTO projects (name, description, created_at, owner_id) 
                    VALUES (@Name, @Description, @CreatedAt, @OwnerId)
                ";

                var parameters = new
                {
                    Name = createProjectDto.ProjectName,
                    Description = createProjectDto.ProjectDescription,
                    CreatedAt = DateTime.UtcNow,
                    OwnerId = userId
                };

                var rowsAffected = await ExecuteAsync(sql, parameters);
                return Response<int>.Success(rowsAffected, "Project created successfully");
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
                        t.status AS TaskStatus,
                        t.assignee_id AS TaskAssigneeId
                    FROM projects p
                    LEFT JOIN tasks t ON t.project_id = p.id
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
                            AssigneeId = reader.IsDBNull(reader.GetOrdinal("TaskAssigneeId")) ? null : reader.GetGuid(reader.GetOrdinal("TaskAssigneeId"))
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

        public async Task<Response<bool>> DeleteProject(DeleteProjectDto deleteProjectDto)
        {
            try
            {
                var sql = "DELETE FROM projects WHERE id = @ProjectId AND owner_id = @UserId";
                var rowsAffected = await ExecuteAsync(sql, new { ProjectId = deleteProjectDto.ProjectId, UserId = deleteProjectDto.UserId });
                if (rowsAffected > 0)
                    return Response<bool>.Success(true, "Project deleted successfully");

                throw new KeyNotFoundException("Project not found or could not be deleted");
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting project: {ex.Message}");
                throw new InvalidOperationException("Error deleting project", ex);
            }
        }

        public async Task<Response<IEnumerable<Tasks>>> GetTasksByProjectIdAsync(Guid projectId, Guid? assigneeId, string? status)
        {
            try
            {
                var sql = @"
                    SELECT id, title as name, description, status, assignee_id as AssigneeId,priority,project_id as ProjectId,created_at as CreatedAt,updated_at as UpdatedAt,due_date as DueDate
                    FROM tasks
                    WHERE project_id = @ProjectId
                      AND (CAST(@AssigneeId AS uuid) IS NULL OR assignee_id = CAST(@AssigneeId AS uuid))
                      AND (CAST(@Status AS task_status) IS NULL OR status = CAST(@Status AS task_status))
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching project tasks: {ex.Message}");
                throw new InvalidOperationException("Error fetching project tasks", ex);
            }
        }

        public async Task<Response<Guid>> CreateTask(CreateTaskDto createTaskDto, Guid projectId, Guid userId)
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
                return Response<Guid>.Success(taskId, $"Task Id : {taskId}");
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

        public async Task<Response<Guid>> UpdateTask(UpdateTaskDto updateTaskDto, Guid taskId)
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
                    throw new KeyNotFoundException("Task not found or you do not have permission to update it");

                var updatedId = result is Guid g ? g : Guid.Parse(result.ToString()!);
                return Response<Guid>.Success(updatedId, "Task updated successfully");
            }
            catch (ValidationException)
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
                    throw new KeyNotFoundException("Task not found or you do not have permission to delete it");

                var deletedId = result is Guid g ? g : Guid.Parse(result.ToString()!);
                return Response<Guid>.Success(deletedId, "Task deleted successfully");
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
    }
}
