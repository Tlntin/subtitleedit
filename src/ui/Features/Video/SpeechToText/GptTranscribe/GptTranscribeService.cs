using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal sealed class GptTranscribeService : ISttTranscriber
{
    private readonly OpenAiSttService _client;
    private readonly OpenAiCompatibleSettings _settings;
    private readonly IGptAudioProcessor _audio;
    private readonly int _concurrency;

    internal GptTranscribeService(OpenAiCompatibleSettings settings, int concurrency,
        HttpClient? client = null, IGptAudioProcessor? audio = null)
    {
        _settings = settings;
        _client = new OpenAiSttService(client ?? OpenAiSttService.SharedHttpClient, settings);
        _audio = audio ?? new GptAudioProcessor(Configuration.Settings.General.FFmpegLocation);
        _concurrency = Math.Clamp(concurrency, 1, 4);
    }

    public async Task<OpenAiCompatibleSttResponse> TranscribeAsync(string audioFilePath, string? language = null,
        IProgress<string>? progress = null, IProgress<OpenAiCompatibleSegment>? segmentProgress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_settings.RequestProfile == OpenAiSttRequestProfile.GptDiarize)
        {
            var native = await RequestAsync(audioFilePath, language, cancellationToken).ConfigureAwait(false);
            if ((!string.IsNullOrWhiteSpace(native.Text) && (native.Segments == null || native.Segments.Count == 0)) ||
                native.Segments?.Any(s => !double.IsFinite(s.Start) || !double.IsFinite(s.End) || s.Start < 0 || s.End <= s.Start) == true)
                throw new InvalidDataException(Se.Language.Video.AudioToText.GptMissingTimings);
            return native;
        }

        var folder = Directory.CreateTempSubdirectory("se-gpt-vad-").FullName;
        var pcm = Path.Combine(folder, "audio.pcm");
        try
        {
            progress?.Report(Se.Language.Video.AudioToText.GptDetectingSpeech);
            await _audio.DecodeAsync(audioFilePath, pcm, cancellationToken).ConfigureAwait(false);
            var intervals = await Task.Run(() => _audio.Detect(pcm, cancellationToken), cancellationToken).ConfigureAwait(false);
            var result = new OpenAiCompatibleSttResponse
            {
                Segments = new List<OpenAiCompatibleSegment>(),
                Duration = new FileInfo(pcm).Length / 32000.0,
            };
            if (intervals.Count == 0)
            {
                progress?.Report(Se.Language.Video.AudioToText.GptNoSpeech);
                return result; // Silence is never uploaded to the API.
            }

            for (var first = 0; first < intervals.Count; first += _concurrency)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = intervals.Skip(first).Take(_concurrency).Select(async (interval, i) =>
                {
                    var clip = Path.Combine(folder, $"clip-{first + i}.wav");
                    try
                    {
                        GptAudioProcessor.WriteClip(pcm, clip, interval, cancellationToken);
                        var response = await RequestAsync(clip, language, cancellationToken).ConfigureAwait(false);
                        return (interval, response);
                    }
                    finally { if (File.Exists(clip)) File.Delete(clip); }
                }).ToArray();

                // Emit in timeline order even when a later request finishes first. The main
                // editor uses segment end times for live progress and batch ETA.
                var responses = await Task.WhenAll(batch).ConfigureAwait(false);
                foreach (var (interval, response) in responses)
                {
                    if (!string.IsNullOrWhiteSpace(response.Language)) result.Language ??= response.Language;
                    var text = response.Text.Trim();
                    if (text.Length == 0) continue;
                    var segment = new OpenAiCompatibleSegment
                    {
                        Id = result.Segments.Count, Start = interval.Start, End = interval.End, Text = text,
                    };
                    result.Segments.Add(segment);
                    segmentProgress?.Report(segment);
                }
                progress?.Report(string.Format(Se.Language.Video.AudioToText.GptTranscribingParts,
                    Math.Min(first + _concurrency, intervals.Count), intervals.Count));
            }

            result.Text = string.Join(" ", result.Segments.Select(s => s.Text));
            return result;
        }
        finally
        {
            // This private directory contains only files created by this invocation.
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                try { File.Delete(file); } catch (IOException) { }
            }
            try { Directory.Delete(folder); } catch (IOException) { }
        }
    }

    private async Task<OpenAiCompatibleSttResponse> RequestAsync(string file, string? language, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await _client.TranscribeAsync(file, language, cancellationToken: token).ConfigureAwait(false); }
            catch (HttpRequestException ex) when (attempt < 2 && (ex.StatusCode == HttpStatusCode.TooManyRequests || (int?)ex.StatusCode >= 500))
            {
                var seconds = ex.Data["RetryAfterSeconds"] is double retry ? retry : 2 * (attempt + 1);
                _settings.Logger?.Invoke($"GPT transcription: retrying HTTP {(int?)ex.StatusCode}, attempt {attempt + 1}");
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 0.1, 60)), token).ConfigureAwait(false);
            }
        }
    }
}
