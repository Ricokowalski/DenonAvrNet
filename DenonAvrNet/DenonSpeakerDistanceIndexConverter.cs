namespace DenonAvrNet.Models;

/// <summary>
/// Maps speaker indices of the distance page (<c>get_config?type=4</c>) to typed channels.
/// These indices differ from the level-page indices in <see cref="DenonSpeakerPresetIndexConverter"/>.
/// Derived from the AVC-X6800H web interface; indices not listed here are unknown.
/// </summary>
public static class DenonSpeakerDistanceIndexConverter
{
    private static readonly IReadOnlyDictionary<int, SpeakerChannel> Map =
        new Dictionary<int, SpeakerChannel>
        {
            [0] = SpeakerChannel.FrontLeft,
            [1] = SpeakerChannel.FrontRight,
            [2] = SpeakerChannel.Center,
            [3] = SpeakerChannel.Subwoofer,
            [4] = SpeakerChannel.Subwoofer2,
            [5] = SpeakerChannel.Subwoofer3,
            [7] = SpeakerChannel.SurroundLeft,
            [8] = SpeakerChannel.SurroundRight,
            [11] = SpeakerChannel.SurroundBack,
            [14] = SpeakerChannel.FrontHeightLeft,
            [15] = SpeakerChannel.FrontHeightRight,
            [20] = SpeakerChannel.TopMiddleLeft,
            [21] = SpeakerChannel.TopMiddleRight,
            [28] = SpeakerChannel.RearHeightLeft,
            [29] = SpeakerChannel.RearHeightRight
        };

    /// <summary>Tries to resolve one distance-page index to its channel.</summary>
    public static bool TryToSpeakerChannel(int speakerIndex, out SpeakerChannel channel) =>
        Map.TryGetValue(speakerIndex, out channel);
}