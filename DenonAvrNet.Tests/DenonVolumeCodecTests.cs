using DenonAvrNet.Protocol;

namespace DenonAvrNet.Tests;

public sealed class DenonVolumeCodecTests
{
    [Theory]
    [InlineData(0.0, "00")]
    [InlineData(0.5, "005")]
    [InlineData(44.5, "445")]
    [InlineData(80.0, "80")]
    [InlineData(80.5, "805")]
    [InlineData(98.0, "98")]
    public void ToTelnetValue_UsesAbsoluteDenonScale(double volume, string expected) =>
        Assert.Equal(expected, DenonVolumeCodec.ToTelnetValue(volume));

    [Theory]
    [InlineData(0.0, "-80.0")]
    [InlineData(44.5, "-35.5")]
    [InlineData(80.0, "0.0")]
    [InlineData(98.0, "18.0")]
    public void ToHttpValue_ConvertsAbsoluteScaleToDb(double volume, string expected) =>
        Assert.Equal(expected, DenonVolumeCodec.ToHttpValue(volume));

    [Theory]
    [InlineData(-80.0, 0.0)]
    [InlineData(-35.5, 44.5)]
    [InlineData(0.0, 80.0)]
    [InlineData(18.0, 98.0)]
    public void FromDecibels_ConvertsToAbsoluteScale(double db, double expected) =>
        Assert.Equal(expected, DenonVolumeCodec.FromDecibels(db));

    [Theory]
    [InlineData("00", 0.0)]
    [InlineData("005", 0.5)]
    [InlineData("445", 44.5)]
    [InlineData("80", 80.0)]
    [InlineData("805", 80.5)]
    [InlineData("98", 98.0)]
    public void ParseTelnetValue_ReturnsAbsoluteDenonScale(string raw, double expected) =>
        Assert.Equal(expected, DenonVolumeCodec.ParseTelnetValue(raw));

    [Fact]
    public void ParseTelnetValue_99IsNotMute() =>
        Assert.Null(DenonVolumeCodec.ParseTelnetValue("99"));
}
