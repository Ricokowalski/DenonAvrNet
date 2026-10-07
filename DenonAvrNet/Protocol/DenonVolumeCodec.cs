using System.Globalization;

namespace DenonAvrNet.Protocol;

/// <summary>
/// Converts between Denon's absolute 0..98 volume scale, the relative dB scale
/// and the transport-specific encodings. Mute is intentionally separate.
/// </summary>
internal static class DenonVolumeCodec
{
    internal const double MinVolume = 0.0;
    internal const double MaxVolume = 98.0;
    internal const double MinDecibels = -80.0;
    internal const double MaxDecibels = 18.0;

    internal static double NormalizeVolume(double volume)
    {
        if (volume is < MinVolume or > MaxVolume)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volume), volume,
                $"The Denon volume must be between {MinVolume:0.0} and {MaxVolume:0.0}.");
        }

        return Math.Round(volume * 2, MidpointRounding.ToEven) / 2.0;
    }

    internal static double NormalizeDecibels(double volumeDb)
    {
        if (volumeDb is < MinDecibels or > MaxDecibels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeDb), volumeDb,
                string.Create(CultureInfo.InvariantCulture,
                    $"The volume must be between {MinDecibels:+0.0;-0.0} and {MaxDecibels:+0.0;-0.0} dB."));
        }

        return Math.Round(volumeDb * 2, MidpointRounding.ToEven) / 2.0;
    }

    internal static double ToDecibels(double volume) => NormalizeVolume(volume) - 80.0;

    internal static double FromDecibels(double volumeDb) => NormalizeDecibels(volumeDb) + 80.0;

    /// <summary>HTTP volume endpoints use the relative dB representation.</summary>
    internal static string ToHttpValue(double volume) =>
        ToDecibels(volume).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Telnet uses Denon's absolute scale directly: 44.5 => 445, 80 => 80.</summary>
    internal static string ToTelnetValue(double volume)
    {
        var normalized = NormalizeVolume(volume);
        var wholeValue = (int)Math.Floor(normalized);

        return normalized - wholeValue >= 0.5
            ? $"{wholeValue:00}5"
            : $"{wholeValue:00}";
    }

    internal static double? ParseTelnetValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();

        // Some protocol revisions use 99 as a minimum/undefined sentinel.
        // It is not mute; mute has its own MU/Z2MU/Z3MU commands.
        if (trimmed.Equals("99", StringComparison.Ordinal))
        {
            return null;
        }

        if (trimmed.Length is not (2 or 3) || !trimmed.All(char.IsAsciiDigit))
        {
            throw new FormatException($"'{value}' is not a valid Denon volume value.");
        }

        var volume = trimmed.Length == 2
            ? int.Parse(trimmed, CultureInfo.InvariantCulture)
            : int.Parse(trimmed[..2], CultureInfo.InvariantCulture) +
              int.Parse(trimmed[2..], CultureInfo.InvariantCulture) / 10.0;

        if (volume is < MinVolume or > MaxVolume)
        {
            throw new FormatException($"'{value}' is outside the supported Denon volume range.");
        }

        return volume;
    }
}
