using MachineMonitoring.Application;
using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
using MachineMonitoring.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests;

public class RetryingMachineDiagnosticServiceTests
{
    [Fact]
    public async Task GetDiagnosticAsync_WhenTransientFailuresStop_ShouldReturnDiagnostic()
    {
        // Arrange
        FlakyMachineDiagnosticService innerService = new(failuresBeforeSuccess: 2);

        DiagnosticRetryOptions options = new() { MaxRetryAttempts = 2, DelayMilliseconds = 1 };

        RetryingMachineDiagnosticService service = new(
            innerService: innerService,
            options: Options.Create(options),
            logger: NullLogger<RetryingMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        // Act
        MachineDiagnostic diagnostic = await service.GetDiagnosticAsync(
            machine,
            CancellationToken.None
        );

        // Assert
        Assert.Equal("M-001", diagnostic.MachineId);
        Assert.Equal(3, innerService.AttemptCount);
    }

    [Fact]
    public async Task GetDiagnosticAsync_WhenRetriesAreExhausted_ShouldThrowUnavailableException()
    {
        // Arrange
        FlakyMachineDiagnosticService innerService = new(failuresBeforeSuccess: 10);

        DiagnosticRetryOptions options = new() { MaxRetryAttempts = 2, DelayMilliseconds = 1 };

        RetryingMachineDiagnosticService service = new(
            innerService: innerService,
            options: Options.Create(options),
            logger: NullLogger<RetryingMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        // Act
        Func<Task> action = () => service.GetDiagnosticAsync(machine, CancellationToken.None);

        // Assert
        MachineDiagnosticUnavailableException exception =
            await Assert.ThrowsAsync<MachineDiagnosticUnavailableException>(action);

        Assert.Equal(3, innerService.AttemptCount);

        Assert.IsType<TransientMachineDiagnosticException>(exception.InnerException);
    }

    [Fact]
    public async Task GetDiagnosticAsync_WhenFailureIsPermanent_ShouldNotRetry()
    {
        // Arrange
        PermanentlyFailingDiagnosticService innerService = new();

        DiagnosticRetryOptions options = new() { MaxRetryAttempts = 5, DelayMilliseconds = 1 };

        RetryingMachineDiagnosticService service = new(
            innerService: innerService,
            options: Options.Create(options),
            logger: NullLogger<RetryingMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        // Act
        Func<Task> action = () => service.GetDiagnosticAsync(machine, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<MachineDiagnosticUnavailableException>(action);

        Assert.Equal(1, innerService.AttemptCount);
    }

    [Fact]
    public async Task GetDiagnosticAsync_WhenCancelledDuringDelay_ShouldStopRetrying()
    {
        // Arrange
        FlakyMachineDiagnosticService innerService = new(failuresBeforeSuccess: 10);

        DiagnosticRetryOptions options = new() { MaxRetryAttempts = 5, DelayMilliseconds = 10_000 };

        RetryingMachineDiagnosticService service = new(
            innerService: innerService,
            options: Options.Create(options),
            logger: NullLogger<RetryingMachineDiagnosticService>.Instance
        );

        Machine machine = CreateMachine("M-001");

        using CancellationTokenSource source = new();

        source.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act
        Func<Task> action = () => service.GetDiagnosticAsync(machine, source.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);

        Assert.Equal(1, innerService.AttemptCount);
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

    private sealed class PermanentlyFailingDiagnosticService : ILimitedMachineDiagnosticService
    {
        public int AttemptCount { get; private set; }

        public Task<MachineDiagnostic> GetDiagnosticAsync(
            Machine machine,
            CancellationToken cancellationToken
        )
        {
            AttemptCount++;

            throw new MachineDiagnosticUnavailableException(
                machineId: machine.Id,
                message: "Permanent diagnostic failure."
            );
        }
    }
}
