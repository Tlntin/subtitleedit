using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal sealed record GptConnectionTestResult(bool Success, string Message);

internal sealed class GptConnectionTestService(HttpClient? client = null)
{
    internal async Task<GptConnectionTestResult> TestAsync(OpenAiCompatibleSettings settings, CancellationToken token)
    {
        var language = Se.Language.Video.AudioToText;
        var watch = Stopwatch.StartNew();
        try
        {
            // A real transcription request verifies the endpoint, key, model and response format.
            // Only this built-in synthetic voice is uploaded, never the user's selected media.
            using var audio = typeof(GptConnectionTestService).Assembly.GetManifestResourceStream("SpeechToTextConnectionTest.wav")
                ?? throw new InvalidDataException("Missing connection test audio");
            var response = await new OpenAiSttService(client ?? OpenAiSttService.SharedHttpClient, settings)
                .TranscribeAsync(audio, "connection-test.wav", cancellationToken: token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(response.Text) || settings.RequestProfile == OpenAiSttRequestProfile.GptDiarize &&
                (response.Segments is not { Count: > 0 } || response.Segments.Any(s =>
                    !double.IsFinite(s.Start) || !double.IsFinite(s.End) || s.Start < 0 || s.End <= s.Start)))
                return new(false, language.GptConnectionInvalidResponse);
            return new(true, string.Format(language.GptConnectionSuccess, settings.Model, watch.Elapsed.TotalSeconds.ToString("0.0")));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (TimeoutException) { return new(false, language.GptConnectionTimeout); }
        catch (OperationCanceledException) { return new(false, language.GptConnectionTimeout); }
        catch (HttpRequestException ex)
        {
            var hint = (int?)ex.StatusCode switch
            {
                401 => language.GptConnectionUnauthorized,
                403 => language.GptConnectionForbidden,
                404 => language.GptConnectionNotFound,
                429 => language.GptConnectionRateLimit,
                >= 500 => language.GptConnectionServerError,
                null => language.GptConnectionNetworkError,
                _ => language.GptConnectionRequestError,
            };
            var message = ex.StatusCode is { } status ? $"HTTP {(int)status}: {hint}" : hint;
            var detail = SafeErrorDetail(ex, settings);
            if (detail.Length > 0) message += Environment.NewLine + detail;
            return new(false, string.Format(language.GptConnectionFailed, message));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return new(false, language.GptConnectionInvalidResponse);
        }
    }

    private static string SafeErrorDetail(HttpRequestException exception, OpenAiCompatibleSettings settings)
    {
        // Only show the provider's structured message. Exception text can contain an endpoint
        // query or proxy credentials; it must not be copied into the UI or tools log wholesale.
        var marker = exception.Message.LastIndexOf("Response: ", StringComparison.Ordinal);
        if (marker < 0) return string.Empty;
        try
        {
            using var json = JsonDocument.Parse(exception.Message[(marker + 10)..]);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object) root = error;
            if (!root.TryGetProperty("message", out var value) || value.ValueKind != JsonValueKind.String) return string.Empty;
            var detail = value.GetString() ?? string.Empty;
            detail = detail.Replace(settings.EndpointUrl, OpenAiSttService.SanitizeEndpointForLog(settings.EndpointUrl), StringComparison.Ordinal);
            if (Uri.TryCreate(settings.EndpointUrl, UriKind.Absolute, out var endpoint))
            {
                if (endpoint.UserInfo.Length > 0) detail = detail.Replace(endpoint.UserInfo, "[redacted]", StringComparison.Ordinal);
                foreach (var pair in endpoint.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var equals = pair.IndexOf('=');
                    if (equals >= 0 && Uri.UnescapeDataString(pair[(equals + 1)..]) is { Length: > 0 } secret)
                        detail = detail.Replace(secret, "[redacted]", StringComparison.Ordinal);
                }
            }
            if (!string.IsNullOrEmpty(settings.ApiKey)) detail = detail.Replace(settings.ApiKey, "[redacted]", StringComparison.Ordinal);
            foreach (var line in settings.ExtraHeaders.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colon = line.IndexOf(':');
                if (colon >= 0 && line[(colon + 1)..].Trim() is { Length: > 0 } secret)
                    detail = detail.Replace(secret, "[redacted]", StringComparison.Ordinal);
            }
            return detail.Length > 500 ? detail[..500] + "…" : detail;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return string.Empty; }
    }
}
