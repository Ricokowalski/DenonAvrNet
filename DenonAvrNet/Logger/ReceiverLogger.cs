using System.Text;

namespace DenonAvrNet.Logger;

/// <summary>
/// Simple, thread-safe file logger shared by all receiver protocol layers.
/// Logging is disabled by default and does nothing until a path has been set.
/// The path defaults to <c>c:\temp\denon.log</c>.
/// </summary>
public static class ReceiverLogger
{
    private const string DefaultFilePath = @"c:\temp\denon.log";
    private const string TelnetSource = "TELNET";
    private static readonly object Gate = new();
    private static readonly UTF8Encoding LogEncoding = new(false);
    private static volatile bool _enabled;
    private static volatile bool _telnetLoggingEnabled = true;
    private static string? _filePath = DefaultFilePath;

    /// <summary>Gets or sets whether log lines are written.</summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Gets or sets whether log lines with the source <c>TELNET</c> are written (default: true).</summary>
    public static bool TelnetLoggingEnabled
    {
        get => _telnetLoggingEnabled;
        set => _telnetLoggingEnabled = value;
    }

    /// <summary>
    /// Gets or sets the full path of the log file. The value is validated when set and a missing
    /// directory is created. Setting the path does not create or change the file itself.
    /// </summary>
    public static string? FilePath
    {
        get
        {
            lock (Gate)
            {
                return _filePath;
                // <-----------
            }
        }
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            var fullPath = System.IO.Path.GetFullPath(value.Trim());
            if (Directory.Exists(fullPath))
            {
                throw new ArgumentException("Der Log-Pfad darf kein Verzeichnis sein.", nameof(value));
                // <-----------
            }

            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (Gate)
            {
                _filePath = fullPath;
            }
        }
    }

    /// <summary>Overwrites the log file with empty content.</summary>
    /// <exception cref="InvalidOperationException">No path has been set.</exception>
    public static void Clear()
    {
        lock (Gate)
        {
            if (_filePath is null)
            {
                throw new InvalidOperationException("Es wurde kein Log-Pfad gesetzt.");
                // <-----------
            }

            EnsureDirectoryExists(_filePath);
            File.WriteAllText(_filePath, string.Empty, LogEncoding);
        }
    }

    internal static void Write(string source, string message)
    {
        if (!_enabled)
        {
            return;
            // <-----------
        }

        if (!_telnetLoggingEnabled &&
            string.Equals(source, TelnetSource, StringComparison.Ordinal))
        {
            return;
            // <-----------
        }

        try
        {
            lock (Gate)
            {
                if (_filePath is null)
                {
                    return;
                    // <-----------
                }

                EnsureDirectoryExists(_filePath);
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}] {message}{Environment.NewLine}";
                File.AppendAllText(_filePath, line, LogEncoding);
            }
        }
        catch (Exception exception) when (exception is IOException or
                                              UnauthorizedAccessException or
                                              ArgumentException or
                                              NotSupportedException)
        {
            // Logging must never break receiver logic.
        }
    }

    internal static void WriteException(string source, string context, Exception exception)
    {
        if (!_enabled)
        {
            return;
            // <-----------
        }

        Write(source, $"{context} failed: {exception.GetType().Name}: {exception.Message}");
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = System.IO.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}