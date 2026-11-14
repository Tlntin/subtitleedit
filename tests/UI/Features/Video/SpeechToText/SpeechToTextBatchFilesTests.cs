using Nikse.SubtitleEdit.Features.Video.SpeechToText;

namespace UITests.Features.Video.SpeechToText;

public class SpeechToTextBatchFilesTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("se-stt-folders-");

    public void Dispose() => _root.Delete(recursive: true);

    private string FileIn(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_root.FullName, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private List<string> Collect(string[] paths, bool recursive = true, string[]? existing = null,
        CancellationToken? token = null, Action<string>? reportFolder = null) =>
        SpeechToTextBatchFiles.Collect(paths, existing ?? [], recursive,
            token ?? TestContext.Current.CancellationToken, reportFolder ?? (_ => { }));

    [Fact]
    public void RecursiveFolder_CollectsAudioAndVideoAcrossNestedChinesePaths()
    {
        var video = FileIn("episode.MKV");
        var audio = FileIn("课程/第二课/讲解.FLAC");
        FileIn("episode.srt");
        FileIn("课程/第二课/cover.jpg");
        FileIn("课程/第二课/subtitle.sup");

        var files = Collect([_root.FullName]);

        Assert.Equal(2, files.Count);
        Assert.Contains(video, files);
        Assert.Contains(audio, files);
    }

    [Fact]
    public void NonRecursiveFolder_DoesNotIncludeSubfolders()
    {
        var video = FileIn("episode.mp4");
        FileIn("season 2/episode.mp4");

        Assert.Equal([video], Collect([_root.FullName], recursive: false));
    }

    [Fact]
    public void OverlappingFoldersAndFiles_SkipDuplicatesAndExistingQueue()
    {
        var existing = FileIn("episode.mp4");
        var nested = FileIn("season 2/episode.mp4");

        var files = Collect([nested, _root.FullName, Path.GetDirectoryName(nested)!, nested], existing: [existing]);

        Assert.Equal([nested], files);
    }

    [Fact]
    public void ExplicitFile_PreservesAllFilesPickerSupportAndNormalizesPaths()
    {
        var file = FileIn("audio.custom-extension");

        var files = Collect([Path.Combine(_root.FullName, ".", Path.GetFileName(file)), file]);

        Assert.Equal([file], files);
    }

    [Fact]
    public void MissingFolder_DoesNotPreventAddingOtherSelections()
    {
        var file = FileIn("episode.wav");

        Assert.Equal([file], Collect([Path.Combine(_root.FullName, "missing"), file]));
    }

    [Fact]
    public void EmptyOrNonMediaFolder_ReturnsNoFiles()
    {
        FileIn("readme.txt");
        FileIn("poster.png");

        Assert.Empty(Collect([_root.FullName]));
    }

    [Fact]
    public void CancellationDuringScan_StopsBeforeCollectingFiles()
    {
        FileIn("season 1/episode.mp4");
        using var cts = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() =>
            Collect([_root.FullName], token: cts.Token, reportFolder: _ => cts.Cancel()));
    }

    [Fact]
    public void CancelledBeforeScan_DoesNotVisitFolders()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var visited = false;

        Assert.Throws<OperationCanceledException>(() =>
            Collect([_root.FullName], token: cts.Token, reportFolder: _ => visited = true));
        Assert.False(visited);
    }

    [Fact]
    public void JunctionCycleAndOverlappingAlias_CollectEachFileOnce()
    {
        var file = FileIn("season 1/episode.mp4");
        var loop = Path.Combine(_root.FullName, "season 1", "loop");
        try
        {
            Directory.CreateSymbolicLink(loop, _root.FullName);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.Skip("Creating directory symlinks requires permission on this platform.");
        }
        catch (IOException)
        {
            Assert.Skip("Directory symlinks are unavailable on this filesystem.");
        }

        try
        {
            Assert.Equal([file], Collect([_root.FullName, loop]));
        }
        finally
        {
            Directory.Delete(loop);
        }
    }

    [Fact]
    public void PathComparison_MatchesPlatformCaseSensitivity()
    {
        var first = FileIn("episode.wav");
        if (OperatingSystem.IsWindows())
        {
            Assert.Empty(Collect([first], existing: [first.ToUpperInvariant()]));
        }
        else
        {
            Assert.False(SpeechToTextBatchFiles.PathComparer.Equals("episode.wav", "EPISODE.wav"));
        }
    }
}
