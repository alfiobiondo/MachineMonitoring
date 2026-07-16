using System.Net;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresHealthEndpointTests
{
    private readonly HttpClient _client;

    public PostgresHealthEndpointTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetReadyHealth_WhenPostgresIsRunning_ReturnsOk()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
