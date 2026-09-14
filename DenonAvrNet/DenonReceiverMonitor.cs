using DenonAvrNet.Models;

namespace DenonAvrNet;

/// <summary>
/// Maintains an up-to-date receiver status through Telnet events and a periodic
/// HTTP status refresh as a fallback for missed events.
/// </summary>
public sealed class DenonReceiverMonitor : IAsyncDisposable
{
    private readonly DenonAvrClient _receiver;
    private readonly DenonTelnetEventListener _telnetListener;
    private readonly TimeSpan _pollInterval;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CancellationTokenSource? _lifetime;
    private Task? _pollingTask;
    private Task? _eventRefreshTask;
    private int _eventRefreshQueued;

    /// <summary>Creates a monitor with a dedicated HTTP client and a 15-second fallback polling interval.</summary>
    public DenonReceiverMonitor(
        string host,
        TimeSpan? pollInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _receiver = new DenonAvrClient(host);
        _telnetListener = new DenonTelnetEventListener(host);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(15);
        if (_pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
        }
    }

    /// <summary>Gets the last status snapshot received by the monitor.</summary>
    public DenonReceiverState? CurrentState { get; private set; }

    /// <summary>Raised after the monitor has refreshed the HTTP status snapshot.</summary>
    public event Action<DenonReceiverState>? StateRefreshed;

    /// <summary>Raised immediately when an unsolicited Telnet message arrives.</summary>
    public event Action<DenonTelnetEvent>? TelnetEventReceived;

    /// <summary>Raised for recoverable Telnet or HTTP refresh errors.</summary>
    public event Action<Exception>? Error;

    /// <summary>Starts the event listener, performs an initial refresh and begins fallback polling.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_lifetime is not null)
        {
            throw new InvalidOperationException("Der Receiver-Monitor wurde bereits gestartet.");
        }

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _telnetListener.EventReceived += OnTelnetEventReceived;
        _telnetListener.Error += OnError;

        await _receiver.InitializeAsync(_lifetime.Token).ConfigureAwait(false);
        await _telnetListener.StartAsync(_lifetime.Token).ConfigureAwait(false);
        await RefreshAsync(_lifetime.Token).ConfigureAwait(false);
        _pollingTask = PollAsync(_lifetime.Token);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_lifetime is null)
        {
            return;
        }

        _lifetime.Cancel();
        _telnetListener.EventReceived -= OnTelnetEventReceived;
        _telnetListener.Error -= OnError;

        try
        {
            if (_pollingTask is not null)
            {
                await _pollingTask.ConfigureAwait(false);
            }

            if (_eventRefreshTask is not null)
            {
                await _eventRefreshTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            await _telnetListener.DisposeAsync().ConfigureAwait(false);
            _receiver.Dispose();
            _refreshLock.Dispose();
            _lifetime.Dispose();
            _lifetime = null;
        }
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnTelnetEventReceived(DenonTelnetEvent telnetEvent)
    {
        TelnetEventReceived?.Invoke(telnetEvent);

        // Receivers often emit several messages for one action. One refresh
        // after a short coalescing window is sufficient and avoids a request burst.
        if (Interlocked.Exchange(ref _eventRefreshQueued, 1) == 0)
        {
            _eventRefreshTask = RefreshAfterTelnetEventAsync();
        }
    }

    private async Task RefreshAfterTelnetEventAsync()
    {
        try
        {
            var cancellationToken = _lifetime?.Token ?? CancellationToken.None;
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
        finally
        {
            Volatile.Write(ref _eventRefreshQueued, 0);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                CurrentState = await _receiver.UpdateAsync(cancellationToken).ConfigureAwait(false);
                StateRefreshed?.Invoke(CurrentState);
            }
            finally
            {
                _refreshLock.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Error?.Invoke(exception);
        }
    }

    private void OnError(Exception exception) => Error?.Invoke(exception);
}
