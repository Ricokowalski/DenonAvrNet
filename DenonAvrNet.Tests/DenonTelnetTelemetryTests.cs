using DenonAvrNet.Models;

namespace DenonAvrNet.Tests;

public sealed class DenonTelnetTelemetryTests
{
    [Fact]
    public void TryParseActiveSpeakerMatrix_DecodesActiveOutputPositions()
    {
        const string message = "OPINFASP 22222200222000022000000002200000";

        var parsed = DenonTelnetTelemetry.TryParseActiveSpeakerMatrix(message, out var matrix);

        Assert.True(parsed);
        Assert.NotNull(matrix);
        Assert.Equal(32, matrix.PositionCount);
        Assert.Equal(13, matrix.ActivePositionCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 9, 10, 11, 16, 17, 26, 27 }, matrix.ActivePositions);
    }

    [Theory]
    [InlineData("OPINFASP 22A")]
    [InlineData("OPINFASP ")]
    [InlineData("MSDOLBY SURROUND")]
    public void TryParseActiveSpeakerMatrix_RejectsOtherOrMalformedMessages(string message)
    {
        var parsed = DenonTelnetTelemetry.TryParseActiveSpeakerMatrix(message, out DenonActiveSpeakerMatrix? matrix);

        Assert.False(parsed);
        Assert.Null(matrix);
    }
}
