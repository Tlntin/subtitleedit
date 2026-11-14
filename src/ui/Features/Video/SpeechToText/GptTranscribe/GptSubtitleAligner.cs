using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Features.Files.ImportPlainText;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal interface IGptSubtitleAligner
{
    Task<IReadOnlyList<OpenAiCompatibleSegment>> AlignAsync(string clip, string text, double duration, CancellationToken token);
}

internal sealed class GptSubtitleAligner(string executable, string model) : IGptSubtitleAligner
{
    internal static GptSubtitleAligner? FindInstalled()
    {
        foreach (var folder in new[] { Se.CrispAsrFolder, Path.Combine(Se.GetApplicationDataFolder(), "Subtitle Edit", "CrispASR") }.Distinct())
        {
            var executable = Path.Combine(folder, CrispAsrQwen3.GetExecutableFileName());
            var model = Path.Combine(folder, "models", ForcedAlignerOption.Qwen3().FileName);
            if (File.Exists(executable) && File.Exists(model)) return new(executable, model);
        }
        return null;
    }

    public async Task<IReadOnlyList<OpenAiCompatibleSegment>> AlignAsync(string clip, string text, double duration, CancellationToken token)
    {
        var lines = SpeechToTextViewModel.SplitIntoSentences(text).SelectMany(s => s.Replace("\r", "").Split('\n'))
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        var textFile = Path.ChangeExtension(clip, ".txt");
        await File.WriteAllTextAsync(textFile, string.Join(Environment.NewLine, lines), new UTF8Encoding(false), token).ConfigureAwait(false);
        var srt = await new CrispAsrAlignOnlyRunner(executable, model).AlignAsync(clip, textFile, token).ConfigureAwait(false);
        var subtitle = new Subtitle();
        new SubRip().LoadSubtitle(subtitle, srt.Split('\n').Select(s => s.TrimEnd('\r')).ToList(), "aligned.srt");
        if (subtitle.Paragraphs.Count != lines.Length || subtitle.Paragraphs.Any(p =>
            !double.IsFinite(p.StartTime.TotalSeconds) || !double.IsFinite(p.EndTime.TotalSeconds) ||
            p.StartTime.TotalSeconds < 0 || p.EndTime.TotalSeconds <= p.StartTime.TotalSeconds || p.EndTime.TotalSeconds > duration + 0.2))
            throw new InvalidDataException("The aligner did not return valid times for every input sentence.");
        // Keep the API text verbatim; alignment may only change its timing.
        return subtitle.Paragraphs.Select((p, i) => new OpenAiCompatibleSegment
        {
            Id = i, Start = p.StartTime.TotalSeconds, End = Math.Min(duration, p.EndTime.TotalSeconds), Text = lines[i],
        }).ToArray();
    }
}
