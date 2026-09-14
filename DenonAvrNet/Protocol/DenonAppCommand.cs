using System.Xml.Linq;

namespace DenonAvrNet.Protocol;

/// <summary>Creates the XML requests understood by Denon's AppCommand API.</summary>
internal static class DenonAppCommand
{
    internal static string CreateMainZoneStatusRequest()
    {
        var commands = new[]
        {
            "GetAllZonePowerStatus",
            "GetAllZoneVolume",
            "GetAllZoneMuteStatus",
            "GetAllZoneSource",
            "GetDeletedSource"
        };

        return CreateTransaction(commands).ToString(SaveOptions.DisableFormatting);
    }

    private static XElement CreateTransaction(IEnumerable<string> commands) =>
        new("tx", commands.Select(command => new XElement("cmd", new XAttribute("id", "1"), command)));
}
