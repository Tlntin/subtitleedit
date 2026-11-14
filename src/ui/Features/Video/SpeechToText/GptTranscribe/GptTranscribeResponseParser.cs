using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal static class GptTranscribeResponseParser
{
    internal static OpenAiCompatibleSttResponse Parse(string json, bool requireTimings)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("GPT transcription response has no text field.");

        var response = new OpenAiCompatibleSttResponse { Text = text.GetString() ?? string.Empty };
        if (root.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.String)
            response.Language = language.GetString();
        else if (root.TryGetProperty("languages", out var languages) && languages.ValueKind == JsonValueKind.Array)
            response.Language = languages.EnumerateArray().Select(l => l.TryGetProperty("code", out var code) ? code.GetString() : null)
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

        if (!requireTimings) return response;
        var segments = new List<OpenAiCompatibleSegment>();
        if (root.TryGetProperty("segments", out var source) && source.ValueKind == JsonValueKind.Array)
        {
            foreach (var segment in source.EnumerateArray())
            {
                var caption = segment.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(caption)) continue;
                if (!segment.TryGetProperty("start", out var s) || s.ValueKind != JsonValueKind.Number || !s.TryGetDouble(out var start) ||
                    !segment.TryGetProperty("end", out var e) || e.ValueKind != JsonValueKind.Number || !e.TryGetDouble(out var end) ||
                    !double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start)
                    throw new InvalidDataException(Se.Language.Video.AudioToText.GptMissingTimings);
                // Diarize IDs may be strings ("seg_001"), unlike Whisper's numeric IDs.
                segments.Add(new OpenAiCompatibleSegment { Id = segments.Count, Start = start, End = end, Text = caption });
            }
        }

        if (segments.Count == 0 && !string.IsNullOrWhiteSpace(response.Text))
            throw new InvalidDataException(Se.Language.Video.AudioToText.GptMissingTimings);
        response.Segments = segments.OrderBy(s => s.Start).ToList();
        return response;
    }
}
