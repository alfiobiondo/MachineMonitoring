using System.Text.Json;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Domain;
using MachineMonitoring.Infrastructure;
using MachineMonitoring.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Tests.Infrastructure;

public class JsonMachineProviderTests
{
    [Fact]
    public async Task GetMachinesAsync_WithValidJson_ShouldReturnMachines()
    {
        // Arrange
        string json = """
            [
              {
                "id": "M-001",
                "name": "Laser Cutter",
                "status": "Running",
                "location": "Production Hall A",
                "serialNumber": "SN-001"
              },
              {
                "id": "M-002",
                "name": "Tube Bender",
                "status": "Maintenance",
                "location": "Production Hall B",
                "serialNumber": "SN-002"
              }
            ]
            """;

        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        IReadOnlyCollection<Machine> machines = await provider.GetMachinesAsync(
            CancellationToken.None
        );

        // Assert
        Machine[] result = machines.ToArray();

        Assert.Equal(2, result.Length);

        Assert.Equal("M-001", result[0].Id);
        Assert.Equal("Laser Cutter", result[0].Name);
        Assert.Equal(MachineStatus.Running, result[0].Status);
        Assert.Equal("Production Hall A", result[0].Location);
        Assert.Equal("SN-001", result[0].SerialNumber);

        Assert.Equal("M-002", result[1].Id);
        Assert.Equal("Tube Bender", result[1].Name);
        Assert.Equal(MachineStatus.Maintenance, result[1].Status);
        Assert.Equal("Production Hall B", result[1].Location);
        Assert.Equal("SN-002", result[1].SerialNumber);
    }

