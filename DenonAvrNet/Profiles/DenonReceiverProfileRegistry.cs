using DenonAvrNet.Models;

namespace DenonAvrNet.Profiles;

/// <summary>Maps reported Denon model families to the small API differences they expose.</summary>
internal static class DenonReceiverProfileRegistry
{
    private static readonly IDenonReceiverProfile X6800 =
        new AvcX6800hProfile();

    private static readonly IDenonReceiverProfile X6700 =
        new AvcX6700hProfile();

    private static readonly IDenonReceiverProfile Legacy =
        new LegacyReceiverProfile();

    private static readonly IDenonReceiverProfile Unknown =
        new UnknownReceiverProfile();

    internal static IDenonReceiverProfile Select(
        DenonDeviceInfo deviceInfo,
        int httpPort)
    {
        ArgumentNullException.ThrowIfNull(deviceInfo);

        var model =
            $"{deviceInfo.ModelName} {deviceInfo.ManualModelName}";

        if (model.Contains(
            "X6800",
            StringComparison.OrdinalIgnoreCase))
        {
            return X6800;
        }

        if (model.Contains(
            "X6700",
            StringComparison.OrdinalIgnoreCase))
        {
            return X6700;
        }

        // Older units usually expose their control pages on port 80.
        // This is deliberately a separate profile: do not send
        // X6800-only AJAX calls to it.
        return httpPort == 80
            ? Legacy
            : Unknown;
    }
}

internal sealed class AvcX6800hProfile : IDenonReceiverProfile
{
    public string Id => "avc-x6800h";
    public int SpeakerPresetCount => 2;
    public bool SupportsSpeakerPresetLevels => true;
    public bool SupportsSpeakerDistances => true;
    public bool SupportsSpeakerPresetSelection => true;

    public ISpeakerPresetLevelProvider SpeakerPresetLevels { get; } =
        new AjaxSpeakerPresetLevelProvider(DenonSpeakerAjaxOptions.AvcX6800H);

    public ISpeakerDistanceProvider SpeakerDistances { get; } =
        new AjaxSpeakerDistanceProvider(DenonSpeakerAjaxOptions.AvcX6800H);

    public ISpeakerPresetSelectionProvider SpeakerPresetSelection { get; } =
        new AjaxSpeakerPresetSelectionProvider(DenonSpeakerAjaxOptions.AvcX6800H);
}

internal sealed class AvcX6700hProfile : IDenonReceiverProfile
{
    public string Id => "avc-x6700h";
    public int SpeakerPresetCount => 2;
    public bool SupportsSpeakerPresetLevels => true;
    public bool SupportsSpeakerDistances => true;
    public bool SupportsSpeakerPresetSelection => true;

    public ISpeakerPresetLevelProvider SpeakerPresetLevels { get; } =
        new AjaxSpeakerPresetLevelProvider(DenonSpeakerAjaxOptions.AvcX6700H);

    public ISpeakerDistanceProvider SpeakerDistances { get; } =
        new AjaxSpeakerDistanceProvider(DenonSpeakerAjaxOptions.AvcX6700H);

    public ISpeakerPresetSelectionProvider SpeakerPresetSelection { get; } =
        new AjaxSpeakerPresetSelectionProvider(DenonSpeakerAjaxOptions.AvcX6700H);
}

internal sealed class LegacyReceiverProfile : IDenonReceiverProfile
{
    public string Id => "legacy-goform";
    public int SpeakerPresetCount => 0;
    public bool SupportsSpeakerPresetLevels => false;
    public bool SupportsSpeakerDistances => false;
    public bool SupportsSpeakerPresetSelection => false;

    public ISpeakerPresetLevelProvider SpeakerPresetLevels { get; } =
        new UnsupportedSpeakerPresetLevelProvider(
            "legacy GoForm receiver");

    public ISpeakerDistanceProvider SpeakerDistances { get; } =
        new UnsupportedSpeakerDistanceProvider(
            "legacy GoForm receiver");

    public ISpeakerPresetSelectionProvider SpeakerPresetSelection { get; } =
        new UnsupportedSpeakerPresetSelectionProvider(
            "legacy GoForm receiver");
}

internal sealed class UnknownReceiverProfile : IDenonReceiverProfile
{
    public string Id => "unknown";
    public int SpeakerPresetCount => 0;
    public bool SupportsSpeakerPresetLevels => false;
    public bool SupportsSpeakerDistances => false;
    public bool SupportsSpeakerPresetSelection => false;

    public ISpeakerPresetLevelProvider SpeakerPresetLevels { get; } =
        new UnsupportedSpeakerPresetLevelProvider(
            "this receiver");

    public ISpeakerDistanceProvider SpeakerDistances { get; } =
        new UnsupportedSpeakerDistanceProvider(
            "this receiver");

    public ISpeakerPresetSelectionProvider SpeakerPresetSelection { get; } =
        new UnsupportedSpeakerPresetSelectionProvider(
            "this receiver");
}