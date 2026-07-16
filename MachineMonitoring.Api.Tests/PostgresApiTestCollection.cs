namespace MachineMonitoring.Api.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresApiTestCollection : ICollectionFixture<PostgresWebApplicationFactory>
{
    public const string Name = "PostgreSQL API integration tests";
}
