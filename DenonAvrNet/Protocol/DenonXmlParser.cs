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

    internal static DenonReceiverState ParseAppCommandMainZoneStatus(string xml)
    {
        var root = ParseSecurely(xml).Root;

        if (root is null || !NameEquals(root, "rx"))
        {
            throw new DenonProtocolException("Die Antwort ist keine gültige Denon-AppCommand-Antwort.");
        }

        var responses = root.Elements().ToArray();
        if (responses.Length < 4 || responses.Take(4).Any(element => !NameEquals(element, "cmd")))
        {
            throw new DenonProtocolException("Die AppCommand-Antwort enthält nicht alle Main-Zone-Statuswerte.");
        }

        var power = Value(responses[0], "zone1") ?? "UNKNOWN";
        var volume = NestedValue(responses[1], "zone1", "volume");
        var mute = Value(responses[2], "zone1");
        var input = NestedValue(responses[3], "zone1", "source");
        var inputs = responses.Length >= 5
            ? ParseAvailableInputs(responses[4])
            : [];

        return new DenonReceiverState(
            power.Equals("ON", StringComparison.OrdinalIgnoreCase),
            power,
            input,
            ParseNullableDouble(volume),
            ParseNullableBoolean(mute),
            inputs);
    }

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
