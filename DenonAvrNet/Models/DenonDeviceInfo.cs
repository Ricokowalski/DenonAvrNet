namespace DenonAvrNet.Models;

/// <summary>Describes receiver identity and capabilities reported by Deviceinfo.xml.</summary>
public sealed record DenonDeviceInfo(
    string ModelName,
    string? ManualModelName,
    string? CommunicationApiVersion,
    int? ZoneCount,
    string? MacAddress,
    string? CategoryName);
