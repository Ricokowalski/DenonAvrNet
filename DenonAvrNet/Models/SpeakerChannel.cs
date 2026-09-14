namespace DenonAvrNet.Models;

/// <summary>
/// Identifies one or more active speaker channels reported by a Denon receiver.
/// Values can be combined because a receiver normally activates multiple outputs.
/// </summary>
[Flags]
public enum SpeakerChannel : ulong
{
    None = 0,

    FrontLeft = 1UL << 0, FrontRight = 1UL << 1, Center = 1UL << 2, Subwoofer = 1UL << 3,
    SurroundLeft = 1UL << 4, SurroundRight = 1UL << 5,
    SurroundBack = 1UL << 6, SurroundBackLeft = 1UL << 7, SurroundBackRight = 1UL << 8,
    FrontWideLeft = 1UL << 9, FrontWideRight = 1UL << 10,

    FrontHeightLeft = 1UL << 11, FrontHeightRight = 1UL << 12,
    TopFrontLeft = 1UL << 13, TopFrontRight = 1UL << 14,
    TopMiddleLeft = 1UL << 15, TopMiddleRight = 1UL << 16,
    TopRearLeft = 1UL << 17, TopRearRight = 1UL << 18,
    RearHeightLeft = 1UL << 19, RearHeightRight = 1UL << 20,
    SurroundHeightLeft = 1UL << 21, SurroundHeightRight = 1UL << 22,
    CenterHeight = 1UL << 23, TopSurround = 1UL << 24,

    FrontDolbyLeft = 1UL << 25, FrontDolbyRight = 1UL << 26,
    SurroundDolbyLeft = 1UL << 27, SurroundDolbyRight = 1UL << 28,
    BackDolbyLeft = 1UL << 29, BackDolbyRight = 1UL << 30
}
