namespace DenonAvrNet.Models;

public sealed record DenonDeviceInfo(
    string ModelName,
    string? ManualModelName,
    string? CommunicationApiVersion,
    int? ZoneCount,
    string? MacAddress,
    string? CategoryName);
