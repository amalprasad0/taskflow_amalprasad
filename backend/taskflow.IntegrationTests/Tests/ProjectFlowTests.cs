using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using taskflow.IntegrationTests.Fixtures;

namespace taskflow.IntegrationTests.Tests;


public sealed class ProjectFlowTests(TaskFlowFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateProject_ThenTask_VerifiedInGetProject()
    {
        var jwt = await RegisterAndLoginAsync(
            UniqueEmail("proj"), "P@ssword123!", $"projuser_{Guid.NewGuid().ToString("N")[..8]}");
        AuthenticatedClient(jwt);

        var createProjResp = await Client.PostAsJsonAsync("/projects", new
        {
            projectName        = "Integration Test Project",
            projectDescription = "Created by integration tests"
        });

        createProjResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var projData   = await ReadDataAsync(createProjResp);
        var projectId  = projData.GetProperty("projectId").GetString();
        projectId.Should().NotBeNullOrWhiteSpace();

        var createTaskResp = await Client.PostAsJsonAsync(
            $"/project/{projectId}/tasks", new
            {
                title       = "Integration Task",
                description = "Test task",
                status      = "todo",
                priority    = "medium"
            });

        createTaskResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var taskData = await ReadDataAsync(createTaskResp);
        var taskId   = taskData.GetProperty("taskId").GetString();
        taskId.Should().NotBeNullOrWhiteSpace();

        var getResp = await Client.GetAsync($"/project/{projectId}");

        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var projectDetail = await ReadDataAsync(getResp);
        projectDetail.GetProperty("name").GetString()
            .Should().Be("Integration Test Project");

        var tasks = projectDetail.GetProperty("tasks").EnumerateArray().ToList();
        tasks.Should().NotBeEmpty("the task we created should appear");
        tasks.Should().ContainSingle(t =>
            t.GetProperty("id").GetString() == taskId);
    }

    [Fact]
    public async Task GetProject_UnknownId_Returns404()
    {
        var jwt = await RegisterAndLoginAsync(
            UniqueEmail("404"), "P@ssword123!", $"user404_{Guid.NewGuid().ToString("N")[..8]}");
        AuthenticatedClient(jwt);

        var resp = await Client.GetAsync($"/project/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
