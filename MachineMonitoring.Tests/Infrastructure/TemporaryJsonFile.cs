namespace MachineMonitoring.Tests.Infrastructure;

public sealed class TemporaryJsonFile : IAsyncDisposable
{
    public string FilePath { get; }

    private TemporaryJsonFile(string filePath)
    {
        FilePath = filePath;
    }

    public static async Task<TemporaryJsonFile> CreateAsync(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        string filePath = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(filePath, json);

            return new TemporaryJsonFile(filePath);
        }
        catch
        {
            File.Delete(filePath);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        File.Delete(FilePath);

        return ValueTask.CompletedTask;
    }
}
