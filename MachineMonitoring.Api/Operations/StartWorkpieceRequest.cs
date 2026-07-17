namespace MachineMonitoring.Api.Operations;

public sealed record StartWorkpieceRequest(string InitialPhase, int? StartFromSequenceNumber);
