using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Console;

public sealed class MaterialTrackingDemoService
{
    private static readonly Guid StainlessSteel304MaterialId = Guid.Parse(
        "10000000-0000-0000-0000-000000000001"
    );

    private readonly MachineMonitoringDbContext _dbContext;

    private readonly ILogger<MaterialTrackingDemoService> _logger;

    public MaterialTrackingDemoService(
        MachineMonitoringDbContext dbContext,
        ILogger<MaterialTrackingDemoService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Material? untrackedMaterial = await _dbContext
            .Materials.AsNoTracking()
            .SingleOrDefaultAsync(
                material => material.Id == StainlessSteel304MaterialId,
                cancellationToken
            );

        if (untrackedMaterial is null)
        {
            throw new InvalidOperationException(
                $"Material {StainlessSteel304MaterialId} was not found."
            );
        }

        EntityEntry<Material> untrackedEntry = _dbContext.Entry(untrackedMaterial);

        _logger.LogInformation(
            "Material {MaterialCode} loaded with AsNoTracking. "
                + "EF state: {EntityState}. "
                + "IsEnabled: {IsEnabled}.",
            untrackedMaterial.Code,
            untrackedEntry.State,
            untrackedMaterial.IsEnabled
        );

        /*
         * Errore intenzionale:
         *
         * L'oggetto viene modificato in memoria, ma EF Core non lo sta
         * tracciando perché è stato caricato con AsNoTracking().
         *
         * SaveChangesAsync non dovrebbe quindi generare alcun UPDATE.
         */
        untrackedMaterial.Disable();

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Untracked material {MaterialCode} was disabled in memory. "
                + "After SaveChanges, EF state: {EntityState}. "
                + "In-memory IsEnabled: {IsEnabled}.",
            untrackedMaterial.Code,
            _dbContext.Entry(untrackedMaterial).State,
            untrackedMaterial.IsEnabled
        );

        /*
         * Nuova query, questa volta con tracking.
         *
         * EF Core rilegge il valore dal database. Poiché la modifica
         * precedente non è stata salvata, IsEnabled dovrebbe essere true.
         */
        Material? trackedMaterial = await _dbContext.Materials.SingleOrDefaultAsync(
            material => material.Id == StainlessSteel304MaterialId,
            cancellationToken
        );

        if (trackedMaterial is null)
        {
            throw new InvalidOperationException(
                $"Material {StainlessSteel304MaterialId} was not found."
            );
        }

        EntityEntry<Material> trackedEntry = _dbContext.Entry(trackedMaterial);

        _logger.LogInformation(
            "Material {MaterialCode} loaded with tracking. "
                + "EF state: {EntityState}. "
                + "IsEnabled: {IsEnabled}.",
            trackedMaterial.Code,
            trackedEntry.State,
            trackedMaterial.IsEnabled
        );
    }
}
