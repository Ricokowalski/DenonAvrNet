using System.Xml.Linq;

namespace DenonAvrNet.Protocol;

/// <summary>Creates the XML requests understood by Denon's AppCommand API.</summary>
internal static class DenonAppCommand
{
    internal static string CreateMainZoneStatusRequest()
    {
        string[] commands =
        [
            "GetAllZonePowerStatus",
            "GetAllZoneVolume",
            "GetAllZoneMuteStatus",
            "GetAllZoneSource",
            "GetDeletedSource"
        ];

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "tx",
                commands.Select(command =>
                    new XElement("cmd", new XAttribute("id", "1"), command))));

        return document.ToString(SaveOptions.DisableFormatting);
    }
}