    [Fact]
    public async Task GetMachinesAsync_WithInvalidJson_ShouldThrowMachineUnavailableException()
    {
        // Arrange
        string invalidJson = """
            [
              {
                "id": "M-001",
                "name": "Laser Cutter"
              }
            """;

        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(invalidJson);

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

        // Assert
        MachineUnavailableException exception =
            await Assert.ThrowsAsync<MachineUnavailableException>(action);

        Assert.Contains("contains invalid JSON", exception.Message);

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetMachinesAsync_WithEmptyArray_ShouldThrowMachineUnavailableException()
    {
        // Arrange
        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync("[]");

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

        // Assert
        MachineUnavailableException exception =
            await Assert.ThrowsAsync<MachineUnavailableException>(action);

        Assert.Equal(
            "The machine data file contains an empty machine collection.",
            exception.Message
        );

        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task GetMachinesAsync_WithNullCollection_ShouldThrowMachineUnavailableException()
    {
        // Arrange
        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync("null");

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

        // Assert
        MachineUnavailableException exception =
            await Assert.ThrowsAsync<MachineUnavailableException>(action);

        Assert.Equal("The machine data file contains no machine collection.", exception.Message);

        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task GetMachinesAsync_WithUnknownStatus_ShouldThrowMachineUnavailableException()
    {
        // Arrange
        string json = """
            [
              {
                "id": "M-001",
                "name": "Laser Cutter",
                "status": "Exploding",
                "location": "Production Hall A",
                "serialNumber": "SN-001"
              }
            ]
            """;

        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

        // Assert
        MachineUnavailableException exception =
            await Assert.ThrowsAsync<MachineUnavailableException>(action);

        Assert.Equal("Machine M-001 has invalid status 'Exploding'.", exception.Message);

        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task GetMachinesAsync_WithUppercasePropertyNames_ShouldDeserializeMachine()
    {
        // Arrange
        string json = """
            [
              {
                "ID": "M-001",
                "NAME": "Laser Cutter",
                "STATUS": "running",
                "LOCATION": "Production Hall A",
                "SERIALNUMBER": "SN-001"
              }
            ]
            """;

        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        IReadOnlyCollection<Machine> machines = await provider.GetMachinesAsync(
            CancellationToken.None
        );

        // Assert
        Machine machine = Assert.Single(machines);

        Assert.Equal("M-001", machine.Id);
        Assert.Equal("Laser Cutter", machine.Name);
        Assert.Equal(MachineStatus.Running, machine.Status);
        Assert.Equal("Production Hall A", machine.Location);
        Assert.Equal("SN-001", machine.SerialNumber);
    }

    [Fact]
    public async Task GetMachinesAsync_WithMissingFile_ShouldThrowMachineUnavailableException()
    {
        // Arrange
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        JsonMachineProvider provider = CreateProvider(filePath);

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

        // Assert
        MachineUnavailableException exception =
            await Assert.ThrowsAsync<MachineUnavailableException>(action);

        Assert.Contains("was not found", exception.Message);

        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    // [Fact]
    // public async Task GetMachinesAsync_WithoutSerialNumber_ShouldThrowMachineUnavailableException()
    // {
    //     // Arrange
    //     string json = """
    //         [
    //           {
    //             "id": "M-001",
    //             "name": "Laser Cutter",
    //             "status": "Running",
    //             "location": "Production Hall A"
    //           }
    //         ]
    //         """;

    //     await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

    //     JsonMachineProvider provider = CreateProvider(file.FilePath);

    //     // Act
    //     Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

    //     // Assert
    //     MachineUnavailableException exception =
    //         await Assert.ThrowsAsync<MachineUnavailableException>(action);

    //     Assert.Equal("The serial number of machine M-001 is missing.", exception.Message);

    //     Assert.Null(exception.InnerException);
    // }

    [Fact]
    public async Task GetMachinesAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        string json = """
            [
              {
                "id": "M-001",
                "name": "Laser Cutter",
                "status": "Running",
                "location": "Production Hall A",
                "serialNumber": "SN-001"
              }
            ]
            """;

        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        using CancellationTokenSource source = new();

        source.Cancel();

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(source.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }

    [Theory]
    [MemberData(nameof(MissingFieldData))]
    public async Task GetMachinesAsync_WithMissingRequiredField_ShouldThrowExpectedException(
        string json,
        string expectedMessage
    )
    {
        // Arrange
        await using TemporaryJsonFile file = await TemporaryJsonFile.CreateAsync(json);

        JsonMachineProvider provider = CreateProvider(file.FilePath);

        // Act
        Func<Task> action = () => provider.GetMachinesAsync(CancellationToken.None);

        // Assert
        MachineUnavailableException exception =
            await Assert.ThrowsAsync<MachineUnavailableException>(action);

        Assert.Equal(expectedMessage, exception.Message);
    }

    private static JsonMachineProvider CreateProvider(string filePath)
    {
        MachineDataOptions machineDataOptions = new() { FilePath = filePath };

        IOptions<MachineDataOptions> options = Options.Create(machineDataOptions);

        return new JsonMachineProvider(options, NullLogger<JsonMachineProvider>.Instance);
    }

    public static TheoryData<string, string> MissingFieldData =>
        new()
        {
            {
                """
                    [
                      {
                        "name": "Laser Cutter",
                        "status": "Running",
                        "location": "Production Hall A",
                        "serialNumber": "SN-001"
                      }
                    ]
                    """,
                "The JSON machine ID is missing."
            },
            {
                """
                    [
                      {
                        "id": "M-001",
                        "status": "Running",
                        "location": "Production Hall A",
                        "serialNumber": "SN-001"
                      }
                    ]
                    """,
                "The name of machine M-001 is missing."
            },
            {
                """
                    [
                      {
                        "id": "M-001",
                        "name": "Laser Cutter",
                        "status": "Running",
                        "serialNumber": "SN-001"
                      }
                    ]
                    """,
                "The location of machine M-001 is missing."
            },
            {
                """
                    [
                      {
                        "id": "M-001",
                        "name": "Laser Cutter",
                        "status": "Running",
                        "location": "Production Hall A"
                      }
                    ]
                    """,
                "The serial number of machine M-001 is missing."
            },
        };
}
