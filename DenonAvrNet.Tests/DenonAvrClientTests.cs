using System.Net;
using DenonAvrNet.Transport;

namespace DenonAvrNet.Tests;

public sealed class DenonAvrClientTests
{
    [Fact]
    public async Task InitializeAsync_FallsBackFromPort80ToPort8080()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
            request.RequestUri!.Port == 80
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : StubHttpMessageHandler.Xml(TestXml.DeviceInfo));
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("192.0.2.10", transport);

        var device = await client.InitializeAsync();

        Assert.Equal("AVC-X6800H", device.ModelName);
        Assert.Equal(8080, client.HttpPort);
        Assert.Equal(new[] { 80, 8080 }, handler.RequestedUris.Select(uri => uri.Port).ToArray());
    }

    [Fact]
    public async Task UpdateAsync_StoresConfirmedReceiverState()
    {
        var handler = CreateInitializedReceiverHandler();
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("10.37.0.190", transport);
        await client.InitializeAsync();

        var state = await client.UpdateAsync();

        Assert.Same(state, client.State);
        Assert.True(state.IsPoweredOn);
        Assert.Equal(-35.5, state.VolumeDb);
    }

    [Fact]
    public async Task UpdateAsync_UsesAppCommandPostOnPort8080()
    {
        var responses = new Queue<string>(
        [
            TestXml.AppCommandPower,
            TestXml.AppCommandVolume,
            TestXml.AppCommandMute,
            TestXml.AppCommandSource,
            TestXml.AppCommandDeletedSources
        ]);
        var handler = new StubHttpMessageHandler((request, _) =>
            request.RequestUri!.Port == 80
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : request.RequestUri.AbsolutePath switch
                {
                    "/goform/Deviceinfo.xml" => StubHttpMessageHandler.Xml(TestXml.DeviceInfo),
                    "/goform/AppCommand.xml" =>
                        StubHttpMessageHandler.Xml(responses.Dequeue()),
                    _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
                });
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("10.37.0.190", transport);
        await client.InitializeAsync();

        var state = await client.UpdateAsync();

        Assert.All(handler.RequestMethods.TakeLast(5), method => Assert.Equal(HttpMethod.Post, method));
        Assert.All(
            handler.RequestedUris.TakeLast(5),
            uri => Assert.Equal("/goform/AppCommand.xml", uri.AbsolutePath));
        Assert.All(
            handler.RequestContentTypes.TakeLast(5),
            type => Assert.Equal("text/xml", type));
        Assert.All(
            handler.RequestContentCharsets.TakeLast(5),
            charset => Assert.Equal("utf-8", charset));
        Assert.All(
            handler.RequestBodies.TakeLast(5),
            body => Assert.StartsWith(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<tx>",
                body));
        Assert.Contains("GetAllZonePowerStatus", handler.RequestBodies[^5]);
        Assert.Contains("GetAllZoneVolume", handler.RequestBodies[^4]);
        Assert.Contains("GetAllZoneMuteStatus", handler.RequestBodies[^3]);
        Assert.Contains("GetAllZoneSource", handler.RequestBodies[^2]);
        Assert.Contains("GetDeletedSource", handler.RequestBodies[^1]);
        Assert.Empty(responses);
        Assert.Equal(-35.5, state.VolumeDb);
    }

    [Theory]
    [InlineData(-80.1)]
    [InlineData(18.1)]
    public async Task SetVolumeAsync_RejectsOutOfRangeValue(double volume)
    {
        var handler = CreateInitializedReceiverHandler();
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("10.37.0.190", transport);
        await client.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SetVolumeAsync(volume));
    }

    [Fact]
    public async Task SetVolumeAsync_RoundsToHalfDecibelAndUsesInvariantFormat()
    {
        var handler = CreateInitializedReceiverHandler();
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("10.37.0.190", transport);
        await client.InitializeAsync();

        await client.SetVolumeAsync(-35.26);

        Assert.Equal(
            "/goform/formiPhoneAppVolume.xml?1+-35.5",
            handler.RequestedUris[^1].PathAndQuery);
    }

    [Fact]
    public async Task SetInputAsync_EncodesSpacesInInputName()
    {
        var handler = CreateInitializedReceiverHandler();
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("10.37.0.190", transport);
        await client.InitializeAsync();

        await client.SetInputAsync("TV AUDIO");

        Assert.Equal(
            "/goform/formiPhoneAppDirect.xml?SITV%20AUDIO",
            handler.RequestedUris[^1].PathAndQuery);
    }

    [Fact]
    public async Task PowerOnAsync_SendsExpectedCommand()
    {
        var handler = CreateInitializedReceiverHandler();
        using var transport = new DenonHttpTransport(handler, TimeSpan.FromSeconds(1));
        using var client = new DenonAvrClient("10.37.0.190", transport);
        await client.InitializeAsync();

        await client.PowerOnAsync();

        Assert.Equal(
            "/goform/formiPhoneAppPower.xml?1+PowerOn",
            handler.RequestedUris[^1].PathAndQuery);
    }

    private static StubHttpMessageHandler CreateInitializedReceiverHandler() =>
        new((request, _) => request.RequestUri!.AbsolutePath switch
        {
            "/goform/Deviceinfo.xml" => StubHttpMessageHandler.Xml(TestXml.DeviceInfo),
            "/goform/formMainZone_MainZoneXmlStatus.xml" =>
                StubHttpMessageHandler.Xml(TestXml.MainZoneStatus),
            _ => StubHttpMessageHandler.Ok()
        });
}
