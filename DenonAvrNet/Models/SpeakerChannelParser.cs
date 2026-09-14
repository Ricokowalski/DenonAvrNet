namespace DenonAvrNet.Models;

/// <summary>Converts Denon's active-speaker abbreviations into <see cref="SpeakerChannel"/> flags.</summary>
public static class SpeakerChannelParser
{
    private static readonly IReadOnlyDictionary<string, SpeakerChannel> Map =
        new Dictionary<string, SpeakerChannel>(StringComparer.OrdinalIgnoreCase)
        {
            ["FL"] = SpeakerChannel.FrontLeft, ["FR"] = SpeakerChannel.FrontRight,
            ["C"] = SpeakerChannel.Center, ["SW"] = SpeakerChannel.Subwoofer,
            ["SW1"] = SpeakerChannel.Subwoofer, ["SW2"] = SpeakerChannel.Subwoofer,
            ["SW3"] = SpeakerChannel.Subwoofer, ["SW4"] = SpeakerChannel.Subwoofer,
            ["SL"] = SpeakerChannel.SurroundLeft, ["SR"] = SpeakerChannel.SurroundRight,
            ["SB"] = SpeakerChannel.SurroundBack,
            ["SBL"] = SpeakerChannel.SurroundBackLeft, ["SBR"] = SpeakerChannel.SurroundBackRight,
            ["FWL"] = SpeakerChannel.FrontWideLeft, ["FWR"] = SpeakerChannel.FrontWideRight,
            ["FHL"] = SpeakerChannel.FrontHeightLeft, ["FHR"] = SpeakerChannel.FrontHeightRight,
            ["TFL"] = SpeakerChannel.TopFrontLeft, ["TFR"] = SpeakerChannel.TopFrontRight,
            ["TML"] = SpeakerChannel.TopMiddleLeft, ["TMR"] = SpeakerChannel.TopMiddleRight,
            ["TRL"] = SpeakerChannel.TopRearLeft, ["TRR"] = SpeakerChannel.TopRearRight,
            ["RHL"] = SpeakerChannel.RearHeightLeft, ["RHR"] = SpeakerChannel.RearHeightRight,
            ["SHL"] = SpeakerChannel.SurroundHeightLeft, ["SHR"] = SpeakerChannel.SurroundHeightRight,
            ["CH"] = SpeakerChannel.CenterHeight, ["TS"] = SpeakerChannel.TopSurround,
            ["FDL"] = SpeakerChannel.FrontDolbyLeft, ["FDR"] = SpeakerChannel.FrontDolbyRight,
            ["SDL"] = SpeakerChannel.SurroundDolbyLeft, ["SDR"] = SpeakerChannel.SurroundDolbyRight,
            ["BDL"] = SpeakerChannel.BackDolbyLeft, ["BDR"] = SpeakerChannel.BackDolbyRight
        };

    /// <summary>Combines all known channel codes into a flag value. Unknown codes are ignored.</summary>
    public static SpeakerChannel Parse(IEnumerable<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);

        var result = SpeakerChannel.None;
        foreach (var code in codes)
        {
            if (!string.IsNullOrWhiteSpace(code) && Map.TryGetValue(code, out var channel))
            {
                result |= channel;
            }
        }

        return result;
    }
}
