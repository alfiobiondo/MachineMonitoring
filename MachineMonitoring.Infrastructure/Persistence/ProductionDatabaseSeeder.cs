using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Persistence.Models;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class ProductionDatabaseSeeder
{
    private readonly MachineMonitoringDbContext _dbContext;

    public ProductionDatabaseSeeder(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await _dbContext.Materials.AnyAsync(cancellationToken))
        {
            _dbContext.Materials.AddRange(InMemoryProductionData.CreateMaterials());
        }

        if (!await _dbContext.Nozzles.AnyAsync(cancellationToken))
        {
            _dbContext.Nozzles.AddRange(InMemoryProductionData.CreateNozzles());
        }

        if (!await _dbContext.DrawingFiles.AnyAsync(cancellationToken))
        {
            _dbContext.DrawingFiles.AddRange(InMemoryProductionData.CreateDrawingFiles());
        }

        if (!await _dbContext.MachineCapabilities.AnyAsync(cancellationToken))
        {
            IEnumerable<MachineCapabilitiesRecord> records = InMemoryProductionData
                .CreateMachineCapabilities()
                .Select(CreateMachineCapabilitiesRecord);

            _dbContext.MachineCapabilities.AddRange(records);
        }

        if (!await _dbContext.MachineRuntimeStates.AnyAsync(cancellationToken))
        {
            IEnumerable<MachineRuntimeStateRecord> records = InMemoryProductionData
                .CreateMachineCapabilities()
                .Select(capabilities => new MachineRuntimeStateRecord
                {
                    MachineId = capabilities.MachineId,
                    Status = Domain.Production.MachineRuntimeStatus.Available,
                    CurrentOperationId = null,
                    LastChangedAt = DateTimeOffset.UtcNow,
                    FailureReason = null,
                    ActiveAlarmId = null,
                    Version = 1,
                });

            _dbContext.MachineRuntimeStates.AddRange(records);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MachineCapabilitiesRecord CreateMachineCapabilitiesRecord(
        MachineCapabilities capabilities
    )
    {
        MachineCapabilitiesRecord record = new()
        {
            MachineId = capabilities.MachineId,
            MaximumLaserPowerWatts = capabilities.MaximumLaserPowerWatts,
            MinimumThicknessMillimeters = capabilities.MinimumThicknessMillimeters,
            MaximumThicknessMillimeters = capabilities.MaximumThicknessMillimeters,
            MaximumTubeDiameterMillimeters = capabilities.MaximumTubeDiameterMillimeters,
            MaximumTubeLengthMillimeters = capabilities.MaximumTubeLengthMillimeters,
            MaximumSheetWidthMillimeters = capabilities.MaximumSheetWidthMillimeters,
            MaximumSheetHeightMillimeters = capabilities.MaximumSheetHeightMillimeters,
        };

        foreach (MaterialCategory category in capabilities.SupportedMaterialCategories)
        {
            record.SupportedMaterialCategories.Add(
                new MachineCapabilityMaterialCategoryRecord
                {
                    MachineId = capabilities.MachineId,
                    MaterialCategory = category,
                }
            );
        }

        foreach (Guid nozzleId in capabilities.SupportedNozzleIds)
        {
            record.SupportedNozzles.Add(
                new MachineCapabilityNozzleRecord
                {
                    MachineId = capabilities.MachineId,
                    NozzleId = nozzleId,
                }
            );
        }

        foreach (WorkpieceGeometryType geometryType in capabilities.SupportedGeometryTypes)
        {
            record.SupportedGeometryTypes.Add(
                new MachineCapabilityGeometryTypeRecord
                {
                    MachineId = capabilities.MachineId,
                    GeometryType = geometryType,
                }
            );
        }

        return record;
    }
}
