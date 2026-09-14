using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DenonAvrNet.Exceptions;
using DenonAvrNet.Models;

namespace DenonAvrNet.Protocol;

internal static class DenonXmlParser
{
    internal static DenonDeviceInfo ParseDeviceInfo(string xml)
    {
        var root = ParseSecurely(xml).Root;

        if (root is null || !NameEquals(root, "Device_Info"))
        {
            throw new DenonProtocolException("Die Antwort ist keine gültige Denon-Deviceinfo-Antwort.");
        }

        var modelName = Value(root, "ModelName");
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new DenonProtocolException("Die Geräteantwort enthält keinen Modellnamen.");
        }

        return new DenonDeviceInfo(
            modelName,
            Value(root, "ManualModelName"),
            Value(root, "CommApiVers"),
            ParseNullableInt(Value(root, "DeviceZones")),
            Value(root, "MacAddress"),
            Value(root, "CategoryName"));
    }

    internal static DenonReceiverState ParseMainZoneStatus(string xml)
    {
        var root = ParseSecurely(xml).Root;

        if (root is null || !NameEquals(root, "item"))
        {
            throw new DenonProtocolException("Die Antwort ist keine gültige Main-Zone-Statusantwort.");
        }

        var power = NestedValue(root, "Power") ?? "UNKNOWN";
        var inputs = root.Elements()
            .FirstOrDefault(element => NameEquals(element, "InputFuncList"))?
            .Elements()
            .Where(element => NameEquals(element, "value"))
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0)
            .ToArray() ?? [];

        return new DenonReceiverState(
            power.Equals("ON", StringComparison.OrdinalIgnoreCase),
            power,
            NestedValue(root, "InputFuncSelect"),
            ParseNullableDouble(NestedValue(root, "MasterVolume")),
            ParseNullableBoolean(NestedValue(root, "Mute")),
            inputs);
    }

    internal static DenonReceiverState ParseAppCommandMainZoneStatus(
        string powerXml,
        string volumeXml,
        string muteXml,
        string sourceXml,
        string deletedSourcesXml,
        string? audioInfoXml = null,
        string? activeSpeakersXml = null)
    {
        var powerCommand = ParseAppCommandResponse(powerXml);
        var volumeCommand = ParseAppCommandResponse(volumeXml);
        var muteCommand = ParseAppCommandResponse(muteXml);
        var sourceCommand = ParseAppCommandResponse(sourceXml);
        var deletedSourcesCommand = ParseAppCommandResponse(deletedSourcesXml);

        var inputs = deletedSourcesCommand is null
            ? []
            : ParseAvailableInputs(deletedSourcesCommand);
        var audio = ParseAudioInfo(audioInfoXml, activeSpeakersXml);
        return CreateMainZoneState(
            powerCommand,
            volumeCommand,
            muteCommand,
            sourceCommand,
            inputs) with
        {
            Audio = audio
        };
    }

    internal static DenonReceiverState ParseBundledAppCommandMainZoneStatus(
        string xml,
        IReadOnlyList<string> availableInputs)
    {
        ArgumentNullException.ThrowIfNull(availableInputs);
        var responses = ParseAppCommandResponses(xml);

        if (responses.Count != DenonAppCommand.MainZoneStatusCommands.Count)
        {
            throw new DenonProtocolException(
                $"Die gebündelte AppCommand-Antwort enthält {responses.Count} statt " +
                $"{DenonAppCommand.MainZoneStatusCommands.Count} Ergebnissen.");
        }

        return CreateMainZoneState(
            CommandOrNull(responses[0]),
            CommandOrNull(responses[1]),
            CommandOrNull(responses[2]),
            CommandOrNull(responses[3]),
            availableInputs);
    }

    internal static DenonReceiverState ParseSeparateAppCommandMainZoneStatus(
        string powerXml,
        string volumeXml,
        string muteXml,
        string sourceXml,
        IReadOnlyList<string> availableInputs)
    {
        ArgumentNullException.ThrowIfNull(availableInputs);
        return CreateMainZoneState(
            ParseAppCommandResponse(powerXml),
            ParseAppCommandResponse(volumeXml),
            ParseAppCommandResponse(muteXml),
            ParseAppCommandResponse(sourceXml),
            availableInputs);
    }

    internal static IReadOnlyList<string> ParseAppCommandAvailableInputs(string xml)
    {
        var command = ParseAppCommandResponse(xml);
        return command is null ? [] : ParseAvailableInputs(command);
    }

    internal static DenonAudioInfo? ParseAppCommandAudioInfo(
        string? audioInfoXml,
        string? activeSpeakersXml) =>
        ParseAudioInfo(audioInfoXml, activeSpeakersXml);

    private static DenonAudioInfo? ParseAudioInfo(
        string? audioInfoXml,
        string? activeSpeakersXml)
    {
        var audioCommand = ParseOptionalAppCommandResponse(audioInfoXml);
        var activeSpeakersCommand = ParseOptionalAppCommandResponse(activeSpeakersXml);
        var activeSpeakers = activeSpeakersCommand?
            .Descendants()
            .Where(element => NameEquals(element, "param"))
            .Where(element => string.Equals(
                AttributeValue(element, "control"),
                "2",
                StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var inputMode = ParameterValue(audioCommand, "inputmode");
        var output = ParameterValue(audioCommand, "output");
        var audioFormat = ParameterValue(audioCommand, "signal");
        var soundMode = ParameterValue(audioCommand, "sound");
        var sampleRate = ParameterValue(audioCommand, "fs");

        if (inputMode is null && output is null && audioFormat is null &&
            soundMode is null && sampleRate is null && activeSpeakers.Length == 0)
        {
            return null;
        }

        return new DenonAudioInfo(
            inputMode,
            output,
            audioFormat,
            soundMode,
            sampleRate,
            activeSpeakers);
    }

    private static XElement? ParseOptionalAppCommandResponse(string? xml) =>
        string.IsNullOrWhiteSpace(xml)
            ? null
            : ParseAppCommandResponse(xml);

    private static XElement? ParseAppCommandResponse(string xml)
    {
        // Unsupported commands are represented by <error> instead of <cmd>.
        return ParseAppCommandResponses(xml)
            .FirstOrDefault(element => NameEquals(element, "cmd"));
    }

    private static IReadOnlyList<XElement> ParseAppCommandResponses(string xml)
    {
        var root = ParseSecurely(xml).Root;

        if (root is null || !NameEquals(root, "rx"))
        {
            throw new DenonProtocolException("Die Antwort ist keine gültige Denon-AppCommand-Antwort.");
        }

        return root.Elements()
            .Where(element => NameEquals(element, "cmd") || NameEquals(element, "error"))
            .ToArray();
    }

    private static XElement? CommandOrNull(XElement response) =>
        NameEquals(response, "cmd") ? response : null;

    private static DenonReceiverState CreateMainZoneState(
        XElement? powerCommand,
        XElement? volumeCommand,
        XElement? muteCommand,
        XElement? sourceCommand,
        IReadOnlyList<string> availableInputs)
    {
        var mainZone = CreateZoneState(powerCommand, volumeCommand, muteCommand, sourceCommand, "zone1");

        if (mainZone is null)
        {
            throw new DenonProtocolException(
                "Die AppCommand-Antworten enthalten keine auswertbaren Main-Zone-Statuswerte.");
        }

        return new DenonReceiverState(
            mainZone.IsPoweredOn,
            mainZone.Power,
            mainZone.Input,
            mainZone.VolumeDb,
            mainZone.IsMuted,
            availableInputs,
            Zone2: CreateZoneState(powerCommand, volumeCommand, muteCommand, sourceCommand, "zone2"),
            Zone3: CreateZoneState(powerCommand, volumeCommand, muteCommand, sourceCommand, "zone3"));
    }

    private static DenonZoneState? CreateZoneState(
        XElement? powerCommand, XElement? volumeCommand, XElement? muteCommand,
        XElement? sourceCommand, string zoneName)
    {
        var power = powerCommand is null ? null : Value(powerCommand, zoneName);
        var volume = volumeCommand is null ? null : NestedValue(volumeCommand, zoneName, "volume");
        var mute = muteCommand is null ? null : Value(muteCommand, zoneName);
        var input = sourceCommand is null ? null : NestedValue(sourceCommand, zoneName, "source");
        if (power is null && volume is null && mute is null && input is null)
        {
            return null;
        }

        var normalizedPower = power ?? "UNKNOWN";
        return new DenonZoneState(
            normalizedPower.Equals("ON", StringComparison.OrdinalIgnoreCase), normalizedPower,
            input, ParseNullableDouble(volume), ParseNullableBoolean(mute));
    }

    private static string? ParameterValue(XElement? command, string parameterName)
    {
        if (command is null)
        {
            return null;
        }

        var value = command.Descendants()
            .FirstOrDefault(element =>
                NameEquals(element, "param") &&
                string.Equals(
                    AttributeValue(element, "name"),
                    parameterName,
                    StringComparison.OrdinalIgnoreCase))?
            .Value
            .Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? AttributeValue(XElement element, string attributeName) =>
        element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals(
                attributeName,
                StringComparison.OrdinalIgnoreCase))?
            .Value;

    private static XDocument ParseSecurely(string xml)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 2_000_000
            };

            using var textReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(textReader, settings);
            return XDocument.Load(xmlReader, LoadOptions.None);
        }
        catch (XmlException exception)
        {
            throw new DenonProtocolException("Die XML-Antwort des Receivers ist ungültig.", exception);
        }
    }

    private static string? Value(XElement parent, string elementName)
    {
        var value = parent.Elements()
            .FirstOrDefault(element => NameEquals(element, elementName))?
            .Value
            .Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NestedValue(XElement parent, string elementName)
    {
        var value = parent.Elements()
            .FirstOrDefault(element => NameEquals(element, elementName))?
            .Elements()
            .FirstOrDefault(element => NameEquals(element, "value"))?
            .Value
            .Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NestedValue(XElement parent, string containerName, string elementName)
    {
        var value = parent.Elements()
            .FirstOrDefault(element => NameEquals(element, containerName))?
            .Elements()
            .FirstOrDefault(element => NameEquals(element, elementName))?
            .Value
            .Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> ParseAvailableInputs(XElement command)
    {
        if (!NameEquals(command, "cmd"))
        {
            return [];
        }

        return command.Elements()
            .FirstOrDefault(element => NameEquals(element, "functiondelete"))?
            .Elements()
            .Where(element => NameEquals(element, "list"))
            .Where(element => !string.Equals(
                Value(element, "use"),
                "0",
                StringComparison.OrdinalIgnoreCase))
            .Select(element => Value(element, "FuncName") ?? Value(element, "name"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray() ?? [];
    }

    private static bool NameEquals(XElement element, string localName) =>
        element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase);

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static double? ParseNullableDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static bool? ParseNullableBoolean(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Equals("ON", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("OFF", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}
