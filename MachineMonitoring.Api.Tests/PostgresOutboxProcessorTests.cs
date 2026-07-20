using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Models;
using MachineMonitoring.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class PostgresOutboxProcessorTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public PostgresOutboxProcessorTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenBatchSizeIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        await DeleteTestOutboxMessagesAsync();
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            new RecordingOutboxMessageDispatcher(),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero))
        );

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            processor.ProcessPendingAsync(0, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenNoPendingMessages_ReturnsZeroWithoutCallingDispatcher()
    {
        await DeleteTestOutboxMessagesAsync();
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        RecordingOutboxMessageDispatcher dispatcher = new();
        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 5, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            5,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(0, 0, 0), result);
        Assert.Empty(dispatcher.Messages);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenDispatchSucceeds_MarksMessageProcessed()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid recordId = Guid.NewGuid();
        DateTimeOffset processedAt = new(2026, 7, 20, 8, 10, 0, TimeSpan.Zero);
        await InsertOutboxMessagesAsync(
            CreateRecord(recordId, "success", new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero))
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(processedAt)
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            5,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(1, 1, 0), result);
        Assert.Equal(result.AttemptedCount, result.SucceededCount + result.FailedCount);
        Assert.Single(dispatcher.Messages);
        Assert.Equal(recordId, dispatcher.Messages[0].Id);

        OutboxMessageRecord storedRecord = await GetRequiredOutboxMessageAsync(recordId);
        Assert.Equal(processedAt, storedRecord.ProcessedAt);
        Assert.Equal(0, storedRecord.Attempts);
        Assert.Null(storedRecord.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenDispatchFails_IncrementsAttemptsAndStoresLastError()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid recordId = Guid.NewGuid();
        await InsertOutboxMessagesAsync(
            CreateRecord(recordId, "failure", new DateTimeOffset(2026, 7, 20, 8, 15, 0, TimeSpan.Zero))
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueFailure(new InvalidOperationException("Dispatch failed."));

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 20, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            5,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(1, 0, 1), result);
        Assert.Equal(result.AttemptedCount, result.SucceededCount + result.FailedCount);

        OutboxMessageRecord storedRecord = await GetRequiredOutboxMessageAsync(recordId);
        Assert.Null(storedRecord.ProcessedAt);
        Assert.Equal(1, storedRecord.Attempts);
        Assert.Equal("Dispatch failed.", storedRecord.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenPreviouslyFailedMessageSucceeds_ClearsErrorAndPreservesAttempts()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid recordId = Guid.NewGuid();
        await InsertOutboxMessagesAsync(
            CreateRecord(recordId, "retry", new DateTimeOffset(2026, 7, 20, 8, 25, 0, TimeSpan.Zero))
        );

        await using AsyncServiceScope firstScope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher failingDispatcher = new();
        failingDispatcher.EnqueueFailure(new InvalidOperationException("Transient dispatch failure."));
        OutboxProcessor failingProcessor = CreateProcessor(
            firstScope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            failingDispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 26, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult firstResult = await failingProcessor.ProcessPendingAsync(
            5,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(1, 0, 1), firstResult);

        await using AsyncServiceScope secondScope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher succeedingDispatcher = new();
        DateTimeOffset processedAt = new(2026, 7, 20, 8, 27, 0, TimeSpan.Zero);
        OutboxProcessor succeedingProcessor = CreateProcessor(
            secondScope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            succeedingDispatcher,
            new FixedTimeProvider(processedAt)
        );

        OutboxProcessingResult secondResult = await succeedingProcessor.ProcessPendingAsync(
            5,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(1, 1, 0), secondResult);

        OutboxMessageRecord storedRecord = await GetRequiredOutboxMessageAsync(recordId);
        Assert.Equal(processedAt, storedRecord.ProcessedAt);
        Assert.Equal(1, storedRecord.Attempts);
        Assert.Null(storedRecord.LastError);

        await using AsyncServiceScope thirdScope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher thirdDispatcher = new();
        OutboxProcessor thirdProcessor = CreateProcessor(
            thirdScope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            thirdDispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 28, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult thirdResult = await thirdProcessor.ProcessPendingAsync(
            5,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(0, 0, 0), thirdResult);
        Assert.Empty(thirdDispatcher.Messages);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenBatchContainsSuccessFailureSuccess_PersistsMixedOutcomes()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Guid thirdId = Guid.NewGuid();
        DateTimeOffset baseTime = new(2026, 7, 20, 8, 30, 0, TimeSpan.Zero);

        await InsertOutboxMessagesAsync(
            CreateRecord(firstId, "mixed-1", baseTime),
            CreateRecord(secondId, "mixed-2", baseTime.AddMinutes(1)),
            CreateRecord(thirdId, "mixed-3", baseTime.AddMinutes(2))
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueSuccess();
        dispatcher.EnqueueFailure(new InvalidOperationException("Second failed."));
        dispatcher.EnqueueSuccess();

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 40, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            10,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(3, 2, 1), result);
        Assert.Equal(result.AttemptedCount, result.SucceededCount + result.FailedCount);

        OutboxMessageRecord firstRecord = await GetRequiredOutboxMessageAsync(firstId);
        OutboxMessageRecord secondRecord = await GetRequiredOutboxMessageAsync(secondId);
        OutboxMessageRecord thirdRecord = await GetRequiredOutboxMessageAsync(thirdId);

        Assert.NotNull(firstRecord.ProcessedAt);
        Assert.Null(firstRecord.LastError);
        Assert.Equal(0, firstRecord.Attempts);

        Assert.Null(secondRecord.ProcessedAt);
        Assert.Equal(1, secondRecord.Attempts);
        Assert.Equal("Second failed.", secondRecord.LastError);

        Assert.NotNull(thirdRecord.ProcessedAt);
        Assert.Null(thirdRecord.LastError);
        Assert.Equal(0, thirdRecord.Attempts);
    }

    [Fact]
    public async Task ProcessPendingAsync_OrdersPendingMessagesByCreatedAtThenId()
    {
        await DeleteTestOutboxMessagesAsync();

        DateTimeOffset createdAt = new(2026, 7, 20, 8, 45, 0, TimeSpan.Zero);
        Guid firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Guid thirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");

        await InsertOutboxMessagesAsync(
            CreateRecord(thirdId, "order-3", createdAt.AddMinutes(1)),
            CreateRecord(secondId, "order-2", createdAt),
            CreateRecord(firstId, "order-1", createdAt)
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueSuccess();
        dispatcher.EnqueueSuccess();
        dispatcher.EnqueueSuccess();

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 8, 50, 0, TimeSpan.Zero))
        );

        await processor.ProcessPendingAsync(10, CancellationToken.None);

        Assert.Equal([firstId, secondId, thirdId], dispatcher.Messages.Select(item => item.Id));
    }

    [Fact]
    public async Task ProcessPendingAsync_ProcessesAtMostBatchSizeMessages()
    {
        await DeleteTestOutboxMessagesAsync();

        DateTimeOffset createdAt = new(2026, 7, 20, 8, 55, 0, TimeSpan.Zero);
        OutboxMessageRecord firstRecord = CreateRecord(Guid.NewGuid(), "batch-1", createdAt);
        OutboxMessageRecord secondRecord = CreateRecord(
            Guid.NewGuid(),
            "batch-2",
            createdAt.AddMinutes(1)
        );
        OutboxMessageRecord thirdRecord = CreateRecord(
            Guid.NewGuid(),
            "batch-3",
            createdAt.AddMinutes(2)
        );

        await InsertOutboxMessagesAsync(firstRecord, secondRecord, thirdRecord);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueSuccess();
        dispatcher.EnqueueSuccess();

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            2,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(2, 2, 0), result);
        Assert.Equal(2, dispatcher.Messages.Count);

        OutboxMessageRecord storedThirdRecord = await GetRequiredOutboxMessageAsync(thirdRecord.Id);
        Assert.Null(storedThirdRecord.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenCancellationTokenIsCanceled_PropagatesWithoutMutatingCurrentOrNextRecords()
    {
        await DeleteTestOutboxMessagesAsync();

        OutboxMessageRecord firstRecord = CreateRecord(
            Guid.NewGuid(),
            "cancel-1",
            new DateTimeOffset(2026, 7, 20, 9, 5, 0, TimeSpan.Zero)
        );
        OutboxMessageRecord secondRecord = CreateRecord(
            Guid.NewGuid(),
            "cancel-2",
            new DateTimeOffset(2026, 7, 20, 9, 6, 0, TimeSpan.Zero)
        );

        await InsertOutboxMessagesAsync(firstRecord, secondRecord);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        using CancellationTokenSource cancellationTokenSource = new();

        dispatcher.EnqueueBehavior(
            (_, _) =>
            {
                cancellationTokenSource.Cancel();
                throw new OperationCanceledException(cancellationTokenSource.Token);
            }
        );

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 9, 7, 0, TimeSpan.Zero))
        );

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            processor.ProcessPendingAsync(10, cancellationTokenSource.Token)
        );

        Assert.Single(dispatcher.Messages);
        Assert.Equal(firstRecord.Id, dispatcher.Messages[0].Id);

        OutboxMessageRecord storedFirstRecord = await GetRequiredOutboxMessageAsync(firstRecord.Id);
        OutboxMessageRecord storedSecondRecord = await GetRequiredOutboxMessageAsync(secondRecord.Id);

        Assert.Null(storedFirstRecord.ProcessedAt);
        Assert.Equal(0, storedFirstRecord.Attempts);
        Assert.Null(storedFirstRecord.LastError);

        Assert.Null(storedSecondRecord.ProcessedAt);
        Assert.Equal(0, storedSecondRecord.Attempts);
        Assert.Null(storedSecondRecord.LastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenDispatcherThrowsOperationCanceledWithoutCancellation_TreatsItAsFailure()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        await InsertOutboxMessagesAsync(
            CreateRecord(firstId, "oce-failure-1", new DateTimeOffset(2026, 7, 20, 9, 10, 0, TimeSpan.Zero)),
            CreateRecord(secondId, "oce-failure-2", new DateTimeOffset(2026, 7, 20, 9, 11, 0, TimeSpan.Zero))
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueFailure(new OperationCanceledException("Dispatch timeout."));
        dispatcher.EnqueueSuccess();

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 9, 12, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            10,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(2, 1, 1), result);
        Assert.Equal(result.AttemptedCount, result.SucceededCount + result.FailedCount);

        OutboxMessageRecord firstRecord = await GetRequiredOutboxMessageAsync(firstId);
        OutboxMessageRecord secondRecord = await GetRequiredOutboxMessageAsync(secondId);

        Assert.Null(firstRecord.ProcessedAt);
        Assert.Equal(1, firstRecord.Attempts);
        Assert.Equal("Dispatch timeout.", firstRecord.LastError);
        Assert.NotNull(secondRecord.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_DoesNotPickAlreadyProcessedRecords()
    {
        await DeleteTestOutboxMessagesAsync();

        DateTimeOffset createdAt = new(2026, 7, 20, 9, 15, 0, TimeSpan.Zero);
        Guid processedId = Guid.NewGuid();
        Guid pendingId = Guid.NewGuid();

        await InsertOutboxMessagesAsync(
            CreateRecord(
                processedId,
                "already-processed",
                createdAt,
                processedAt: createdAt.AddMinutes(10),
                attempts: 0
            ),
            CreateRecord(pendingId, "still-pending", createdAt.AddMinutes(1))
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueSuccess();

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 9, 20, 0, TimeSpan.Zero))
        );

        OutboxProcessingResult result = await processor.ProcessPendingAsync(
            10,
            CancellationToken.None
        );

        Assert.Equal(new OutboxProcessingResult(1, 1, 0), result);
        Assert.Single(dispatcher.Messages);
        Assert.Equal(pendingId, dispatcher.Messages[0].Id);

        OutboxMessageRecord processedRecord = await GetRequiredOutboxMessageAsync(processedId);
        Assert.Equal(createdAt.AddMinutes(10), processedRecord.ProcessedAt);
    }

    [Fact]
    public async Task ProcessPendingAsync_NormalizesAndTruncatesLastError()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid recordId = Guid.NewGuid();
        await InsertOutboxMessagesAsync(
            CreateRecord(recordId, "last-error", new DateTimeOffset(2026, 7, 20, 9, 25, 0, TimeSpan.Zero))
        );

        string rawMessage = "Line1\r\nLine2\t" + new string('x', 1100);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueFailure(new InvalidOperationException(rawMessage));

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 9, 26, 0, TimeSpan.Zero))
        );

        await processor.ProcessPendingAsync(10, CancellationToken.None);

        OutboxMessageRecord storedRecord = await GetRequiredOutboxMessageAsync(recordId);
        Assert.NotNull(storedRecord.LastError);
        Assert.DoesNotContain('\r', storedRecord.LastError);
        Assert.DoesNotContain('\n', storedRecord.LastError);
        Assert.DoesNotContain('\t', storedRecord.LastError);
        Assert.Equal(1000, storedRecord.LastError.Length);
    }

    [Fact]
    public async Task ProcessPendingAsync_UsesFallbackMessageWhenExceptionMessageIsBlank()
    {
        await DeleteTestOutboxMessagesAsync();

        Guid recordId = Guid.NewGuid();
        await InsertOutboxMessagesAsync(
            CreateRecord(recordId, "fallback-error", new DateTimeOffset(2026, 7, 20, 9, 30, 0, TimeSpan.Zero))
        );

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        RecordingOutboxMessageDispatcher dispatcher = new();
        dispatcher.EnqueueFailure(new BlankMessageException());

        OutboxProcessor processor = CreateProcessor(
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>(),
            dispatcher,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 9, 31, 0, TimeSpan.Zero))
        );

        await processor.ProcessPendingAsync(10, CancellationToken.None);

        OutboxMessageRecord storedRecord = await GetRequiredOutboxMessageAsync(recordId);
        Assert.Equal("Dispatch failed with BlankMessageException.", storedRecord.LastError);
    }

    private static OutboxProcessor CreateProcessor(
        MachineMonitoringDbContext dbContext,
        IOutboxMessageDispatcher dispatcher,
        TimeProvider timeProvider
    ) => new(dbContext, dispatcher, timeProvider);

    private async Task InsertOutboxMessagesAsync(params OutboxMessageRecord[] records)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        dbContext.OutboxMessages.AddRange(records);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<OutboxMessageRecord> GetRequiredOutboxMessageAsync(Guid id)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        return await dbContext.OutboxMessages.AsNoTracking().SingleAsync(
            item => item.Id == id,
            CancellationToken.None
        );
    }

    private async Task DeleteTestOutboxMessagesAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        MachineMonitoringDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MachineMonitoringDbContext>();

        await dbContext.OutboxMessages.ExecuteDeleteAsync(CancellationToken.None);
    }

    private static OutboxMessageRecord CreateRecord(
        Guid id,
        string suffix,
        DateTimeOffset createdAt,
        DateTimeOffset? processedAt = null,
        int attempts = 0,
        string? lastError = null
    )
    {
        return new OutboxMessageRecord
        {
            Id = id,
            Type = $"test-outbox-processor-{suffix}",
            Payload = $$"""{"test":"{{suffix}}"}""",
            OccurredAt = createdAt.AddMinutes(-1),
            CreatedAt = createdAt,
            ProcessedAt = processedAt,
            Attempts = attempts,
            LastError = lastError,
        };
    }

    private sealed class RecordingOutboxMessageDispatcher : IOutboxMessageDispatcher
    {
        private readonly Queue<Func<OutboxDispatchMessage, CancellationToken, Task>> _behaviors = new();

        public List<OutboxDispatchMessage> Messages { get; } = [];

        public void EnqueueSuccess() => EnqueueBehavior((_, _) => Task.CompletedTask);

        public void EnqueueFailure(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            EnqueueBehavior((_, _) => Task.FromException(exception));
        }

        public void EnqueueBehavior(Func<OutboxDispatchMessage, CancellationToken, Task> behavior)
        {
            ArgumentNullException.ThrowIfNull(behavior);

            _behaviors.Enqueue(behavior);
        }

        public async Task DispatchAsync(
            OutboxDispatchMessage message,
            CancellationToken cancellationToken
        )
        {
            Messages.Add(message);

            if (_behaviors.Count == 0)
            {
                return;
            }

            Func<OutboxDispatchMessage, CancellationToken, Task> behavior = _behaviors.Dequeue();
            await behavior(message, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class BlankMessageException : Exception
    {
        public override string Message => " \t ";
    }
}
