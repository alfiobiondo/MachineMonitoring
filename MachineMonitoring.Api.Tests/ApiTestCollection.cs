namespace MachineMonitoring.Api.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "API integration tests";
}
