using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresMachineOperationRepository : IMachineOperationRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresMachineOperationRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<MachineOperation?> GetByIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        MachineOperationRecord? record = await _dbContext
            .MachineOperations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == operationId, cancellationToken);

        return record is null ? null : RestoreOperation(record);
    }

    public async Task<IReadOnlyCollection<MachineOperation>> GetAllAsync(
        string? machineId,
        MachineOperationStatus? status,
        CancellationToken cancellationToken
    )
    {
        IQueryable<MachineOperationRecord> query = _dbContext.MachineOperations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(machineId))
        {
            query = query.Where(operation => operation.MachineId == machineId);
        }

        if (status is not null)
        {
            query = query.Where(operation => operation.Status == status.Value);
        }

        List<MachineOperationRecord> records = await query
            .OrderByDescending(operation => operation.CreatedAt)
            .ToListAsync(cancellationToken);

        return records.Select(RestoreOperation).ToArray();
    }

    public async Task<LaserCutConfiguration?> GetConfigurationByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        LaserCutConfigurationRecord? record = await _dbContext
            .LaserCutConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(
                configuration => configuration.OperationId == operationId,
                cancellationToken
            );

        return record is null ? null : RestoreConfiguration(record);
    }

    public async Task AddAsync(
        MachineOperation operation,
        LaserCutConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.OperationId != operation.Id)
        {
            throw new ArgumentException(
                "The configuration does not belong to the supplied operation.",
                nameof(configuration)
            );
        }

        MachineOperationRecord operationRecord = CreateOperationRecord(operation);

        LaserCutConfigurationRecord configurationRecord = CreateConfigurationRecord(configuration);

        operationRecord.LaserCutConfiguration = configurationRecord;

        _dbContext.MachineOperations.Add(operationRecord);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MachineOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        MachineOperationRecord? record = await _dbContext.MachineOperations.SingleOrDefaultAsync(
            item => item.Id == operation.Id,
            cancellationToken
        );

        if (record is null)
        {
            throw new InvalidOperationException($"Operation {operation.Id} does not exist.");
        }

        record.Status = operation.Status;
        record.ProgressPercentage = operation.ProgressPercentage;
        record.CurrentPhase = operation.CurrentPhase;
        record.FailureReason = operation.FailureReason;
        record.StartedAt = operation.StartedAt;
        record.CompletedAt = operation.CompletedAt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MachineOperationRecord CreateOperationRecord(MachineOperation operation)
    {
        return new MachineOperationRecord
        {
            Id = operation.Id,
            WorkpieceId = operation.WorkpieceId,
            MachineId = operation.MachineId,
            Type = operation.Type,
            Status = operation.Status,
            ProgressPercentage = operation.ProgressPercentage,
            CurrentPhase = operation.CurrentPhase,
            FailureReason = operation.FailureReason,
            CreatedAt = operation.CreatedAt,
            StartedAt = operation.StartedAt,
            CompletedAt = operation.CompletedAt,
        };
    }

    private static LaserCutConfigurationRecord CreateConfigurationRecord(
        LaserCutConfiguration configuration
    )
    {
        LaserCutConfigurationRecord record = new()
        {
            Id = configuration.Id,
            OperationId = configuration.OperationId,
            MaterialId = configuration.MaterialId,
            NozzleId = configuration.NozzleId,
            DrawingFileId = configuration.DrawingFileId,
            GeometryType = configuration.GeometryType,
            ThicknessMillimeters = configuration.Geometry.ThicknessMillimeters,
            LaserPowerWatts = configuration.LaserPowerWatts,
            CuttingSpeedMillimetersPerMinute = configuration.CuttingSpeedMillimetersPerMinute,
            AssistGas = configuration.AssistGas,
            GasPressureBar = configuration.GasPressureBar,
            FocalOffsetMillimeters = configuration.FocalOffsetMillimeters,
            NumberOfPasses = configuration.NumberOfPasses,
            CreatedAt = configuration.CreatedAt,
        };

        switch (configuration.Geometry)
        {
            case TubeGeometry tube:
                record.TubeOuterDiameterMillimeters = tube.OuterDiameterMillimeters;
                record.TubeLengthMillimeters = tube.LengthMillimeters;
                break;

            case SheetGeometry sheet:
                record.SheetWidthMillimeters = sheet.WidthMillimeters;
                record.SheetHeightMillimeters = sheet.HeightMillimeters;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported geometry type " + $"{configuration.Geometry.GetType().Name}."
                );
        }

        return record;
    }

    private static MachineOperation RestoreOperation(MachineOperationRecord record)
    {
        return MachineOperation.Restore(
            id: record.Id,
            workpieceId: record.WorkpieceId,
            machineId: record.MachineId,
            type: record.Type,
            status: record.Status,
            progressPercentage: record.ProgressPercentage,
            currentPhase: record.CurrentPhase,
            failureReason: record.FailureReason,
            createdAt: record.CreatedAt,
            startedAt: record.StartedAt,
            completedAt: record.CompletedAt
        );
    }

    private static LaserCutConfiguration RestoreConfiguration(LaserCutConfigurationRecord record)
    {
        IWorkpieceGeometry geometry = record.GeometryType switch
        {
            WorkpieceGeometryType.Tube => new TubeGeometry(
                outerDiameterMillimeters: record.TubeOuterDiameterMillimeters
                    ?? throw new InvalidOperationException("Tube outer diameter is missing."),
                thicknessMillimeters: record.ThicknessMillimeters,
                lengthMillimeters: record.TubeLengthMillimeters
                    ?? throw new InvalidOperationException("Tube length is missing.")
            ),

            WorkpieceGeometryType.Sheet => new SheetGeometry(
                widthMillimeters: record.SheetWidthMillimeters
                    ?? throw new InvalidOperationException("Sheet width is missing."),
                heightMillimeters: record.SheetHeightMillimeters
                    ?? throw new InvalidOperationException("Sheet height is missing."),
                thicknessMillimeters: record.ThicknessMillimeters
            ),

            _ => throw new InvalidOperationException(
                $"Unsupported geometry type " + $"{record.GeometryType}."
            ),
        };

        return new LaserCutConfiguration(
            id: record.Id,
            operationId: record.OperationId,
            materialId: record.MaterialId,
            nozzleId: record.NozzleId,
            drawingFileId: record.DrawingFileId,
            geometry: geometry,
            laserPowerWatts: record.LaserPowerWatts,
            cuttingSpeedMillimetersPerMinute: record.CuttingSpeedMillimetersPerMinute,
            assistGas: record.AssistGas,
            gasPressureBar: record.GasPressureBar,
            focalOffsetMillimeters: record.FocalOffsetMillimeters,
            numberOfPasses: record.NumberOfPasses,
            createdAt: record.CreatedAt
        );
    }
}
