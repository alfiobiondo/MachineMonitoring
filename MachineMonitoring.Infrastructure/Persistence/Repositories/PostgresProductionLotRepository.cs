using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresProductionLotRepository : IProductionLotRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresProductionLotRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<ProductionLot?> GetByIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        ProductionLotRecord? record = await _dbContext
            .ProductionLots.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == productionLotId, cancellationToken);

        return record is null ? null : Restore(record);
    }

    public async Task AddAsync(ProductionLot productionLot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productionLot);

        _dbContext.ProductionLots.Add(CreateRecord(productionLot));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProductionLot productionLot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(productionLot);

        ProductionLotRecord? record = await _dbContext.ProductionLots.SingleOrDefaultAsync(
            item => item.Id == productionLot.Id,
            cancellationToken
        );

        if (record is null)
        {
            throw new InvalidOperationException($"Production lot {productionLot.Id} does not exist.");
        }

        record.Status = productionLot.Status;
        record.StartedAt = productionLot.StartedAt;
        record.CompletedAt = productionLot.CompletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ProductionLotRecord CreateRecord(ProductionLot productionLot)
    {
        return new ProductionLotRecord
        {
            Id = productionLot.Id,
            Code = productionLot.Code,
            PlannedQuantity = productionLot.PlannedQuantity,
            Status = productionLot.Status,
            CreatedAt = productionLot.CreatedAt,
            StartedAt = productionLot.StartedAt,
            CompletedAt = productionLot.CompletedAt,
        };
    }

    private static ProductionLot Restore(ProductionLotRecord record)
    {
        return ProductionLot.Restore(
            id: record.Id,
            code: record.Code,
            plannedQuantity: record.PlannedQuantity,
            status: record.Status,
            createdAt: record.CreatedAt,
            startedAt: record.StartedAt,
            completedAt: record.CompletedAt
        );
    }
}
