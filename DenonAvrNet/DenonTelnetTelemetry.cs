using DenonAvrNet.Models;

namespace DenonAvrNet;

/// <summary>Parses optional, unsolicited Denon Telnet telemetry messages.</summary>
public static class DenonTelnetTelemetry
{
    private const string ActiveSpeakerPrefix = "OPINFASP ";

    /// <summary>
    /// Tries to decode an <c>OPINFASP</c> message. The payload is a matrix of
    /// output positions: <c>2</c> means active and <c>0</c> means inactive.
    /// Position numbers are one-based. Use <see cref="DenonAudioInfo.ActiveSpeakers"/>
    /// from the HTTP status snapshot for the documented channel names.
    /// </summary>
    public static bool TryParseActiveSpeakerMatrix(
        string? message,
        out DenonActiveSpeakerMatrix? matrix)
    {
        matrix = null;
        if (string.IsNullOrWhiteSpace(message) ||
            !message.StartsWith(ActiveSpeakerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var values = message[ActiveSpeakerPrefix.Length..].Trim();
        if (values.Length == 0 || values.Any(value => value is not ('0' or '1' or '2')))
        {
            return false;
        }

        var activePositions = values
            .Select((value, index) => (value, position: index + 1))
            .Where(item => item.value == '2')
            .Select(item => item.position)
            .ToArray();

        matrix = new DenonActiveSpeakerMatrix(values, activePositions);
        return true;
    }
}
