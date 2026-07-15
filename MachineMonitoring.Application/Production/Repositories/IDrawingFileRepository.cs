using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IDrawingFileRepository
{
    Task<DrawingFile?> GetByIdAsync(Guid drawingFileId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DrawingFile>> GetAllAsync(CancellationToken cancellationToken);
}
