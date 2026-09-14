using System.Net.Sockets;
using System.Text;

namespace DenonAvrNet;

/// <summary>Provides Denon's TCP/IP control protocol, normally available on port 23.</summary>
public sealed class DenonTelnetClient
{
    private readonly TimeSpan _timeout;

    /// <summary>Creates a Telnet protocol client for a receiver host.</summary>
    public DenonTelnetClient(string host, TimeSpan? requestTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        Host = host.Trim().Trim('[', ']');
        _timeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }
    }

    /// <summary>Gets the receiver hostname or IP address.</summary>
    public string Host { get; }

    /// <summary>Sends one Denon TCP command and returns its first response line.</summary>
    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (command.Contains('\r') || command.Contains('\n'))
        {
            throw new ArgumentException("Ein Telnet-Befehl darf keinen Zeilenumbruch enthalten.", nameof(command));
        }

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        var token = linkedSource.Token;
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(Host, 23, token).ConfigureAwait(false);
        await using var stream = tcpClient.GetStream();
        var request = Encoding.ASCII.GetBytes($"{command}\r");
        await stream.WriteAsync(request, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        var response = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var character = (char)buffer[0];
            if (character is '\r' or '\n')
            {
                if (response.Length > 0)
                {
                    break;
                }

                continue;
            }

            response.Append(character);
        }

        if (response.Length == 0)
        {
            throw new IOException("Der Receiver hat auf den Telnet-Befehl keine Antwort gesendet.");
        }

        return response.ToString();
    }

    /// <summary>Queries the current Zone 2 state.</summary>
    public Task<string> QueryZone2Async(CancellationToken cancellationToken = default) =>
        SendCommandAsync("Z2?", cancellationToken);

    /// <summary>Queries the current Zone 3 state.</summary>
    public Task<string> QueryZone3Async(CancellationToken cancellationToken = default) =>
        SendCommandAsync("Z3?", cancellationToken);

    /// <summary>Switches Zone 2 on or off.</summary>
    public Task<string> SetZone2PowerAsync(bool on, CancellationToken cancellationToken = default) =>
        SendCommandAsync(on ? "Z2ON" : "Z2OFF", cancellationToken);

    /// <summary>Switches Zone 3 on or off.</summary>
    public Task<string> SetZone3PowerAsync(bool on, CancellationToken cancellationToken = default) =>
        SendCommandAsync(on ? "Z3ON" : "Z3OFF", cancellationToken);
}
