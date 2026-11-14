using System;
using System.Collections.Generic;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal static class GptContextChunkPlanner
{
    // VAD suggests quiet boundaries; it never decides which audio is discarded.
    // Keep enough context for conversational ASR instead of uploading every short utterance.
    internal static IReadOnlyList<SpeechInterval> Plan(IReadOnlyList<SpeechInterval> speech, double duration)
    {
        if (!double.IsFinite(duration) || duration <= 0) return Array.Empty<SpeechInterval>();
        const double target = 24, minimum = 18, maximum = 30;
        var gaps = new List<double>();
        for (var i = 1; i < speech.Count; i++)
            if (speech[i].Start - speech[i - 1].End >= 0.20)
                gaps.Add((speech[i - 1].End + speech[i].Start) / 2);
        var result = new List<SpeechInterval>();
        var start = 0.0;
        while (duration - start > maximum)
        {
            var end = gaps.Where(g => g >= start + minimum && g <= start + maximum)
                .OrderBy(g => Math.Abs(g - start - target)).Cast<double?>().FirstOrDefault() ?? start + target;
            result.Add(new SpeechInterval(start, end));
            start = end;
        }
        result.Add(new SpeechInterval(start, duration));
        return result;
    }
}
