namespace DenonAvrNet.Models;

/// <summary>Maps modern Denon web-interface speaker indices to typed channels.</summary>
public static class DenonSpeakerPresetIndexConverter
{
    private static readonly IReadOnlyDictionary<int, SpeakerChannel> Map =
        new Dictionary<int, SpeakerChannel>
        {
            [0] = SpeakerChannel.FrontLeft, [1] = SpeakerChannel.Center, [2] = SpeakerChannel.FrontRight,
            [3] = SpeakerChannel.FrontWideRight, [4] = SpeakerChannel.SurroundRight,
            [5] = SpeakerChannel.SurroundBackRight, [6] = SpeakerChannel.SurroundBack,
            [7] = SpeakerChannel.SurroundBackLeft, [8] = SpeakerChannel.SurroundLeft,
            [9] = SpeakerChannel.FrontWideLeft, [10] = SpeakerChannel.FrontHeightRight,
            [11] = SpeakerChannel.FrontDolbyRight, [12] = SpeakerChannel.TopFrontRight,
            [13] = SpeakerChannel.TopMiddleRight, [14] = SpeakerChannel.SurroundDolbyRight,
            [15] = SpeakerChannel.TopRearRight, [16] = SpeakerChannel.SurroundHeightRight,
            [17] = SpeakerChannel.RearHeightRight, [18] = SpeakerChannel.BackDolbyRight,
            [19] = SpeakerChannel.BackDolbyLeft, [20] = SpeakerChannel.RearHeightLeft,
            [21] = SpeakerChannel.SurroundHeightLeft, [22] = SpeakerChannel.TopRearLeft,
            [23] = SpeakerChannel.SurroundDolbyLeft, [24] = SpeakerChannel.TopMiddleLeft,
            [25] = SpeakerChannel.TopFrontLeft, [26] = SpeakerChannel.FrontDolbyLeft,
            [27] = SpeakerChannel.FrontHeightLeft, [28] = SpeakerChannel.CenterHeight,
            [29] = SpeakerChannel.TopSurround, [30] = SpeakerChannel.Subwoofer,
            [31] = SpeakerChannel.Subwoofer2,
            [32] = SpeakerChannel.Subwoofer | SpeakerChannel.Subwoofer2,
            [33] = SpeakerChannel.Subwoofer3, [34] = SpeakerChannel.Subwoofer4,
            [35] = SpeakerChannel.Subwoofer | SpeakerChannel.Subwoofer2 |
                   SpeakerChannel.Subwoofer3 | SpeakerChannel.Subwoofer4
        };

    /// <summary>Tries to resolve one web-interface index to its channel flag or flag combination.</summary>
    public static bool TryToSpeakerChannel(int speakerIndex, out SpeakerChannel channel) =>
        Map.TryGetValue(speakerIndex, out channel);
}
