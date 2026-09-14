using DenonAvrNet.Models;

namespace DenonAvrNet.Tests;

public sealed class DenonSpeakerLevelChannelConverterTests
{
    [Theory]
    [InlineData(DenonSpeakerLevelChannel.FrontLeft, "FL")]
    [InlineData(DenonSpeakerLevelChannel.Subwoofer2, "SW2")]
    [InlineData(DenonSpeakerLevelChannel.TopMiddleRight, "TMR")]
    [InlineData(DenonSpeakerLevelChannel.BackDolbyRight, "BDR")]
    public void Converter_RoundTripsProtocolCodes(DenonSpeakerLevelChannel channel, string code)
    {
        Assert.Equal(code, DenonSpeakerLevelChannelConverter.ToProtocolCode(channel));
        Assert.True(DenonSpeakerLevelChannelConverter.TryFromProtocolCode(code, out var parsed));
        Assert.Equal(channel, parsed);
    }
}
