using DenonAvrNet.Protocol;

namespace DenonAvrNet.Tests;

public sealed class DenonVolumeCodecTests
{
    [Theory]
    [InlineData(-80.0, "00")]
    [InlineData(-79.5, "005")]
    [InlineData(-35.5, "445")]
    [InlineData(0.0, "80")]
    [InlineData(0.5, "805")]
    [InlineData(18.0, "98")]
    public void ToTelnetValue_UsesDenonAbsoluteEncoding(double volumeDb, string expected)
    {
        Assert.Equal(expected, DenonVolumeCodec.ToTelnetValue(volumeDb));
    }

    [Theory]
    [InlineData("00", -80.0)]
    [InlineData("005", -79.5)]
    [InlineData("445", -35.5)]
    [InlineData("80", 0.0)]
    [InlineData("805", 0.5)]
    [InlineData("98", 18.0)]
    public void ParseTelnetValue_ReturnsLibraryDecibels(string value, double expected)
    {
        Assert.Equal(expected, DenonVolumeCodec.ParseTelnetValue(value));
    }

    [Fact]
    public void ParseTelnetValue_DoesNotTreatZeroAsMute()
    {
        Assert.Equal(-80.0, DenonVolumeCodec.ParseTelnetValue("00"));
        Assert.Equal(0.0, DenonVolumeCodec.ParseTelnetValue("80"));
    }

    [Fact]
    public void ParseTelnetValue_LegacyMinimumSentinelIsNotMuteOrZeroDb()
    {
        Assert.Null(DenonVolumeCodec.ParseTelnetValue("99"));
    }

    [Theory]
    [InlineData(-35.26, -35.5, "-35.5")]
    [InlineData(0.24, 0.0, "0.0")]
    [InlineData(0.26, 0.5, "0.5")]
    public void HttpAndTelnetShareTheSameNormalizedDbValue(
        double input,
        double expectedDb,
        string expectedHttp)
    {
        Assert.Equal(expectedDb, DenonVolumeCodec.NormalizeDecibels(input));
        Assert.Equal(expectedHttp, DenonVolumeCodec.ToHttpValue(input));
    }
}
