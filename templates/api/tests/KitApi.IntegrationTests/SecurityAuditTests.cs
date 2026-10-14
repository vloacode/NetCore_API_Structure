using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using KitApi.Application.Features.Auth;

namespace KitApi.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class SecurityAuditTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FailedLogin_IsRecordedAndVisibleToAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = TestUsers.AdminEmail, password = "contraseña-incorrecta" }, ct);
        var admin = await factory.CreateAdminClientAsync();

        var page = await admin.GetFromJsonAsync<JsonElement>($"/api/security-events?type={SecurityEventTypes.LoginFailed}", ct);

        page.GetProperty("totalCount").GetInt32().ShouldBe(1);
        var item = page.GetProperty("items")[0];
        item.GetProperty("userEmail").GetString().ShouldBe(TestUsers.AdminEmail);
        item.GetProperty("details").GetString().ShouldBe("Contraseña incorrecta");
    }

    [Fact]
    public async Task SecurityEvents_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/security-events", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
