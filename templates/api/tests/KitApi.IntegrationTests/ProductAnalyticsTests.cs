using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using KitApi.Application.Abstractions.Analytics;
using KitApi.Infrastructure.Analytics;
using KitApi.Infrastructure.Persistence;

namespace KitApi.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class ProductAnalyticsTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Track_ServerEvent_IsStoredWithoutPersonalData()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IAnalyticsTracker>().Track("test.server_event",
                new Dictionary<string, object?> { ["plan"] = "pro", ["email"] = "someone@example.com" });
        }

        var stored = await WaitForEventAsync("test.server_event");

        stored.Source.ShouldBe("server");
        stored.Properties.ShouldContain("plan");
        stored.Properties.ShouldNotContain("email");
    }

    [Fact]
    public async Task PostEvents_FromClient_Returns202AndStoresOnlyPseudonym()
    {
        var anonymousId = Guid.NewGuid().ToString();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AnalyticsTracker.AnonymousIdHeader, anonymousId);

        var response = await client.PostAsJsonAsync("/api/analytics/events",
            new { events = new[] { new { name = AnalyticsEvents.SearchNoResults, properties = new { term_length = 7 } } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var stored = await WaitForEventAsync(AnalyticsEvents.SearchNoResults);
        stored.Source.ShouldBe("client");
        stored.AnonymousId.ShouldNotBeNullOrEmpty();
        stored.AnonymousId.ShouldNotBe(anonymousId);   // seudónimo, nunca el valor real
    }

    [Fact]
    public async Task PostEvents_WithPersonalData_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/analytics/events",
            new { events = new[] { new { name = AnalyticsEvents.PageViewed, properties = new { email = "a@b.com" } } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostEvents_WithUnknownEvent_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/analytics/events",
            new { events = new[] { new { name = "admin.deleted_everything" } } }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<AnalyticsEvent> WaitForEventAsync(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AnalyticsEvent>()
                .AsNoTracking().FirstOrDefaultAsync(e => e.Name == name, ct);
            if (stored is not null)
                return stored;
            await Task.Delay(100, ct);
        }
        throw new ShouldAssertException($"No se registró el evento {name}");
    }
}
