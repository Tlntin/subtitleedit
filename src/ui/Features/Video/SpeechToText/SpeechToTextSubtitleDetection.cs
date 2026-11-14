using Nikse.SubtitleEdit.UiLogic.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

internal enum ExistingSubtitleKind { None, Embedded, External }

internal sealed class SpeechToTextSubtitleDetection
{
    // Deliberately exclude generic metadata extensions such as .xml/.json/.txt:
    // movie.xml is often library metadata and must not suppress transcription.
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup", ".smi", ".sami",
        ".scc", ".stl", ".ttml", ".dfxp", ".sbv", ".mks", ".lrc", ".mpl", ".890", ".cap",
    };
    private readonly Dictionary<string, string[]> _sidecars = new(SpeechToTextBatchFiles.PathComparer);

    internal ExistingSubtitleKind Detect(SpeechToTextJobItem item, bool embedded, bool external)
    {
        if (embedded && item.MediaInfo.Tracks.Any(t => t.TrackType == FfmpegTrackType.Subtitle))
            return ExistingSubtitleKind.Embedded;
        return external && HasExternalSubtitle(item.InputVideoFileName)
            ? ExistingSubtitleKind.External : ExistingSubtitleKind.None;
    }

    internal bool HasExternalSubtitle(string mediaFile)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(mediaFile))!;
        if (!_sidecars.TryGetValue(directory, out var names))
        {
            try
            {
                names = Directory.EnumerateFiles(directory)
                    .Where(p => Extensions.Contains(Path.GetExtension(p)))
                    .Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
            }
            catch (IOException) { names = Array.Empty<string>(); }
            catch (UnauthorizedAccessException) { names = Array.Empty<string>(); }
            _sidecars[directory] = names;
        }

        var stem = Path.GetFileNameWithoutExtension(mediaFile);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return names.Any(n => n.Equals(stem, comparison) || n.StartsWith(stem + ".", comparison));
    }
}
