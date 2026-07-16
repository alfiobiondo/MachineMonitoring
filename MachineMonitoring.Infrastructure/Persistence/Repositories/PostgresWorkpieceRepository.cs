using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresWorkpieceRepository : IWorkpieceRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresWorkpieceRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<Workpiece?> GetByIdAsync(Guid workpieceId, CancellationToken cancellationToken)
    {
        WorkpieceRecord? record = await _dbContext
            .Workpieces.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == workpieceId, cancellationToken);

        return record is null ? null : Restore(record);
    }

    public async Task<IReadOnlyCollection<Workpiece>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        List<WorkpieceRecord> records = await _dbContext
            .Workpieces.AsNoTracking()
            .Where(item => item.ProductionLotId == productionLotId)
            .OrderBy(item => item.Code)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return records.Select(Restore).ToArray();
    }

    public async Task AddAsync(Workpiece workpiece, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workpiece);

        _dbContext.Workpieces.Add(CreateRecord(workpiece));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Workpiece workpiece, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workpiece);

        WorkpieceRecord? record = await _dbContext.Workpieces.SingleOrDefaultAsync(
            item => item.Id == workpiece.Id,
            cancellationToken
        );

        if (record is null)
        {
            throw new InvalidOperationException($"Workpiece {workpiece.Id} does not exist.");
        }

        record.Status = workpiece.Status;
        record.IsSequenceActive = workpiece.IsSequenceActive;
        record.StartedAt = workpiece.StartedAt;
        record.CompletedAt = workpiece.CompletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static WorkpieceRecord CreateRecord(Workpiece workpiece)
    {
        return new WorkpieceRecord
        {
            Id = workpiece.Id,
            ProductionLotId = workpiece.ProductionLotId,
            Code = workpiece.Code,
            MaterialCode = workpiece.MaterialCode,
            Status = workpiece.Status,
            IsSequenceActive = workpiece.IsSequenceActive,
            CreatedAt = workpiece.CreatedAt,
            StartedAt = workpiece.StartedAt,
            CompletedAt = workpiece.CompletedAt,
        };
    }

    private static Workpiece Restore(WorkpieceRecord record)
    {
        return Workpiece.Restore(
            id: record.Id,
            productionLotId: record.ProductionLotId,
            code: record.Code,
            materialCode: record.MaterialCode,
            status: record.Status,
            isSequenceActive: record.IsSequenceActive,
            createdAt: record.CreatedAt,
            startedAt: record.StartedAt,
            completedAt: record.CompletedAt
        );
    }
}
