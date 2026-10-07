using DenonAvrNet.Models;
using DenonAvrNet.Profiles;

namespace DenonAvrNet.Tests;

public sealed class DenonReceiverProfileRegistryTests
{
    [Theory]
    [InlineData("AVC-X6800H", 8080, "avc-x6800h")]
    [InlineData("AVC-X6700H", 8080, "avc-x6700h")]
    [InlineData("AVR-X4100W", 80, "legacy-goform")]
    public void Select_UsesTheExpectedProfile(string modelName, int port, string expectedProfileId)
    {
        var deviceInfo = new DenonDeviceInfo(modelName, null, null, null, null, null);

        var profile = DenonReceiverProfileRegistry.Select(deviceInfo, port);

        Assert.Equal(expectedProfileId, profile.Id);
    }
    [Fact]
    public void Select_X6700H_EnablesCapturedSpeakerFeatures()
    {
        var deviceInfo = new DenonDeviceInfo("AVC-X6700H", null, null, null, null, null);

        var profile = DenonReceiverProfileRegistry.Select(deviceInfo, 8080);

        Assert.Equal(2, profile.SpeakerPresetCount);
        Assert.True(profile.SupportsSpeakerPresetLevels);
        Assert.True(profile.SupportsSpeakerDistances);
        Assert.True(profile.SupportsSpeakerPresetSelection);
        Assert.IsType<AjaxSpeakerPresetLevelProvider>(profile.SpeakerPresetLevels);
        Assert.IsType<AjaxSpeakerDistanceProvider>(profile.SpeakerDistances);
        Assert.IsType<AjaxSpeakerPresetSelectionProvider>(profile.SpeakerPresetSelection);
    }

}
