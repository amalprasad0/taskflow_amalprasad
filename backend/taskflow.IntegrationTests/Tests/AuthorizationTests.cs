using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using taskflow.IntegrationTests.Fixtures;

namespace taskflow.IntegrationTests.Tests;


public sealed class AuthorizationTests(TaskFlowFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task DeleteTask_AsNonOwner_Returns403()
    {
        var ownerJwt = await RegisterAndLoginAsync(
            UniqueEmail("owner"), "P@ssword123!", $"owner_{Guid.NewGuid().ToString("N")[..8]}");
        AuthenticatedClient(ownerJwt);

        var createProjResp = await Client.PostAsJsonAsync("/projects", new
        {
            projectName        = "Owner's Project",
            projectDescription = "Belongs to owner"
        });
        createProjResp.EnsureSuccessStatusCode();

        var projData  = await ReadDataAsync(createProjResp);
        var projectId = projData.GetProperty("projectId").GetString()!;

        var createTaskResp = await Client.PostAsJsonAsync(
            $"/project/{projectId}/tasks", new
            {
                title    = "Owner Task",
                status   = "todo",
                priority = "low"
            });
        createTaskResp.EnsureSuccessStatusCode();

        var taskData = await ReadDataAsync(createTaskResp);
        var taskId   = taskData.GetProperty("taskId").GetString()!;

        var nonOwnerJwt = await RegisterAndLoginAsync(
            UniqueEmail("nonowner"), "P@ssword123!", $"nonowner_{Guid.NewGuid().ToString("N")[..8]}");
        AuthenticatedClient(nonOwnerJwt); 

        var deleteResp = await Client.DeleteAsync($"/task/{taskId}");

        deleteResp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a user who doesn't own the task or its project must receive 403");
    }

    [Fact]
    public async Task DeleteTask_AsOwner_Succeeds()
    {
        var ownerJwt = await RegisterAndLoginAsync(
            UniqueEmail("delowner"), "P@ssword123!", $"delowner_{Guid.NewGuid().ToString("N")[..8]}");
        AuthenticatedClient(ownerJwt);

        var projResp = await Client.PostAsJsonAsync("/projects", new
        {
            projectName        = "Delete Test Project",
            projectDescription = ""
        });
        var projId = (await ReadDataAsync(projResp)).GetProperty("projectId").GetString()!;

        var taskResp = await Client.PostAsJsonAsync($"/project/{projId}/tasks", new
        {
            title = "Task to delete", status = "todo", priority = "low"
        });
        var taskId = (await ReadDataAsync(taskResp)).GetProperty("taskId").GetString()!;

        var deleteResp = await Client.DeleteAsync($"/task/{taskId}");

        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "the task owner should be able to delete their own task");
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithoutJwt_Returns401()
    {
        var resp = await Client.GetAsync("/projects");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
