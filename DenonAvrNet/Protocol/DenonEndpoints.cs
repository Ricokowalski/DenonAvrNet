namespace DenonAvrNet.Protocol;

internal static class DenonEndpoints
{
    internal const string DeviceInfo = "/goform/Deviceinfo.xml";
    internal const string AppCommand = "/goform/AppCommand.xml";
    internal const string MainZoneStatus = "/goform/formMainZone_MainZoneXmlStatus.xml";
    internal const string PowerOn = "/goform/formiPhoneAppPower.xml?1+PowerOn";
    internal const string PowerStandby = "/goform/formiPhoneAppPower.xml?1+PowerStandby";
    internal const string VolumeUp = "/goform/formiPhoneAppDirect.xml?MVUP";
    internal const string VolumeDown = "/goform/formiPhoneAppDirect.xml?MVDOWN";
    internal const string MuteOn = "/goform/formiPhoneAppMute.xml?1+MuteOn";
    internal const string MuteOff = "/goform/formiPhoneAppMute.xml?1+MuteOff";

    internal static string SetVolume(string invariantVolume) =>
        $"/goform/formiPhoneAppVolume.xml?1+{invariantVolume}";

    internal static string SetInput(string input) =>
        $"/goform/formiPhoneAppDirect.xml?SI{Uri.EscapeDataString(input)}";
}
