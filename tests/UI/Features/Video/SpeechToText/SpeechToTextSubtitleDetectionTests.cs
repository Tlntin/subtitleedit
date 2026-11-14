using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.UiLogic.Media;
using System.Runtime.CompilerServices;

namespace UITests.Features.Video.SpeechToText;

public class SpeechToTextSubtitleDetectionTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("se-stt-existing-");
    public void Dispose() => _root.Delete(true);

    private string Touch(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(_root.FullName, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private SpeechToTextJobItem Item(string name, bool embedded = false)
    {
        var info = (FfmpegMediaInfo)RuntimeHelpers.GetUninitializedObject(typeof(FfmpegMediaInfo));
        info.Tracks = embedded ? [new FfmpegTrackInfo { TrackType = FfmpegTrackType.Subtitle }] : [];
        info.Duration = new TimeCode(1000);
        return new SpeechToTextJobItem(Touch(name), string.Empty, info);
    }

    [Theory]
    [InlineData("电影.srt")]
    [InlineData("电影.zh-Hans.SRT")]
    [InlineData("电影.en.forced.ass")]
    [InlineData("电影.mp4.srt")]
    [InlineData("电影.idx")]
    [InlineData("电影.sup")]
    public void MatchingSidecars_AreDetected(string subtitle)
    {
        var media = Touch("电影.mp4");
        Touch(subtitle);
        Assert.True(new SpeechToTextSubtitleDetection().HasExternalSubtitle(media));
    }

    [Theory]
    [InlineData("电影2.srt")]
    [InlineData("其他电影.zh.srt")]
    [InlineData("电影.xml")]
    [InlineData("电影.json")]
    [InlineData("电影.txt")]
    [InlineData("另一个目录/电影.srt")]
    [InlineData("剧集.srt")]
    public void UnrelatedFilesAndMetadata_DoNotCauseSkipping(string subtitle)
    {
        var media = Touch("电影.mp4");
        Touch(subtitle);
        Assert.False(new SpeechToTextSubtitleDetection().HasExternalSubtitle(media));
    }

    [Fact]
    public void EpisodeNames_AreNotTrimmedToSharedSeriesPrefix()
    {
        var media = Touch("Show.S01E02.mp4");
        Touch("Show.S01E01.srt");
        Touch("Show.srt");
        Assert.False(new SpeechToTextSubtitleDetection().HasExternalSubtitle(media));
    }

    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 1)]
    public void EmbeddedAndExternalChoices_AreIndependent(bool embedded, bool external, int expected)
    {
        var item = Item("movie.mkv", embedded: true);
        Touch("movie.en.srt");
        Assert.Equal((ExistingSubtitleKind)expected, new SpeechToTextSubtitleDetection().Detect(item, embedded, external));
    }

    [AvaloniaFact]
    public async Task AllFilesSkipped_ReturnsBeforeEngineDownloadAndPreservesSubtitle()
    {
        using var settings = new SettingsScope("Tools.AudioToText.SkipExternalSubtitles", "Tools.AudioToText.SkipEmbeddedSubtitles");
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new Nikse.SubtitleEdit.Logic.Media.FolderHelper());
        var window = new SpeechToTextWindow(vm);
        var item = Item("movie.mp4");
        var subtitle = Touch("movie.srt");
        vm.IsBatchMode = true;
        vm.BatchItems.Add(item);
        vm.SkipExternalSubtitles = true;
        try
        {
            await vm.TranscribeCommand.ExecuteAsync(null);
            Assert.Equal(1, vm.SkippedBatchCount);
            Assert.True(vm.IsTranscribeEnabled);
            Assert.Equal(Se.Language.Video.AudioToText.SkippedExternalSubtitles, item.Status);
            Assert.Equal("x", File.ReadAllText(subtitle));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task BatchPreflight_LeavesSkippedRowsWithReasons_AndRechecksNewSidecars()
    {
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new Nikse.SubtitleEdit.Logic.Media.FolderHelper());
        var window = new SpeechToTextWindow(vm);
        var embedded = Item("embedded.mkv", embedded: true);
        var external = Item("external.mp4");
        var plain = Item("plain.mp4");
        Touch("external.zh.srt");
        foreach (var item in new[] { embedded, external, plain }) vm.BatchItems.Add(item);
        vm.SkipEmbeddedSubtitles = true;
        vm.SkipExternalSubtitles = true;
        try
        {
            var eligible = await vm.PrepareBatchItemsAsync();
            Assert.Equal([plain], eligible);
            Assert.Equal(3, vm.BatchItems.Count);
            Assert.Equal(2, vm.SkippedBatchCount);
            Assert.Equal(Se.Language.Video.AudioToText.SkippedEmbeddedSubtitles, embedded.Status);
            Assert.Equal(Se.Language.Video.AudioToText.SkippedExternalSubtitles, external.Status);

            Touch("plain.srt"); // produced since the queue was loaded
            Assert.Empty((await vm.PrepareBatchItemsAsync())!);
            Assert.Equal(3, vm.SkippedBatchCount);

            vm.SkipEmbeddedSubtitles = false;
            vm.SkipExternalSubtitles = false;
            Assert.Equal(3, (await vm.PrepareBatchItemsAsync())!.Count);
            Assert.All(vm.BatchItems, item => Assert.Equal(string.Empty, item.Status));
            Assert.Equal(0, vm.SkippedBatchCount);
        }
        finally { window.Close(); }
    }
}
