using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

internal static class SpeechToTextBatchFiles
{
    internal static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly HashSet<string> MediaExtensions = new(
        Utilities.VideoFileExtensions.Concat(Utilities.AudioFileExtensions), StringComparer.OrdinalIgnoreCase);

    // Explicitly selected files still allow any extension, like the file picker's "All files".
    // Folders only contribute audio/video files; external subtitles and artwork are ignored.
    internal static List<string> Collect(
        IEnumerable<string> paths,
        IEnumerable<string> existingFiles,
        bool recursive,
        CancellationToken token,
        Action<string> reportFolder)
    {
        var result = new List<string>();
        var seenFiles = new HashSet<string>(existingFiles.Select(Path.GetFullPath), PathComparer);
        var visitedFolders = new HashSet<string>(PathComparer);

        void AddFile(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (seenFiles.Add(fullPath))
            {
                result.Add(fullPath);
            }
        }

        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                AddFile(path);
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var folder = pending.Pop();
                try
                {
                    // Walk the resolved target so junctions/symlinks back to an ancestor cannot
                    // loop, and overlapping selections do not add the same tree twice.
                    folder = Directory.ResolveLinkTarget(folder, returnFinalTarget: true)?.FullName
                             ?? Path.GetFullPath(folder);
                    if (!visitedFolders.Add(folder))
                    {
                        continue;
                    }

                    reportFolder(folder);
                    foreach (var file in Directory.EnumerateFiles(folder).Order(PathComparer))
                    {
                        token.ThrowIfCancellationRequested();
                        if (MediaExtensions.Contains(Path.GetExtension(file)))
                        {
                            AddFile(file);
                        }
                    }

                    if (recursive)
                    {
                        foreach (var subFolder in Directory.EnumerateDirectories(folder).OrderDescending(PathComparer))
                        {
                            token.ThrowIfCancellationRequested();
                            pending.Push(subFolder);
                        }
                    }
                }
                catch (IOException)
                {
                    // Missing/unavailable folders must not prevent adding the remaining files.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip unreadable folders (including protected subfolders on network shares).
                }
            }
        }

        return result;
    }
}
