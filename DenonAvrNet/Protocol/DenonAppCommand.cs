using System.Xml.Linq;

namespace DenonAvrNet.Protocol;

/// <summary>Creates the XML requests understood by Denon's AppCommand API.</summary>
internal static class DenonAppCommand
{
    internal const string GetAllZonePowerStatus = "GetAllZonePowerStatus";
    internal const string GetAllZoneVolume = "GetAllZoneVolume";
    internal const string GetAllZoneMuteStatus = "GetAllZoneMuteStatus";
    internal const string GetAllZoneSource = "GetAllZoneSource";
    internal const string GetDeletedSource = "GetDeletedSource";

    internal const string GetAudioInfo = "GetAudioInfo";

    internal const string GetActiveSpeaker = "GetActiveSpeaker";

    internal static readonly IReadOnlyList<string> MainZoneStatusCommands =
    [
        GetAllZonePowerStatus,
        GetAllZoneVolume,
        GetAllZoneMuteStatus,
        GetAllZoneSource
    ];

    internal static string CreateRequest(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return CreateRequest([command]);
    }

    internal static string CreateRequest(IReadOnlyCollection<string> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commands),
                commands.Count,
                "Eine AppCommand-Anfrage muss zwischen einem und fünf Befehlen enthalten.");
        }

        if (commands.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Befehlsnamen dürfen nicht leer sein.", nameof(commands));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "tx",
                commands.Select(command =>
                    new XElement("cmd", new XAttribute("id", "1"), command))));

        return Serialize(document);
    }

    internal static string CreateDetailedRequest(string command, params string[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Parameternamen dürfen nicht leer sein.", nameof(parameters));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "tx",
                new XElement(
                    "cmd",
                    new XAttribute("id", "3"),
                    new XElement("name", command),
                    new XElement(
                        "list",
                        parameters.Select(parameter =>
                            new XElement("param", new XAttribute("name", parameter)))))));

        return Serialize(document);
    }

    private static string Serialize(XDocument document)
    {
        // The embedded XML parser used by current Denon receivers (including
        // the AVC-X6800H) requires the document element to start on a new line.
        // Without this CRLF it acknowledges the POST with an empty <rx/>.
        return $"{document.Declaration}\r\n{document.Root!.ToString(SaveOptions.DisableFormatting)}";
    }
}
