using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresMachineOperationEventRepository : IMachineOperationEventRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresMachineOperationEventRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task AddAsync(
        MachineOperationEvent machineOperationEvent,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machineOperationEvent);

        _dbContext.MachineOperationEvents.Add(CreateRecord(machineOperationEvent));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MachineOperationEvent>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        List<MachineOperationEventRecord> records = await _dbContext
            .MachineOperationEvents.AsNoTracking()
            .Where(item => item.MachineOperationId == operationId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    public async Task<IReadOnlyCollection<MachineOperationEvent>> GetByWorkpieceIdAsync(
        Guid workpieceId,
        CancellationToken cancellationToken
    )
    {
        List<MachineOperationEventRecord> records = await _dbContext
            .MachineOperationEvents.AsNoTracking()
            .Where(item => item.MachineOperation.WorkpieceId == workpieceId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    public async Task<IReadOnlyCollection<MachineOperationEvent>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        List<MachineOperationEventRecord> records = await _dbContext
            .MachineOperationEvents.AsNoTracking()
            .Where(item => item.MachineOperation.Workpiece.ProductionLotId == productionLotId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    private static MachineOperationEventRecord CreateRecord(MachineOperationEvent item)
    {
        return new MachineOperationEventRecord
        {
            Id = item.Id,
            MachineOperationId = item.MachineOperationId,
            EventType = item.EventType,
            OccurredAt = item.OccurredAt,
            PreviousStatus = item.PreviousStatus,
            NewStatus = item.NewStatus,
            ProgressPercentage = item.ProgressPercentage,
            Phase = item.Phase,
            Reason = item.Reason,
            MachineAlarmId = item.MachineAlarmId,
            Metadata = item.Metadata,
        };
    }

    private static MachineOperationEvent Restore(MachineOperationEventRecord item)
    {
        return new MachineOperationEvent(
            id: item.Id,
            machineOperationId: item.MachineOperationId,
            eventType: item.EventType,
            occurredAt: item.OccurredAt,
            previousStatus: item.PreviousStatus,
            newStatus: item.NewStatus,
            progressPercentage: item.ProgressPercentage,
            phase: item.Phase,
            reason: item.Reason,
            machineAlarmId: item.MachineAlarmId,
            metadata: item.Metadata
        );
    }
}
