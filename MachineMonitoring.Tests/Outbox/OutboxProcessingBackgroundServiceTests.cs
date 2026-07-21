using MachineMonitoring.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MachineMonitoring.Tests.Outbox;

public sealed class OutboxProcessingBackgroundServiceTests
{
    [Fact]
    public async Task StartsProcessingImmediately()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);

        await controller.WaitForInvocationCountAsync(1);

        Assert.Single(controller.Invocations);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task CreatesNewScopeForEachIteration()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(2);

        Assert.Equal(2, controller.CreatedInstanceIds.Count);
        Assert.NotEqual(controller.CreatedInstanceIds[0], controller.CreatedInstanceIds[1]);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task CallsProcessorWithConfiguredBatchSize()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller, batchSize: 37);

        await context.Worker.StartAsync(CancellationToken.None);
        TestProcessorInvocation invocation = await controller.WaitForInvocationAsync(1);

        Assert.Equal(37, invocation.BatchSize);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task EmptyBatchDelaysBeforeNextIteration()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);
        await AssertInvocationCountRemainsAsync(controller, 1);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(2);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task PartialBatchDelaysBeforeNextIteration()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(1, 1, 0));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller, batchSize: 5);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);
        await AssertInvocationCountRemainsAsync(controller, 1);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(2);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task FullSuccessfulBatchStartsNextIterationImmediately()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(5, 5, 0));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller, batchSize: 5);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(2);

        Assert.Equal(2, controller.Invocations.Count);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task FullBatchWithFailuresDelaysBeforeRetry()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(5, 4, 1));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller, batchSize: 5);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);
        await AssertInvocationCountRemainsAsync(controller, 1);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(2);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task ProcessorExceptionDelaysBeforeRetry()
    {
        TestProcessorController controller = new();
        controller.EnqueueException(new InvalidOperationException("boom"));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);
        await AssertInvocationCountRemainsAsync(controller, 1);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(2);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task CancellationDuringDelayStopsCleanly()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);

        await context.StopWorkerAsync();

        Assert.Single(controller.Invocations);
    }

    [Fact]
    public async Task ProcessorCancellationWithStoppingTokenStopsCleanly()
    {
        TestProcessorController controller = new();
        TaskCompletionSource started = CreateSignal();

        controller.EnqueueBehavior(
            async (_, cancellationToken) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new OutboxProcessingResult(0, 0, 0);
            }
        );

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await context.StopWorkerAsync();

        Assert.Single(controller.Invocations);
    }

    [Fact]
    public async Task ScopeIsDisposedAfterEveryIteration()
    {
        TestProcessorController controller = new();
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForDisposedCountAsync(1);

        context.WakeUp();
        await controller.WaitForDisposedCountAsync(2);

        Assert.Equal(2, controller.DisposedInstanceIds.Count);

        await context.StopWorkerAsync();
    }

    [Fact]
    public async Task RepeatedErrorsDoNotBusyLoop()
    {
        TestProcessorController controller = new();
        controller.EnqueueException(new InvalidOperationException("boom-1"));
        controller.EnqueueException(new InvalidOperationException("boom-2"));
        controller.EnqueueResult(new OutboxProcessingResult(0, 0, 0));

        await using TestWorkerContext context = CreateWorkerContext(controller);

        await context.Worker.StartAsync(CancellationToken.None);
        await controller.WaitForInvocationCountAsync(1);
        await AssertInvocationCountRemainsAsync(controller, 1);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(2);
        await AssertInvocationCountRemainsAsync(controller, 2);

        context.WakeUp();
        await controller.WaitForInvocationCountAsync(3);

        await context.StopWorkerAsync();
    }

    private static TestWorkerContext CreateWorkerContext(
        TestProcessorController controller,
        int batchSize = 5,
        int pollingIntervalSeconds = 5
    )
    {
        OutboxWakeUpSignal wakeUpSignal = new();
        ServiceCollection services = new();

        services.AddScoped<IOutboxProcessor>(_ => new ScopedTestOutboxProcessor(controller));

        ServiceProvider serviceProvider = services.BuildServiceProvider(validateScopes: true);

        OutboxProcessingBackgroundService worker = new(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new OutboxProcessingOptions
                {
                    BatchSize = batchSize,
                    PollingIntervalSeconds = pollingIntervalSeconds,
                }
            ),
            wakeUpSignal,
            NullLogger<OutboxProcessingBackgroundService>.Instance
        );

        return new TestWorkerContext(
            serviceProvider,
            worker,
            wakeUpSignal,
            TimeSpan.FromSeconds(pollingIntervalSeconds)
        );
    }

    private static async Task AssertInvocationCountRemainsAsync(
        TestProcessorController controller,
        int expectedCount
    )
    {
        await Task.Delay(100);
        Assert.Equal(expectedCount, controller.Invocations.Count);
    }

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class TestWorkerContext : IAsyncDisposable
    {
        public TestWorkerContext(
            ServiceProvider serviceProvider,
            OutboxProcessingBackgroundService worker,
            OutboxWakeUpSignal wakeUpSignal,
            TimeSpan pollingInterval
        )
        {
            ServiceProvider = serviceProvider;
            Worker = worker;
            WakeUpSignal = wakeUpSignal;
            PollingInterval = pollingInterval;
        }

        public ServiceProvider ServiceProvider { get; }

        public OutboxProcessingBackgroundService Worker { get; }

        public OutboxWakeUpSignal WakeUpSignal { get; }

        public TimeSpan PollingInterval { get; }

        public void WakeUp() => WakeUpSignal.Notify();

        public Task StopWorkerAsync() => Worker.StopAsync(CancellationToken.None);

        public ValueTask DisposeAsync() => ServiceProvider.DisposeAsync();
    }

    private sealed class ScopedTestOutboxProcessor : IOutboxProcessor, IAsyncDisposable
    {
        private readonly TestProcessorController _controller;
        private readonly Guid _instanceId = Guid.NewGuid();

        public ScopedTestOutboxProcessor(TestProcessorController controller)
        {
            ArgumentNullException.ThrowIfNull(controller);

            _controller = controller;
            _controller.CreatedInstanceIds.Add(_instanceId);
        }

        public Task<OutboxProcessingResult> ProcessPendingAsync(
            int batchSize,
            CancellationToken cancellationToken
        ) => _controller.InvokeAsync(_instanceId, batchSize, cancellationToken);

        public ValueTask DisposeAsync()
        {
            _controller.DisposedInstanceIds.Add(_instanceId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestProcessorController
    {
        private readonly Lock _lock = new();
        private readonly Queue<Func<TestProcessorInvocation, CancellationToken, Task<OutboxProcessingResult>>> _behaviors =
            new();

        public List<TestProcessorInvocation> Invocations { get; } = [];

        public List<Guid> CreatedInstanceIds { get; } = [];

        public List<Guid> DisposedInstanceIds { get; } = [];

        public void EnqueueResult(OutboxProcessingResult result) =>
            EnqueueBehavior((_, _) => Task.FromResult(result));

        public void EnqueueException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            EnqueueBehavior((_, _) => Task.FromException<OutboxProcessingResult>(exception));
        }

        public void EnqueueBehavior(
            Func<TestProcessorInvocation, CancellationToken, Task<OutboxProcessingResult>> behavior
        )
        {
            ArgumentNullException.ThrowIfNull(behavior);

            lock (_lock)
            {
                _behaviors.Enqueue(behavior);
            }
        }

        public Task<OutboxProcessingResult> InvokeAsync(
            Guid instanceId,
            int batchSize,
            CancellationToken cancellationToken
        )
        {
            TestProcessorInvocation invocation = new(instanceId, batchSize, cancellationToken);
            Func<TestProcessorInvocation, CancellationToken, Task<OutboxProcessingResult>> behavior;

            lock (_lock)
            {
                Invocations.Add(invocation);
                behavior = _behaviors.Count > 0
                    ? _behaviors.Dequeue()
                    : static (_, _) => Task.FromResult(new OutboxProcessingResult(0, 0, 0));
            }

            return behavior(invocation, cancellationToken);
        }

        public async Task WaitForInvocationCountAsync(int expectedCount)
        {
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));

            while (true)
            {
                lock (_lock)
                {
                    if (Invocations.Count >= expectedCount)
                    {
                        return;
                    }
                }

                await Task.Delay(10, cancellationTokenSource.Token);
            }
        }

        public async Task<TestProcessorInvocation> WaitForInvocationAsync(int expectedCount)
        {
            await WaitForInvocationCountAsync(expectedCount);

            lock (_lock)
            {
                return Invocations[expectedCount - 1];
            }
        }

        public async Task WaitForDisposedCountAsync(int expectedCount)
        {
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(2));

            while (true)
            {
                lock (_lock)
                {
                    if (DisposedInstanceIds.Count >= expectedCount)
                    {
                        return;
                    }
                }

                await Task.Delay(10, cancellationTokenSource.Token);
            }
        }
    }

    private sealed record TestProcessorInvocation(
        Guid InstanceId,
        int BatchSize,
        CancellationToken CancellationToken
    );
}
