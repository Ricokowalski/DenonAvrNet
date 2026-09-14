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
