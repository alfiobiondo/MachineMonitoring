using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresMachineCapabilitiesRepository : IMachineCapabilitiesRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresMachineCapabilitiesRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<MachineCapabilities?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        MachineCapabilitiesRecord? record = await _dbContext
            .MachineCapabilities.AsNoTracking()
            .Include(item => item.SupportedMaterialCategories)
            .Include(item => item.SupportedNozzles)
            .Include(item => item.SupportedGeometryTypes)
            .SingleOrDefaultAsync(item => item.MachineId == machineId, cancellationToken);

        if (record is null)
        {
            return null;
        }

        return new MachineCapabilities(
            machineId: record.MachineId,
            maximumLaserPowerWatts: record.MaximumLaserPowerWatts,
            minimumThicknessMillimeters: record.MinimumThicknessMillimeters,
            maximumThicknessMillimeters: record.MaximumThicknessMillimeters,
            supportedMaterialCategories: record.SupportedMaterialCategories.Select(item =>
                item.MaterialCategory
            ),
            supportedNozzleIds: record.SupportedNozzles.Select(item => item.NozzleId),
            supportedGeometryTypes: record.SupportedGeometryTypes.Select(item => item.GeometryType),
            maximumTubeDiameterMillimeters: record.MaximumTubeDiameterMillimeters,
            maximumTubeLengthMillimeters: record.MaximumTubeLengthMillimeters,
            maximumSheetWidthMillimeters: record.MaximumSheetWidthMillimeters,
            maximumSheetHeightMillimeters: record.MaximumSheetHeightMillimeters
        );
    }
}
