namespace DenonAvrNet.Protocol;

internal static class DenonEndpoints
{
    internal static string SpeakerPresetLevels() =>
        $"/ajax/speakers/get_config?type=5&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    internal static string SpeakerDistances() =>
        $"/ajax/speakers/get_config?type=4&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    internal static string SpeakerPreset() =>
    $"/ajax/speakers/get_config?type=11&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    internal static string SetSpeakerPreset(int preset)
    {
        var data = $"<SpeakerPreset>{preset}</SpeakerPreset>";

        return
            $"/ajax/speakers/set_config?type=11&data={Uri.EscapeDataString(data)}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        // <-----------
    }

    internal static string StopTestTone()
    {
        const string data = "<StopTestTone></StopTestTone>";

        return
            $"/ajax/speakers/set_config?type=20&data={Uri.EscapeDataString(data)}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        // <-----------
    }

    internal const string DeviceInfo =
        "/goform/Deviceinfo.xml";

    internal const string AppCommand =
        "/goform/AppCommand.xml";

    internal const string AppCommand0300 =
        "/goform/AppCommand0300.xml";

    internal const string MainZoneStatus =
        "/goform/formMainZone_MainZoneXmlStatus.xml";

    internal const string PowerOn =
        "/goform/formiPhoneAppPower.xml?1+PowerOn";

    internal const string PowerStandby =
        "/goform/formiPhoneAppPower.xml?1+PowerStandby";

    internal const string VolumeUp =
        "/goform/formiPhoneAppDirect.xml?MVUP";

    internal const string VolumeDown =
        "/goform/formiPhoneAppDirect.xml?MVDOWN";

    internal const string MuteOn =
        "/goform/formiPhoneAppMute.xml?1+MuteOn";

    internal const string MuteOff =
        "/goform/formiPhoneAppMute.xml?1+MuteOff";

    internal static string SetVolume(
        string invariantVolume) =>
        $"/goform/formiPhoneAppVolume.xml?1+{invariantVolume}";

    internal static string ZoneVolumeUp(int zone) =>
        $"/goform/formiPhoneAppDirect.xml?Z{zone}UP";

    internal static string ZoneVolumeDown(int zone) =>
        $"/goform/formiPhoneAppDirect.xml?Z{zone}DOWN";

    internal static string SetZoneVolume(int zone, string invariantVolume) =>
        $"/goform/formiPhoneAppVolume.xml?{zone}+{invariantVolume}";

    internal static string SetZoneMute(int zone, bool muted) =>
        $"/goform/formiPhoneAppMute.xml?{zone}+{(muted ? "MuteOn" : "MuteOff")}";

    internal static string SetInput(string input)
    {
        // Denon's command parser expects slashes in protocol source names such
        // as SAT/CBL to remain literal inside the query command.
        var escapedInput = Uri.EscapeDataString(input)
            .Replace(
                "%2F",
                "/",
                StringComparison.OrdinalIgnoreCase);

        return
            $"/goform/formiPhoneAppDirect.xml?SI{escapedInput}";
    }

    internal static string SetSpeakerPresetLevel(
        int speakerIndex,
        int tenthsOfDecibels,
        int configType,
        bool wrapInList)
    {
        var speaker =
            $"<Speaker index=\"{speakerIndex}\">{tenthsOfDecibels}</Speaker>";

        var data = wrapInList
            ? $"<List>{speaker}</List>"
            : speaker;

        return
            $"/ajax/speakers/set_config?type={configType}&data={Uri.EscapeDataString(data)}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    internal static string SetSpeakerDistance(
    int speakerIndex,
    int rawDistance)
    {
        var data =
            $"<Distances><List><Speaker index=\"{speakerIndex}\">{rawDistance}</Speaker></List></Distances>";

        return
            $"/ajax/speakers/set_config?type=4&data={Uri.EscapeDataString(data)}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }
}