using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PostgresDrawingFileRepository : IDrawingFileRepository
{
    private readonly MachineMonitoringDbContext _dbContext;

    public PostgresDrawingFileRepository(MachineMonitoringDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<DrawingFile?> GetByIdAsync(
        Guid drawingFileId,
        CancellationToken cancellationToken
    )
    {
        return await _dbContext
            .DrawingFiles.AsNoTracking()
            .SingleOrDefaultAsync(
                drawingFile => drawingFile.Id == drawingFileId,
                cancellationToken
            );
    }
}
