using MachineMonitoring.Application;
using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;
using MachineMonitoring.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests;

public class CachedMachineDiagnosticServiceTests
{
    [Fact]
    public async Task GetDiagnosticAsync_WhenCalledTwice_ShouldUseCachedValue()
    {
        // Arrange
        CountingRetryingMachineDiagnosticService innerService = new();

        using MemoryCache cache = new(new MemoryCacheOptions());

        DiagnosticCacheOptions options = new() { Enabled = true, DurationSeconds = 30 };

        CachedMachineDiagnosticService service = new(
            innerService: innerService,
            cache: cache,
            options: Options.Create(options),
            logger: NullLogger<CachedMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        // Act
        MachineDiagnostic first = await service.GetDiagnosticAsync(machine, CancellationToken.None);

        MachineDiagnostic second = await service.GetDiagnosticAsync(
            machine,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(1, innerService.CallCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetDiagnosticAsync_WithDifferentMachines_ShouldUseDifferentCacheEntries()
    {
        // Arrange
        CountingRetryingMachineDiagnosticService innerService = new();

        using MemoryCache cache = new(new MemoryCacheOptions());

        DiagnosticCacheOptions options = new() { Enabled = true, DurationSeconds = 30 };

        CachedMachineDiagnosticService service = new(
            innerService,
            cache,
            Options.Create(options),
            NullLogger<CachedMachineDiagnosticService>.Instance
        );

        Machine firstMachine = CreateMachine("M-001");
        Machine secondMachine = CreateMachine("M-002");

        // Act
        MachineDiagnostic first = await service.GetDiagnosticAsync(
            firstMachine,
            CancellationToken.None
        );

        MachineDiagnostic second = await service.GetDiagnosticAsync(
            secondMachine,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(2, innerService.CallCount);
        Assert.Equal("M-001", first.MachineId);
        Assert.Equal("M-002", second.MachineId);
    }

    [Fact]
    public async Task GetDiagnosticAsync_WhenCacheIsDisabled_ShouldCallInnerServiceEveryTime()
    {
        // Arrange
        CountingRetryingMachineDiagnosticService innerService = new();

        using MemoryCache cache = new(new MemoryCacheOptions());

        DiagnosticCacheOptions options = new() { Enabled = false, DurationSeconds = 30 };

        CachedMachineDiagnosticService service = new(
            innerService,
            cache,
            Options.Create(options),
            NullLogger<CachedMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        // Act
        await service.GetDiagnosticAsync(machine, CancellationToken.None);

        await service.GetDiagnosticAsync(machine, CancellationToken.None);

        // Assert
        Assert.Equal(2, innerService.CallCount);
    }

    [Fact]
    public async Task Remove_WhenDiagnosticIsCached_ShouldForceNextRetrieval()
    {
        // Arrange
        CountingRetryingMachineDiagnosticService innerService = new();

        using MemoryCache cache = new(new MemoryCacheOptions());

        DiagnosticCacheOptions options = new() { Enabled = true, DurationSeconds = 30 };

        CachedMachineDiagnosticService service = new(
            innerService,
            cache,
            Options.Create(options),
            NullLogger<CachedMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        await service.GetDiagnosticAsync(machine, CancellationToken.None);

        // Act
        service.Remove(machine.Id);

        await service.GetDiagnosticAsync(machine, CancellationToken.None);

        // Assert
        Assert.Equal(2, innerService.CallCount);
    }

    private static Machine CreateMachine(string id)
    {
        return new Machine(
            id: id,
            name: $"Machine {id}",
            status: MachineStatus.Running,
            location: "Test Area",
            serialNumber: $"SN-{id}"
        );
    }
}
