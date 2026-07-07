using System.Text.Json.Serialization;

namespace MachineMonitoring.Infrastructure.Json;

public class MachineJsonDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }
}
