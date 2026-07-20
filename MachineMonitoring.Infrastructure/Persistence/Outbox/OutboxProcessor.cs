using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed class OutboxProcessor
{
    private const int MaxLastErrorLength = 1000;
    private readonly MachineMonitoringDbContext _dbContext;
    private readonly IOutboxMessageDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    public OutboxProcessor(
        MachineMonitoringDbContext dbContext,
        IOutboxMessageDispatcher dispatcher,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
    }

    public async Task<OutboxProcessingResult> ProcessPendingAsync(
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero."
            );
        }

        List<OutboxMessageRecord> pendingRecords = await _dbContext.OutboxMessages
            .Where(item => item.ProcessedAt == null)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pendingRecords.Count == 0)
        {
            return new OutboxProcessingResult(0, 0, 0);
        }

        int attemptedCount = 0;
        int succeededCount = 0;
        int failedCount = 0;

        foreach (OutboxMessageRecord record in pendingRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();

            attemptedCount++;

            OutboxDispatchMessage dispatchMessage = new(
                record.Id,
                record.Type,
                record.Payload,
                record.OccurredAt,
                record.CreatedAt
            );

            Exception? dispatchException = null;
            bool dispatchSucceeded = false;

            try
            {
                await _dispatcher.DispatchAsync(dispatchMessage, cancellationToken);
                dispatchSucceeded = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                dispatchException = exception;
            }

            if (dispatchSucceeded)
            {
                record.ProcessedAt = _timeProvider.GetUtcNow();
                record.LastError = null;

                await _dbContext.SaveChangesAsync(cancellationToken);
                succeededCount++;
                continue;
            }

            record.ProcessedAt = null;
            record.Attempts += 1;
            record.LastError = CreateSafeLastError(dispatchException!);

            await _dbContext.SaveChangesAsync(cancellationToken);
            failedCount++;
        }

        return new OutboxProcessingResult(attemptedCount, succeededCount, failedCount);
    }

    private static string CreateSafeLastError(Exception exception)
    {
        string message = string.IsNullOrWhiteSpace(exception.Message)
            ? $"Dispatch failed with {exception.GetType().Name}."
            : exception.Message;

        string normalizedMessage = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        return normalizedMessage.Length <= MaxLastErrorLength
            ? normalizedMessage
            : normalizedMessage[..MaxLastErrorLength];
    }
}
