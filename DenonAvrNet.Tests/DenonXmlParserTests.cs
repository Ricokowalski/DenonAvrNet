using DenonAvrNet.Exceptions;
using DenonAvrNet.Protocol;

namespace DenonAvrNet.Tests;

public sealed class DenonXmlParserTests
{
    [Fact]
    public void ParseDeviceInfo_ReadsExpectedValues()
    {
        var result = DenonXmlParser.ParseDeviceInfo(TestXml.DeviceInfo);

        Assert.Equal("AVC-X6800H", result.ModelName);
        Assert.Equal("AVC-X6800H", result.ManualModelName);
        Assert.Equal("0301", result.CommunicationApiVersion);
        Assert.Equal(3, result.ZoneCount);
        Assert.Equal("001122334455", result.MacAddress);
        Assert.Equal("AV RECEIVER", result.CategoryName);
    }

    [Fact]
    public void ParseMainZoneStatus_ReadsExpectedValues()
    {
        var result = DenonXmlParser.ParseMainZoneStatus(TestXml.MainZoneStatus);

        Assert.True(result.IsPoweredOn);
        Assert.Equal("ON", result.Power);
        Assert.Equal("Media Player", result.Input);
        Assert.Equal(-35.5, result.VolumeDb);
        Assert.False(result.IsMuted);
        Assert.Equal(
            new[] { "CBL/SAT", "Media Player", "TV AUDIO", "PHONO" },
            result.AvailableInputs);
    }

    [Fact]
    public void ParseAppCommandMainZoneStatus_ReadsExpectedValues()
    {
        var result = DenonXmlParser.ParseAppCommandMainZoneStatus(
            TestXml.AppCommandPower,
            TestXml.AppCommandVolume,
            TestXml.AppCommandMute,
            TestXml.AppCommandSource,
            TestXml.AppCommandDeletedSources,
            TestXml.AppCommandAudioInfo,
            TestXml.AppCommandActiveSpeakers);

        Assert.True(result.IsPoweredOn);
        Assert.Equal("ON", result.Power);
        Assert.Equal("MPLAY", result.Input);
        Assert.Equal(-35.5, result.VolumeDb);
        Assert.False(result.IsMuted);
        Assert.Equal(
            new[] { "CBL/SAT", "Media Player", "PHONO" },
            result.AvailableInputs);
        Assert.NotNull(result.Audio);
        Assert.Equal("HDMI", result.Audio.InputMode);
        Assert.Equal("Speaker", result.Audio.Output);
        Assert.Equal("Dolby Audio - Dolby Digital Plus", result.Audio.AudioFormat);
        Assert.Equal("Dolby Surround", result.Audio.SoundMode);
        Assert.Equal("48 kHz", result.Audio.SampleRate);
        Assert.Equal(new[] { "SW", "FL", "FR", "SL", "SR" }, result.Audio.ActiveSpeakers);
    }

    [Fact]
    public void ParseAppCommandMainZoneStatus_ToleratesOneUnsupportedCommand()
    {
        var result = DenonXmlParser.ParseAppCommandMainZoneStatus(
            TestXml.AppCommandPower,
            TestXml.AppCommandError,
            TestXml.AppCommandMute,
            TestXml.AppCommandSource,
            TestXml.AppCommandDeletedSources);

        Assert.Equal("ON", result.Power);
        Assert.Null(result.VolumeDb);
        Assert.False(result.IsMuted);
        Assert.Equal("MPLAY", result.Input);
        Assert.Null(result.Audio);
    }

    [Fact]
    public void ParseDeviceInfo_RejectsExternalDocumentType()
    {
        const string unsafeXml = """
            <!DOCTYPE Device_Info [<!ENTITY xxe SYSTEM "file:///windows/win.ini">]>
            <Device_Info><ModelName>&xxe;</ModelName></Device_Info>
            """;

        Assert.Throws<DenonProtocolException>(() => DenonXmlParser.ParseDeviceInfo(unsafeXml));
    }

    [Fact]
    public void ParseDeviceInfo_RejectsMissingModelName()
    {
        const string incompleteXml = "<Device_Info><CommApiVers>0301</CommApiVers></Device_Info>";

        Assert.Throws<DenonProtocolException>(() => DenonXmlParser.ParseDeviceInfo(incompleteXml));
    }
}
