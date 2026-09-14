namespace DenonAvrNet.Protocol;

internal static class DenonInputSource
{
    private static readonly IReadOnlyDictionary<string, string> ProtocolNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TV AUDIO"] = "TV",
            ["iPod/USB"] = "USB/IPOD",
            ["Bluetooth"] = "BT",
            ["Blu-ray"] = "BD",
            ["CBL/SAT"] = "SAT/CBL",
            ["NETWORK"] = "NET",
            ["Media Player"] = "MPLAY",
            ["AUX"] = "AUX1",
            ["Tuner"] = "TUNER",
            ["FM"] = "TUNER",
            ["SpotifyConnect"] = "Spotify Connect"
        };

    internal static string ToProtocolName(string input) =>
        ProtocolNames.TryGetValue(input, out var protocolName)
            ? protocolName
            : input;
}
