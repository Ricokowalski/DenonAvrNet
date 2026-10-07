namespace DenonAvrNet.Profiles;

/// <summary>
/// Describes the receiver-specific transport and payload details of Denon's
/// AJAX speaker-setup API. The logical endpoints are shared by several models,
/// while scheme, port and write payloads can differ between generations.
/// </summary>
internal sealed record DenonSpeakerAjaxOptions(
    string Scheme,
    int Port,
    int SpeakerLevelSetType,
    bool WrapSpeakerLevelInList,
    bool StopTestToneAfterPresetSelection,
    bool AllowUntrustedServerCertificate)
{
    internal static DenonSpeakerAjaxOptions AvcX6800H { get; } = new(
        Uri.UriSchemeHttp,
        11080,
        20,
        false,
        true,
        false);

    internal static DenonSpeakerAjaxOptions AvcX6700H { get; } = new(
        Uri.UriSchemeHttps,
        10443,
        5,
        true,
        false,
        true);
}
