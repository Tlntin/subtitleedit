using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Logic.Media;
using System.Reflection;
using Nikse.SubtitleEdit.Core.Common;

namespace UITests.Features.Video.SpeechToText;

public class SpeechToTextOnlineCancellationTests
{
    [Fact]
    public void CompletingPreviousBatchItem_DoesNotClearCurrentItemsCancelSource()
    {
        var manager = new SpeechToTextOnlineCancellation();
        var previous = manager.Begin();
        var current = manager.Begin();
        var token = current.Token;
        manager.Complete(previous);
        manager.Cancel();
        Assert.True(token.IsCancellationRequested);
        manager.Complete(current);
        manager.Cancel(); // A completed/disposed source is never cancelled again.
    }

    [Fact]
    public void ProgressDataIsCollectedBeforeReportReturns_EvenWithAQueuedSynchronizationContext()
    {
        var original = SynchronizationContext.Current;
        var context = new QueuingContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var collected = new List<int>();
            var progress = new SpeechToTextSynchronousProgress<int>(collected.Add);
            for (var i = 0; i < 100; i++) progress.Report(i);
            Assert.Equal(Enumerable.Range(0, 100), collected);
            Assert.Equal(0, context.Posts);
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); }
    }

    [AvaloniaFact]
    public async Task ConcurrentLogReports_DoNotLoseLines()
    {
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        var method = typeof(SpeechToTextViewModel).GetMethod("LogToConsole", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            vm.ConsoleLog = string.Empty;
            await Task.WhenAll(Enumerable.Range(0, 100).Select(i => Task.Run(() => method.Invoke(vm, [$"unique-line-{i}", false]))));
            Dispatcher.UIThread.RunJobs();
            var lines = vm.ConsoleLog.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(100, lines.Length);
            Assert.Equal(100, lines.Distinct().Count());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CancelledPartialSubtitle_RestoresControlsWithoutPostProcessing_AndSavesLocally()
    {
        var root = Directory.CreateTempSubdirectory("se-cancel-partial-test-");
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        try
        {
            vm.IsTranscribeEnabled = false;
            vm.DoAdjustTimings = true;
            var partial = new Subtitle();
            partial.Paragraphs.Add(new Paragraph("Retain completed transcript", 1000, 2000));
            await vm.StopGptTranscriptionAfterCancellationAsync(partial, root.FullName);
            Assert.True(vm.IsTranscribeEnabled);
            Assert.Equal(0, vm.ProgressOpacity);
            Assert.Single(vm.TranscribedSubtitle.Paragraphs);
            var file = Assert.Single(Directory.GetFiles(root.FullName, "*.srt", SearchOption.AllDirectories));
            Assert.Contains("Retain completed transcript", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        }
        finally { window.Close(); root.Delete(true); }
    }

    [AvaloniaFact]
    public void MissingAligner_DoesNotUsePostProcessingToInventSentenceTimes()
    {
        using var settings = new SettingsScope("Tools.AudioToText.WhisperChoice");
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        try
        {
            vm.SelectedEngine = vm.Engines.Single(e => e is Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines.GptTranscribeEngine);
            typeof(SpeechToTextViewModel).GetField("_gptChunkLevelTimings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(vm, true);
            vm.DoPostProcessing = true;
            vm.DoAdjustTimings = true;
            var subtitle = new Subtitle();
            subtitle.Paragraphs.Add(new Paragraph("A long sentence. Another sentence. Keep all text at its known chunk time.", 0, 24000));
            var actual = (Subtitle)typeof(SpeechToTextViewModel).GetMethod("PostProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, [subtitle])!;
            Assert.Same(subtitle, actual);
            Assert.Single(actual.Paragraphs);
            Assert.Equal(24000, actual.Paragraphs[0].EndTime.TotalMilliseconds);
        }
        finally { window.Close(); }
    }

    private sealed class QueuingContext : SynchronizationContext
    {
        internal int Posts;
        public override void Post(SendOrPostCallback callback, object? state) => Posts++;
    }
}
