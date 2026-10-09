using System.Net.Http.Json;
using KitApi.Application.Features.Auth;

namespace KitApi.IntegrationTests;

public static class TestUsers
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Test#Pass12345";

    public static async Task<HttpClient> CreateAdminClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login!.Tokens!.AccessToken);
        return client;
    }
}
