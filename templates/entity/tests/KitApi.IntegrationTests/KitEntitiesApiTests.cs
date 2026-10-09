using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
#if (hasParent)
using KitApi.Features.ParentFeature;
#endif

namespace KitApi.IntegrationTests;

/// <summary>Casos mínimos por entidad (standards/12). Agregar uno por criterio de aceptación del módulo.</summary>
[Collection(nameof(ApiCollection))]
public sealed class KitEntitiesApiTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Route = "/api/kitentities";

    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409WithCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateClientAsync();
#if (hasParent)
        var parent = await factory.SeedAsync(new KitParent { Name = "Padre", Code = "P-1" });
        var request = new { name = "Uno", code = "A-1", kitParentId = parent.Id };
#else
        var request = new { name = "Uno", code = "A-1" };
#endif
        (await client.PostAsJsonAsync(Route, request, ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync(Route, request, ct);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(ct);
        problem.GetProperty("code").GetString().ShouldBe("KitEntity.CodeAlreadyExists");
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404WithCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateClientAsync();

        var response = await client.GetAsync($"{Route}/999999", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        problem.GetProperty("code").GetString().ShouldBe("KitEntity.NotFound");
    }
#if (security)

    [Fact]
    public async Task GetPaged_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
#endif
#if (security)

    private Task<HttpClient> CreateClientAsync() => factory.CreateAdminClientAsync();
#elif (useApiKey)

    private Task<HttpClient> CreateClientAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.TestApiKey);
        return Task.FromResult(client);
    }
#else

    private Task<HttpClient> CreateClientAsync() => Task.FromResult(factory.CreateClient());
#endif
}
