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
}
