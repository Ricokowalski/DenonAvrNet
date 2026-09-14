namespace DenonAvrNet.Models;

/// <summary>Converts between strongly typed speaker-level channels and Denon CV codes.</summary>
public static class DenonSpeakerLevelChannelConverter
{
    private static readonly IReadOnlyDictionary<DenonSpeakerLevelChannel, string> ToCodeMap =
        new Dictionary<DenonSpeakerLevelChannel, string>
        {
            [DenonSpeakerLevelChannel.FrontLeft] = "FL", [DenonSpeakerLevelChannel.FrontRight] = "FR",
            [DenonSpeakerLevelChannel.Center] = "C", [DenonSpeakerLevelChannel.Subwoofer] = "SW",
            [DenonSpeakerLevelChannel.Subwoofer2] = "SW2", [DenonSpeakerLevelChannel.Subwoofer3] = "SW3",
            [DenonSpeakerLevelChannel.Subwoofer4] = "SW4", [DenonSpeakerLevelChannel.SurroundLeft] = "SL",
            [DenonSpeakerLevelChannel.SurroundRight] = "SR", [DenonSpeakerLevelChannel.SurroundBack] = "SB",
            [DenonSpeakerLevelChannel.SurroundBackLeft] = "SBL", [DenonSpeakerLevelChannel.SurroundBackRight] = "SBR",
            [DenonSpeakerLevelChannel.FrontWideLeft] = "FWL", [DenonSpeakerLevelChannel.FrontWideRight] = "FWR",
            [DenonSpeakerLevelChannel.FrontHeightLeft] = "FHL", [DenonSpeakerLevelChannel.FrontHeightRight] = "FHR",
            [DenonSpeakerLevelChannel.TopFrontLeft] = "TFL", [DenonSpeakerLevelChannel.TopFrontRight] = "TFR",
            [DenonSpeakerLevelChannel.TopMiddleLeft] = "TML", [DenonSpeakerLevelChannel.TopMiddleRight] = "TMR",
            [DenonSpeakerLevelChannel.TopRearLeft] = "TRL", [DenonSpeakerLevelChannel.TopRearRight] = "TRR",
            [DenonSpeakerLevelChannel.RearHeightLeft] = "RHL", [DenonSpeakerLevelChannel.RearHeightRight] = "RHR",
            [DenonSpeakerLevelChannel.SurroundHeightLeft] = "SHL", [DenonSpeakerLevelChannel.SurroundHeightRight] = "SHR",
            [DenonSpeakerLevelChannel.CenterHeight] = "CH", [DenonSpeakerLevelChannel.TopSurround] = "TS",
            [DenonSpeakerLevelChannel.FrontDolbyLeft] = "FDL", [DenonSpeakerLevelChannel.FrontDolbyRight] = "FDR",
            [DenonSpeakerLevelChannel.SurroundDolbyLeft] = "SDL", [DenonSpeakerLevelChannel.SurroundDolbyRight] = "SDR",
            [DenonSpeakerLevelChannel.BackDolbyLeft] = "BDL", [DenonSpeakerLevelChannel.BackDolbyRight] = "BDR"
        };

    private static readonly IReadOnlyDictionary<string, DenonSpeakerLevelChannel> FromCodeMap =
        ToCodeMap.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the Denon CV suffix for a channel.</summary>
    public static string ToProtocolCode(DenonSpeakerLevelChannel channel) => ToCodeMap[channel];

    /// <summary>Tries to parse a Denon CV suffix into one exact channel.</summary>
    public static bool TryFromProtocolCode(string code, out DenonSpeakerLevelChannel channel) =>
        FromCodeMap.TryGetValue(code, out channel);
}
