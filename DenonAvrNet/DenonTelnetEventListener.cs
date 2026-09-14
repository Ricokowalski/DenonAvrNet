using System.Net.Sockets;
using System.Text;
using DenonAvrNet.Models;

namespace DenonAvrNet;

/// <summary>
/// Keeps a Telnet connection open and publishes unsolicited receiver events.
/// Reconnects automatically after a connection loss.
/// </summary>
public sealed class DenonTelnetEventListener : IAsyncDisposable
{
    private readonly TimeSpan _reconnectDelay;
    private CancellationTokenSource? _lifetime;
    private Task? _runTask;

    /// <summary>Creates a persistent Telnet event listener for a receiver host.</summary>
    public DenonTelnetEventListener(string host, TimeSpan? reconnectDelay = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        Host = host.Trim().Trim('[', ']');
        _reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(3);
        if (_reconnectDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(reconnectDelay));
        }
    }

    /// <summary>Gets the receiver hostname or IP address.</summary>
    public string Host { get; }

    /// <summary>Raised whenever the receiver sends an unsolicited Telnet message.</summary>
    public event Action<DenonTelnetEvent>? EventReceived;

    /// <summary>Raised after connecting or losing the persistent Telnet connection.</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>Raised for recoverable connection or read errors.</summary>
    public event Action<Exception>? Error;

    /// <summary>Starts the background listener. Calling this method twice is not allowed.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_lifetime is not null)
        {
            throw new InvalidOperationException("Der Telnet-Event-Listener wurde bereits gestartet.");
        }

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunAsync(_lifetime.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_lifetime is null)
        {
            return;
        }

        _lifetime.Cancel();
        try
        {
            if (_runTask is not null)
            {
                await _runTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down the listener.
        }
        finally
        {
            _lifetime.Dispose();
            _lifetime = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var connected = false;

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(Host, 23, cancellationToken).ConfigureAwait(false);
                connected = true;
                ConnectionChanged?.Invoke(true);

                await using var stream = client.GetStream();
                using var reader = new StreamReader(
                    stream,
                    Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null)
                    {
                        throw new IOException("Die Telnet-Verbindung wurde vom Receiver geschlossen.");
                    }

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        EventReceived?.Invoke(new DenonTelnetEvent(DateTimeOffset.Now, line.Trim()));
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Error?.Invoke(exception);
            }
            finally
            {
                if (connected)
                {
                    ConnectionChanged?.Invoke(false);
                }
            }

            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
