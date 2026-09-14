using DenonAvrNet.Models;

namespace DenonAvrNet.Tests;

public sealed class SpeakerChannelParserTests
{
    [Fact]
    public void Parse_MapsAllSpeakerFamilies()
    {
        var result = SpeakerChannelParser.Parse(
            ["FL", "SB", "SBL", "SBR", "FWL", "FWR", "FHL", "TFL", "TML", "TRL", "RHL", "SHL", "CH", "TS", "FDL", "SDL", "BDL"]);

        Assert.Equal(
            SpeakerChannel.FrontLeft |
            SpeakerChannel.SurroundBack |
            SpeakerChannel.SurroundBackLeft |
            SpeakerChannel.SurroundBackRight |
            SpeakerChannel.FrontWideLeft |
            SpeakerChannel.FrontWideRight |
            SpeakerChannel.FrontHeightLeft |
            SpeakerChannel.TopFrontLeft |
            SpeakerChannel.TopMiddleLeft |
            SpeakerChannel.TopRearLeft |
            SpeakerChannel.RearHeightLeft |
            SpeakerChannel.SurroundHeightLeft |
            SpeakerChannel.CenterHeight |
            SpeakerChannel.TopSurround |
            SpeakerChannel.FrontDolbyLeft |
            SpeakerChannel.SurroundDolbyLeft |
            SpeakerChannel.BackDolbyLeft,
            result);
    }

    [Fact]
    public void Parse_IgnoresUnknownCodes()
    {
        Assert.Equal(SpeakerChannel.Center, SpeakerChannelParser.Parse(["C", "UNKNOWN"]));
    }
}
