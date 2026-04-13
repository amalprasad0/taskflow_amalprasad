using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using taskflow.IntegrationTests.Fixtures;

namespace taskflow.IntegrationTests;


[Collection("Integration")]
public abstract class IntegrationTestBase
{
    protected readonly HttpClient Client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected IntegrationTestBase(TaskFlowFactory factory)
    {
        Client = factory.CreateClient();
    }

    protected async Task<string> RegisterAndLoginAsync(string email, string password, string username)
    {
        var registerResp = await Client.PostAsJsonAsync("/auth/Register", new
        {
            email,
            password,
            username
        });
        registerResp.EnsureSuccessStatusCode();

        var loginResp = await Client.PostAsJsonAsync("/auth/Login", new { email, password });
        loginResp.EnsureSuccessStatusCode();

        var body = await loginResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement
                   .GetProperty("data")
                   .GetProperty("accessToken")
                   .GetString()!;
    }

    protected HttpClient AuthenticatedClient(string jwt)
    {
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return Client;
    }

    protected static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }

    protected static string UniqueEmail(string prefix = "user")
        => $"{prefix}_{Guid.NewGuid():N}@test.com";
}
