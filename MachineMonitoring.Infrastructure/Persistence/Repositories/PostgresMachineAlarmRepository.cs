using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresMachineAlarmRepository : IMachineAlarmRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresMachineAlarmRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<MachineAlarm?> GetByIdAsync(Guid alarmId, CancellationToken cancellationToken)
    {
        MachineAlarmRecord? record = await _dbContext
            .MachineAlarms.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == alarmId, cancellationToken);

        return record is null ? null : Restore(record);
    }

    public async Task<IReadOnlyCollection<MachineAlarm>> GetByMachineIdAsync(
        string machineId,
        bool activeOnly,
        CancellationToken cancellationToken
    )
    {
        IQueryable<MachineAlarmRecord> query = _dbContext.MachineAlarms.AsNoTracking().Where(item =>
            item.MachineId == machineId
        );

        if (activeOnly)
        {
            query = query.Where(item => item.Status != MachineAlarmStatus.Resolved);
        }

        List<MachineAlarmRecord> records = await query
            .OrderByDescending(item => item.RaisedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    public async Task<IReadOnlyCollection<MachineAlarm>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        List<MachineAlarmRecord> records = await _dbContext
            .MachineAlarms.AsNoTracking()
            .Where(item => item.MachineOperationId == operationId)
            .OrderByDescending(item => item.RaisedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    public async Task AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        _dbContext.MachineAlarms.Add(CreateRecord(alarm));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MachineAlarm alarm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        MachineAlarmRecord? record = await _dbContext.MachineAlarms.SingleOrDefaultAsync(
            item => item.Id == alarm.Id,
            cancellationToken
        );

        if (record is null)
        {
            throw new InvalidOperationException($"Machine alarm {alarm.Id} does not exist.");
        }

        record.Status = alarm.Status;
        record.AcknowledgedAt = alarm.AcknowledgedAt;
        record.ResolvedAt = alarm.ResolvedAt;
        record.ResolutionNotes = alarm.ResolutionNotes;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MachineAlarmRecord CreateRecord(MachineAlarm alarm)
    {
        return new MachineAlarmRecord
        {
            Id = alarm.Id,
            MachineId = alarm.MachineId,
            MachineOperationId = alarm.MachineOperationId,
            Code = alarm.Code,
            Severity = alarm.Severity,
            Status = alarm.Status,
            Message = alarm.Message,
            RaisedAt = alarm.RaisedAt,
            AcknowledgedAt = alarm.AcknowledgedAt,
            ResolvedAt = alarm.ResolvedAt,
            ResolutionNotes = alarm.ResolutionNotes,
        };
    }

    private static MachineAlarm Restore(MachineAlarmRecord record)
    {
        return MachineAlarm.Restore(
            id: record.Id,
            machineId: record.MachineId,
            machineOperationId: record.MachineOperationId,
            code: record.Code,
            severity: record.Severity,
            status: record.Status,
            message: record.Message,
            raisedAt: record.RaisedAt,
            acknowledgedAt: record.AcknowledgedAt,
            resolvedAt: record.ResolvedAt,
            resolutionNotes: record.ResolutionNotes
        );
    }
}
