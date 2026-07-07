using System.Text.Json;
using MachineMonitoring.Application;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
using MachineMonitoring.Infrastructure.Configuration;
using MachineMonitoring.Infrastructure.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Infrastructure;

public class JsonMachineProvider : IMachineProvider
{
    private readonly MachineDataOptions _options;
    private readonly ILogger<JsonMachineProvider> _logger;

    public JsonMachineProvider(
        IOptions<MachineDataOptions> options,
        ILogger<JsonMachineProvider> logger
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Machine>> GetMachinesAsync(
        CancellationToken cancellationToken
    )
    {
        string fullPath = Path.GetFullPath(_options.FilePath, AppContext.BaseDirectory);

        _logger.LogDebug("Reading machine data from JSON file {MachineDataFilePath}.", fullPath);

        try
        {
            await using FileStream stream = File.OpenRead(fullPath);

            JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };

            List<MachineJsonDto>? dtos = await JsonSerializer.DeserializeAsync<
                List<MachineJsonDto>
            >(stream, serializerOptions, cancellationToken);

            if (dtos is null)
            {
                throw new MachineUnavailableException(
                    "The machine data file contains no machine collection."
                );
            }

            if (dtos.Count == 0)
            {
                throw new MachineUnavailableException(
                    "The machine data file contains an empty machine collection."
                );
            }

            List<Machine> machines = new();

            foreach (MachineJsonDto dto in dtos)
            {
                Machine machine = ConvertToDomain(dto);

                machines.Add(machine);
            }

            _logger.LogInformation(
                "{MachineCount} machines loaded from JSON file.",
                machines.Count
            );

            return machines;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            throw new MachineUnavailableException(
                $"The machine data file '{fullPath}' was not found.",
                exception
            );
        }
        catch (JsonException exception)
        {
            throw new MachineUnavailableException(
                $"The machine data file '{fullPath}' contains invalid JSON.",
                exception
            );
        }
        catch (IOException exception)
        {
            throw new MachineUnavailableException(
                $"The machine data file '{fullPath}' could not be read.",
                exception
            );
        }
    }

    private static Machine ConvertToDomain(MachineJsonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new MachineUnavailableException("The JSON machine ID is missing.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new MachineUnavailableException($"The name of machine {dto.Id} is missing.");
        }

        if (string.IsNullOrWhiteSpace(dto.Location))
        {
            throw new MachineUnavailableException($"The location of machine {dto.Id} is missing.");
        }

        bool statusParsed = Enum.TryParse(dto.Status, ignoreCase: true, out MachineStatus status);

        if (!statusParsed)
        {
            throw new MachineUnavailableException(
                $"Machine {dto.Id} has invalid status '{dto.Status}'."
            );
        }

        if (string.IsNullOrWhiteSpace(dto.SerialNumber))
        {
            throw new MachineUnavailableException(
                $"The serial number of machine {dto.Id} is missing."
            );
        }

        return new Machine(
            id: dto.Id,
            name: dto.Name,
            status: status,
            location: dto.Location,
            serialNumber: dto.SerialNumber
        );
    }
}
