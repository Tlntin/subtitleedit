using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.Media;
using System.Runtime.CompilerServices;

namespace UITests.Features.Video.SpeechToText;

public sealed class SpeechToTextBatchSaveRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "se-save-recovery-" + Guid.NewGuid().ToString("N"));
    private const string Text = "1\n00:00:01,000 --> 00:00:02,000\n中文 subtitle\n";
    private string Target => Path.Combine(_root, "video.srt");
    private string Recovery => Path.Combine(_root, "recovery");
    public SpeechToTextBatchSaveRecoveryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private static IOException SmbError() => new("The user name or password is incorrect", unchecked((int)0x8007052e));
    private static Task<string> NoPicker() => throw new InvalidOperationException("Unexpected folder picker");

    [Fact]
    public async Task SmbAuthenticationFailure_PreservesExactTranscriptBeforePrompt_AndStops()
    {
        var saver = new SpeechToTextBatchSaveRecovery((path, text) =>
            path == Target ? Task.FromException(SmbError()) : File.WriteAllTextAsync(path, text));
        var result = await saver.SaveAsync(Text, _ => Target, null, Recovery, async failure =>
        {
            Assert.Equal(unchecked((int)0x8007052e), failure.Error.HResult);
            Assert.Equal(Target, failure.Target);
            Assert.Equal(Text, await File.ReadAllTextAsync(failure.RecoveryFile!));
            Assert.Null(failure.RecoveryError);
            return BatchSaveAction.Stop;
        }, NoPicker);
        Assert.Null(result.SavedFile);
        Assert.Equal(Text, await File.ReadAllTextAsync(result.RecoveryFile!));
    }

    [Fact]
    public async Task Retry_UsesCompletedText_AndRetainsBackup()
    {
        var attempts = 0;
        var saver = new SpeechToTextBatchSaveRecovery((path, text) =>
            path == Target && ++attempts == 1 ? Task.FromException(SmbError()) : File.WriteAllTextAsync(path, text));
        var result = await saver.SaveAsync(Text, _ => Target, null, Recovery,
            _ => Task.FromResult(BatchSaveAction.Retry), NoPicker);
        Assert.Equal(2, attempts);
        Assert.Equal(Target, result.SavedFile);
        Assert.Equal(Text, await File.ReadAllTextAsync(Target));
        Assert.Equal(Text, await File.ReadAllTextAsync(result.RecoveryFile!));
    }

    [Fact]
    public async Task MissingDestination_ChooseFolderSavesResult_AndReturnsFolderForRemainingJobs()
    {
        var localFolder = Path.Combine(_root, "local");
        Directory.CreateDirectory(localFolder);
        var result = await new SpeechToTextBatchSaveRecovery().SaveAsync(Text,
            folder => Path.Combine(folder ?? Path.Combine(_root, "missing-share"), "video.srt"), null, Recovery,
            _ => Task.FromResult(BatchSaveAction.ChooseFolder), () => Task.FromResult(localFolder));
        Assert.Equal(localFolder, result.OutputFolder);
        Assert.Equal(Text, await File.ReadAllTextAsync(result.SavedFile!));
    }

    [Fact]
    public async Task BackupAlsoFails_ErrorIsReported_AndNoSaveIsClaimed()
    {
        var saver = new SpeechToTextBatchSaveRecovery((_, _) => Task.FromException(new UnauthorizedAccessException("denied")));
        var result = await saver.SaveAsync(Text, _ => Target, null, Recovery, failure =>
        {
            Assert.Null(failure.RecoveryFile);
            Assert.IsType<UnauthorizedAccessException>(failure.RecoveryError);
            return Task.FromResult(BatchSaveAction.Stop);
        }, NoPicker);
        Assert.Null(result.SavedFile);
        Assert.Null(result.RecoveryFile);
    }

    [Fact]
    public async Task ExistingSubtitleAtWriteTime_IsNeverTruncatedOrDeleted()
    {
        await File.WriteAllTextAsync(Target, "existing subtitle");
        var result = await new SpeechToTextBatchSaveRecovery().SaveAsync(Text, _ => Target, null, Recovery,
            _ => Task.FromResult(BatchSaveAction.Stop), NoPicker);
        Assert.Null(result.SavedFile);
        Assert.Equal("existing subtitle", await File.ReadAllTextAsync(Target));
        Assert.Equal(Text, await File.ReadAllTextAsync(result.RecoveryFile!));
    }

    [Fact]
    public async Task CancelFolderPicker_ReturnsToChoices_WithSameBackup()
    {
        var calls = 0;
        var saver = new SpeechToTextBatchSaveRecovery((path, text) =>
            path == Target ? Task.FromException(SmbError()) : File.WriteAllTextAsync(path, text));
        var result = await saver.SaveAsync(Text, _ => Target, null, Recovery,
            _ => Task.FromResult(++calls == 1 ? BatchSaveAction.ChooseFolder : BatchSaveAction.Stop),
            () => Task.FromResult(string.Empty));
        Assert.Equal(2, calls);
        Assert.Single(Directory.GetFiles(Recovery, "*.srt", SearchOption.AllDirectories));
        Assert.NotNull(result.RecoveryFile);
    }

    [Fact]
    public async Task DestinationNameProbeFails_StillBacksUpAndAllowsChangingFolder()
    {
        var result = await new SpeechToTextBatchSaveRecovery().SaveAsync(Text,
            folder => folder == null ? throw SmbError() : Target, null, Recovery,
            _ => Task.FromResult(BatchSaveAction.ChooseFolder), () => Task.FromResult(_root));
        Assert.Equal(Target, result.SavedFile);
        Assert.Equal(Text, await File.ReadAllTextAsync(result.RecoveryFile!));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAfterSaveFailure_RestoresControls_AndKeepsEngineFiles(bool backupFails)
    {
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        var engineOutput = Path.Combine(_root, "engine-output.srt");
        await File.WriteAllTextAsync(engineOutput, Text);
        try
        {
            vm.IsBatchMode = true;
            vm.IsTranscribeEnabled = false;
            vm.BatchEstimatedText = "old estimate";
            var item = Item("video");
            var saver = new SpeechToTextBatchSaveRecovery((path, text) =>
                path == Target || backupFails ? Task.FromException(SmbError()) : File.WriteAllTextAsync(path, text));
            var saved = await vm.SaveBatchItemAsync(item, Subtitle(), saver,
                _ => Task.FromResult(BatchSaveAction.Stop), NoPicker, Recovery);
            Assert.False(saved);
            Assert.True(vm.IsTranscribeEnabled);
            Assert.NotEqual(Se.Language.General.Converted, item.Status);
            Assert.Empty(vm.BatchEstimatedText);
            Assert.Equal(0, vm.ProgressOpacity);
            Assert.Equal(!backupFails, File.Exists(item.OutputSubtitleFileName));
            Assert.Single(vm.TranscribedSubtitle.Paragraphs);
            Assert.Equal(Text, await File.ReadAllTextAsync(engineOutput));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CancelDuringSave_RestoresControlsAfterWrite_AndDoesNotAdvanceBatch()
    {
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            vm.IsBatchMode = true;
            vm.IsTranscribeEnabled = false;
            var item = Item("video");
            var saver = new SpeechToTextBatchSaveRecovery(async (path, text) =>
            {
                entered.SetResult();
                await release.Task;
                await File.WriteAllTextAsync(path, text);
            });
            var saving = vm.SaveBatchItemAsync(item, Subtitle(), saver,
                _ => throw new InvalidOperationException("Unexpected save failure"), NoPicker, Recovery);
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            vm.CancelCommand.Execute(null);
            release.SetResult();
            Assert.False(await saving); // Saved successfully, but cancellation prevents starting another file.
            Assert.True(vm.IsTranscribeEnabled);
            Assert.Equal(Se.Language.General.Converted, item.Status);
            Assert.True(File.Exists(item.OutputSubtitleFileName));
        }
        finally { release.TrySetResult(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task AlternateFolder_IsUsedForNextItem_WithoutRetranscribing()
    {
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        var localFolder = Path.Combine(_root, "chosen");
        Directory.CreateDirectory(localFolder);
        var prompts = 0;
        try
        {
            var first = Item("video");
            var saver = new SpeechToTextBatchSaveRecovery((path, text) =>
                path == Target ? Task.FromException(SmbError()) : File.WriteAllTextAsync(path, text));
            Assert.True(await vm.SaveBatchItemAsync(first, Subtitle(), saver,
                _ => { prompts++; return Task.FromResult(BatchSaveAction.ChooseFolder); },
                () => Task.FromResult(localFolder), Recovery));
            var next = Item("second");
            Assert.True(await vm.SaveBatchItemAsync(next, Subtitle(), saver,
                _ => throw new InvalidOperationException("Output folder was not retained"), NoPicker, Recovery));
            Assert.Equal(1, prompts);
            Assert.Equal(Path.Combine(localFolder, "second.srt"), next.OutputSubtitleFileName);
            Assert.Equal(Se.Language.General.Converted, next.Status);
        }
        finally { window.Close(); }
    }

    private SpeechToTextJobItem Item(string name)
    {
        var video = Path.Combine(_root, name + ".mkv");
        File.WriteAllText(video, "test media");
        return new SpeechToTextJobItem(video, string.Empty,
            (FfmpegMediaInfo)RuntimeHelpers.GetUninitializedObject(typeof(FfmpegMediaInfo)));
    }
    private static Subtitle Subtitle()
    {
        var result = new Subtitle();
        result.Paragraphs.Add(new Paragraph("中文 subtitle", 1000, 2000));
        return result;
    }
}
