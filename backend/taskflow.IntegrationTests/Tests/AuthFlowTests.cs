using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using taskflow.IntegrationTests.Fixtures;

namespace taskflow.IntegrationTests.Tests;


public sealed class AuthFlowTests(TaskFlowFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Register_ThenLogin_ReturnsJwt()
    {
        var email    = UniqueEmail("auth");
        var password = "P@ssword123!";
        var username = $"user_{Guid.NewGuid().ToString("N")[..8]}";

        var registerResp = await Client.PostAsJsonAsync("/auth/Register", new
        {
            email,
            password,
            username
        });

        registerResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResp = await Client.PostAsJsonAsync("/auth/Login", new { email, password });

        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await ReadDataAsync(loginResp);
        var token = data.GetProperty("accessToken").GetString();

        token.Should().NotBeNullOrWhiteSpace("login should return a valid JWT");

        token!.Split('.').Should().HaveCount(3, "a valid JWT has exactly 3 parts");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email    = UniqueEmail("dup");
        var password = "P@ssword123!";
        var username = $"user_{Guid.NewGuid().ToString("N")[..8]}";

        await Client.PostAsJsonAsync("/auth/Register", new { email, password, username });

        var resp = await Client.PostAsJsonAsync("/auth/Register", new
        {
            email,
            password,
            username = $"other_{Guid.NewGuid().ToString("N")[..8]}"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email    = UniqueEmail("wrong");
        var password = "P@ssword123!";
        var username = $"user_{Guid.NewGuid().ToString("N")[..8]}";

        await Client.PostAsJsonAsync("/auth/Register", new { email, password, username });

        var resp = await Client.PostAsJsonAsync("/auth/Login", new
        {
            email,
            password = "definitely-wrong"
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
