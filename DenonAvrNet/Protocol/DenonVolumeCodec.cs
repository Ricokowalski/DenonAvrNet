using System.Globalization;

namespace DenonAvrNet.Protocol;

/// <summary>
/// Converts between the library's public dB representation and Denon's transport-specific
/// volume encodings. Mute is intentionally not encoded as a volume value.
/// </summary>
internal static class DenonVolumeCodec
{
    internal const double MinDecibels = -80.0;
    internal const double MaxDecibels = 18.0;

    internal static double NormalizeDecibels(double volumeDb)
    {
        if (volumeDb is < MinDecibels or > MaxDecibels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeDb),
                volumeDb,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The volume must be between {MinDecibels:+0.0;-0.0} and {MaxDecibels:+0.0;-0.0} dB."));
        }

        return Math.Round(volumeDb * 2, MidpointRounding.ToEven) / 2.0;
    }

    internal static string ToHttpValue(double volumeDb) =>
        NormalizeDecibels(volumeDb).ToString("0.0", CultureInfo.InvariantCulture);

    internal static string ToTelnetValue(double volumeDb)
    {
        var normalized = NormalizeDecibels(volumeDb);
        var protocolValue = normalized + 80.0;
        var wholeValue = (int)Math.Floor(protocolValue);

        return protocolValue - wholeValue >= 0.5
            ? $"{wholeValue:00}5"
            : $"{wholeValue:00}";
    }

    internal static double? ParseTelnetValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();

        // Older Denon protocol revisions use 99 as a minimum/undefined sentinel.
        // It is not mute: mute is transported separately through MU/Z2MU/Z3MU.
        if (trimmed.Equals("99", StringComparison.Ordinal))
        {
            return null;
        }

        if (trimmed.Length is not (2 or 3) || !trimmed.All(char.IsAsciiDigit))
        {
            throw new FormatException($"'{value}' is not a valid Denon volume value.");
        }

        var protocolValue = trimmed.Length == 2
            ? int.Parse(trimmed, CultureInfo.InvariantCulture)
            : int.Parse(trimmed[..2], CultureInfo.InvariantCulture) +
              int.Parse(trimmed[2..], CultureInfo.InvariantCulture) / 10.0;

        var volumeDb = protocolValue - 80.0;
        if (volumeDb is < MinDecibels or > MaxDecibels)
        {
            throw new FormatException($"'{value}' is outside the supported Denon volume range.");
        }

        return volumeDb;
    }
}
