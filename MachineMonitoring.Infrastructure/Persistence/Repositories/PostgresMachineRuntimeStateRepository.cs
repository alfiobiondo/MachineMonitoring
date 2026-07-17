using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresMachineRuntimeStateRepository : IMachineRuntimeStateRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresMachineRuntimeStateRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<MachineRuntimeState?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        MachineRuntimeStateRecord? record = await _dbContext
            .MachineRuntimeStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.MachineId == machineId, cancellationToken);

        return record is null ? null : Restore(record);
    }

    public async Task<IReadOnlyCollection<MachineRuntimeState>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        List<MachineRuntimeStateRecord> records = await _dbContext
            .MachineRuntimeStates.AsNoTracking()
            .OrderBy(item => item.MachineId)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    public async Task AddAsync(MachineRuntimeState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        _dbContext.MachineRuntimeStates.Add(CreateRecord(state));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MachineRuntimeState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        MachineRuntimeStateRecord? record = await _dbContext.MachineRuntimeStates.SingleOrDefaultAsync(
            item => item.MachineId == state.MachineId,
            cancellationToken
        );

        if (record is null)
        {
            throw new InvalidOperationException(
                $"Machine runtime state for {state.MachineId} does not exist."
            );
        }

        record.Status = state.Status;
        record.CurrentOperationId = state.CurrentOperationId;
        record.LastChangedAt = state.LastChangedAt;
        record.FailureReason = state.FailureReason;
        record.ActiveAlarmId = state.ActiveAlarmId;
        record.Version = state.Version;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MachineRuntimeStateRecord CreateRecord(MachineRuntimeState state)
    {
        return new MachineRuntimeStateRecord
        {
            MachineId = state.MachineId,
            Status = state.Status,
            CurrentOperationId = state.CurrentOperationId,
            LastChangedAt = state.LastChangedAt,
            FailureReason = state.FailureReason,
            ActiveAlarmId = state.ActiveAlarmId,
            Version = state.Version,
        };
    }

    private static MachineRuntimeState Restore(MachineRuntimeStateRecord record)
    {
        return MachineRuntimeState.Restore(
            machineId: record.MachineId,
            status: record.Status,
            currentOperationId: record.CurrentOperationId,
            lastChangedAt: record.LastChangedAt,
            failureReason: record.FailureReason,
            activeAlarmId: record.ActiveAlarmId,
            version: record.Version
        );
    }
}
