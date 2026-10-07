using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DenonAvrNet.Exceptions;
using DenonAvrNet.Protocol;

namespace DenonAvrNet.Profiles;

/// <summary>Speaker-preset selection API used by the AVC-X6800H web interface.</summary>
internal sealed class AjaxSpeakerPresetSelectionProvider : ISpeakerPresetSelectionProvider
{
    private const int SpeakerSetupHttpPort = 11080;

    private static readonly TimeSpan ConfirmationPollInterval = TimeSpan.FromSeconds(1);

    public async Task<int> GetActivePresetAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken)
    {
        var response = await context.HttpTransport.GetStringAsync(
            context.Host,
            SpeakerSetupHttpPort,
            DenonEndpoints.SpeakerPreset(),
            cancellationToken).ConfigureAwait(false);

        return ParseActivePreset(response);
        // <-----------
    }

    public async Task SelectPresetAsync(
        DenonProfileContext context,
        int preset,
        TimeSpan confirmationTimeout,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await context.HttpTransport.GetStringAsync(
                context.Host,
                SpeakerSetupHttpPort,
                DenonEndpoints.SetSpeakerPreset(preset),
                cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HTTP timeout, not a caller cancellation: the receiver may still be
            // switching. The result is unknown and is confirmed by reading below.
        }

        await WaitForActivePresetAsync(
            context,
            preset,
            confirmationTimeout,
            cancellationToken).ConfigureAwait(false);

        try
        {
            _ = await context.HttpTransport.GetStringAsync(
                context.Host,
                SpeakerSetupHttpPort,
                DenonEndpoints.StopTestTone(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                          exception is HttpRequestException or TaskCanceledException)
        {
            throw new DenonProtocolException(
                $"Speaker Preset {preset} ist aktiv, aber der Testton konnte nicht gestoppt werden.",
                exception);
            // <-----------
        }
    }

    private async Task WaitForActivePresetAsync(
        DenonProfileContext context,
        int preset,
        TimeSpan confirmationTimeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(confirmationTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        var token = linkedSource.Token;
        Exception? lastError = null;

        try
        {
            while (true)
            {
                try
                {
                    var activePreset = await GetActivePresetAsync(context, token).ConfigureAwait(false);

                    if (activePreset == preset)
                    {
                        return;
                        // <-----------
                    }

                    lastError = null;
                }
                catch (Exception exception) when (!token.IsCancellationRequested &&
                                                  exception is HttpRequestException
                                                      or TaskCanceledException
                                                      or DenonProtocolException)
                {
                    // The receiver may not answer (or may answer invalid XML) while it is switching;
                    // try again until the limit.
                    lastError = exception;
                }

                await Task.Delay(ConfirmationPollInterval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            var message =
                $"Der Receiver hat den Wechsel auf Speaker Preset {preset} nicht innerhalb von " +
                $"{confirmationTimeout.TotalSeconds:0} Sekunden bestätigt. " +
                "Der Wechsel kann auf dem Receiver trotzdem noch laufen.";

            throw lastError is null
                ? new DenonProtocolException(message)
                : new DenonProtocolException(message, lastError);
            // <-----------
        }
    }

    private static int ParseActivePreset(string xml)
    {
        XElement? root;

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

            root = XDocument.Load(xmlReader, LoadOptions.None).Root;
        }
        catch (XmlException exception)
        {
            throw new DenonProtocolException("Die XML-Antwort des Receivers ist ungültig.", exception);
            // <-----------
        }

        if (root is null ||
            !root.Name.LocalName.Equals("SpeakerPreset", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(root.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var preset))
        {
            throw new DenonProtocolException(
                "Die Receiver-Antwort enthält kein gültiges SpeakerPreset-Element.");
            // <-----------
        }

        return preset;
        // <-----------
    }
}

internal sealed class UnsupportedSpeakerPresetSelectionProvider(string receiverDescription)
    : ISpeakerPresetSelectionProvider
{
    public Task<int> GetActivePresetAsync(
        DenonProfileContext context,
        CancellationToken cancellationToken) =>
        Task.FromException<int>(CreateException());

    public Task SelectPresetAsync(
        DenonProfileContext context,
        int preset,
        TimeSpan confirmationTimeout,
        CancellationToken cancellationToken) =>
        Task.FromException(CreateException());

    private NotSupportedException CreateException() => new(
        $"Speaker-preset selection has not yet been implemented for {receiverDescription}. " +
        "Capture the speaker-preset requests and add a dedicated provider for this receiver profile.");
}