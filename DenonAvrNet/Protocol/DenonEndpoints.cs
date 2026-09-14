namespace DenonAvrNet.Protocol;

internal static class DenonEndpoints
{
    internal static string SpeakerPresetLevels() =>
        $"/ajax/speakers/get_config?type=5&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    internal const string DeviceInfo = "/goform/Deviceinfo.xml";
    internal const string AppCommand = "/goform/AppCommand.xml";
    internal const string AppCommand0300 = "/goform/AppCommand0300.xml";
    internal const string MainZoneStatus = "/goform/formMainZone_MainZoneXmlStatus.xml";
    internal const string PowerOn = "/goform/formiPhoneAppPower.xml?1+PowerOn";
    internal const string PowerStandby = "/goform/formiPhoneAppPower.xml?1+PowerStandby";
    internal const string VolumeUp = "/goform/formiPhoneAppDirect.xml?MVUP";
    internal const string VolumeDown = "/goform/formiPhoneAppDirect.xml?MVDOWN";
    internal const string MuteOn = "/goform/formiPhoneAppMute.xml?1+MuteOn";
    internal const string MuteOff = "/goform/formiPhoneAppMute.xml?1+MuteOff";

    internal static string SetVolume(string invariantVolume) =>
        $"/goform/formiPhoneAppVolume.xml?1+{invariantVolume}";

    internal static string SetInput(string input)
    {
        // Denon's command parser expects slashes in protocol source names such
        // as SAT/CBL to remain literal inside the query command.
        var escapedInput = Uri.EscapeDataString(input)
            .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        return $"/goform/formiPhoneAppDirect.xml?SI{escapedInput}";
    }

    internal static string SetSpeakerPresetLevel(int speakerIndex, int tenthsOfDecibels)
    {
        var data = $"<Speaker index=\"{speakerIndex}\">{tenthsOfDecibels}</Speaker>";
        return $"/ajax/speakers/set_config?type=20&data={Uri.EscapeDataString(data)}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }
}
