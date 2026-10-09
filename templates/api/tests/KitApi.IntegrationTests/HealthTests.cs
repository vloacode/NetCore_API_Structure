using System.Net;
using Shouldly;

namespace KitApi.IntegrationTests;

/// <summary>Tests base del proyecto: la API arranca contra el motor real y responde.</summary>
[Collection(nameof(ApiCollection))]
public sealed class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Ready_WithDatabase_Returns200()
    {
        var response = await factory.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("X-Trace-Id").ShouldBeTrue();
    }
#if (security)

    [Fact]
    public async Task Me_AsSeededAdmin_Returns200()
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/account/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/account/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
}
