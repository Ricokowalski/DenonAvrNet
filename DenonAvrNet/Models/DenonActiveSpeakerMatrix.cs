namespace DenonAvrNet.Models;

/// <summary>
/// Represents the compact <c>OPINFASP</c> active-speaker matrix sent by newer
/// Denon receivers. A value of 2 denotes an active output position; 0 denotes
/// an inactive/unavailable position. The receiver does not publish the channel
/// name order of the 32 proprietary positions.
/// </summary>
public sealed record DenonActiveSpeakerMatrix(
    string RawValues,
    IReadOnlyList<int> ActivePositions)
{
    /// <summary>Gets the number of output positions in this receiver matrix.</summary>
    public int PositionCount => RawValues.Length;

    /// <summary>Gets the number of positions currently reported as active.</summary>
    public int ActivePositionCount => ActivePositions.Count;
}
