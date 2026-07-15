using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MachineMonitoring.Api.Tests;

public sealed class OperationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OperationEndpointTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetOperations_WithInvalidStatus_ReturnsBadRequest()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync(
            "/api/operations?status=UnknownStatus"
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);

        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);

        Assert.Equal("Invalid request", problemDetails.Title);

        Assert.Contains("UnknownStatus", problemDetails.Detail);
    }
}
