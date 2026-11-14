using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.AudioToText;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class GptConnectionTestViewModelTests
{
    private static SpeechToTextViewModel Create()
    {
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        vm.SelectedEngine = vm.Engines.Single(e => e.Choice == WhisperChoice.GptTranscribe);
        vm.IsGptTranscribeVisible = true;
        vm.GptTranscribeUrl = "https://current-input.test/v1/audio/transcriptions";
        vm.GptTranscribeApiKey = "current-key";
        vm.SelectedGptTranscribeModel = GptTranscribeModels.Find("gpt-4o-mini-transcribe");
        vm.GptTranscribeExtraHeaders = "X-Test: current-header";
        return vm;
    }

    [AvaloniaFact]
    public async Task TestsUnsavedInputs_WithoutPersistingOrStartingTranscription_AndRejectsDuplicateClicks()
    {
        using var scope = new SettingsScope("Tools.AudioToText.WhisperChoice", "Tools.GptTranscribeUrl", "Tools.GptTranscribeApiKey");
        var savedUrl = Se.Settings.Tools.GptTranscribeUrl;
        var savedKey = Se.Settings.Tools.GptTranscribeApiKey;
        var vm = Create();
        var window = new SpeechToTextWindow(vm);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var running = vm.RunGptConnectionTestAsync(async (settings, token) =>
            {
                Assert.Equal("https://current-input.test/v1/audio/transcriptions", settings.EndpointUrl);
                Assert.Equal("current-key", settings.ApiKey);
                Assert.Equal("gpt-4o-mini-transcribe", settings.Model);
                Assert.Equal("X-Test: current-header", settings.ExtraHeaders);
                Assert.Equal(30, settings.TimeoutSeconds);
                Assert.Null(settings.Logger);
                entered.SetResult();
                await release.Task.WaitAsync(token);
                return new GptConnectionTestResult(true, "Passed");
            });
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.True(vm.IsTestingGptConnection);
            Assert.False(vm.CanEditBatch);
            Assert.False(vm.CanTestGptConnection);
            Assert.False(vm.IsGptConfigurationEnabled);
            await vm.RunGptConnectionTestAsync((_, _) => throw new Exception("Duplicate click sent another request"));
            release.SetResult();
            await running;
            Assert.Equal("Passed", vm.GptConnectionTestStatus);
            Assert.True(vm.CanTestGptConnection);
            Assert.Equal(savedUrl, Se.Settings.Tools.GptTranscribeUrl);
            Assert.Equal(savedKey, Se.Settings.Tools.GptTranscribeApiKey);
            Assert.Empty(vm.TranscribedSubtitle.Paragraphs);
            vm.GptTranscribeUrl = "https://changed.test/v1/audio/transcriptions";
            Assert.Empty(vm.GptConnectionTestStatus);
        }
        finally { release.TrySetResult(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task MainCancel_CancelsConnectionTest_AndRestoresControls()
    {
        var vm = Create();
        var window = new SpeechToTextWindow(vm);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var running = vm.RunGptConnectionTestAsync(async (_, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new Exception("Should be cancelled");
            });
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            vm.CancelCommand.Execute(null);
            await running;
            Assert.False(vm.IsTestingGptConnection);
            Assert.True(vm.CanEditBatch);
            Assert.Equal(Se.Language.Video.AudioToText.GptConnectionCancelled, vm.GptConnectionTestStatus);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task BadConfiguration_FailsBeforeAnyNetworkRequest()
    {
        var vm = Create();
        var window = new SpeechToTextWindow(vm);
        try
        {
            vm.GptTranscribeApiKey = "";
            await vm.RunGptConnectionTestAsync((_, _) => throw new Exception("Unexpected request"));
            Assert.Equal(Se.Language.Video.AudioToText.GptApiKeyRequired, vm.GptConnectionTestStatus);
            vm.GptTranscribeApiKey = "key";
            vm.GptTranscribeUrl = "invalid";
            await vm.RunGptConnectionTestAsync((_, _) => throw new Exception("Unexpected request"));
            Assert.Equal(Se.Language.Video.AudioToText.GptConfigurationRequired, vm.GptConnectionTestStatus);
            Assert.True(vm.CanEditBatch);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task UnexpectedFailure_IsSafe_AndRestoresControls()
    {
        var vm = Create();
        var window = new SpeechToTextWindow(vm);
        try
        {
            await vm.RunGptConnectionTestAsync((_, _) => throw new Exception("private-key-in-exception"));
            Assert.DoesNotContain("private-key-in-exception", vm.GptConnectionTestStatus);
            Assert.True(vm.CanEditBatch);
            Assert.False(vm.IsTestingGptConnection);
        }
        finally { window.Close(); }
    }
}
