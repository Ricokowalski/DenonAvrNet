namespace DenonAvrNet;

/// <summary>Specifies the transport used for a control command.</summary>
public enum DenonControlProtocol
{
    /// <summary>
    /// Uses HTTP after a successful <see cref="DenonAvrClient.InitializeAsync"/> call;
    /// otherwise uses Telnet. If the selected HTTP control command fails before a
    /// successful response, Telnet is used as a fallback.
    /// </summary>
    Auto,

    /// <summary>Uses the receiver's HTTP/XML control interface.</summary>
    Http,

    /// <summary>Uses the receiver's TCP control interface (normally port 23).</summary>
    Telnet
}
