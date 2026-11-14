using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.AudioToText;
using System.Text.Json;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class GptConnectionTestViewModelTests : IDisposable
{
    private readonly Se _originalSettings = Se.Settings;
    public GptConnectionTestViewModelTests()
    {
        // Window initialization also persists engine selections. Give each test a deep copy
        // so selecting GPT cannot change the next test's default local engine.
        Se.Settings = JsonSerializer.Deserialize(JsonSerializer.Serialize(_originalSettings, SeJsonContext.Default.Se), SeJsonContext.Default.Se)!;
    }
    public void Dispose()
    {
        Se.Settings = _originalSettings;
        Se.UpdateLibSeSettings();
    }
    [AvaloniaFact]
    public void SaveSettings_PersistsKeyEndpointAndModel_AndReopensWithoutRetranscribing()
    {
        var previous = Se.Settings;
        var previousFile = Se.SettingsFilePathOverride;
        var folder = Directory.CreateTempSubdirectory("se-gpt-save-test-");
        SpeechToTextWindow? window = null;
        try
        {
            Se.Settings = new Se();
            Se.SettingsFilePathOverride = Path.Combine(folder.FullName, "Settings.json");
            var vm = Create();
            window = new SpeechToTextWindow(vm);
            vm.GptTranscribeLanguage = "zh";
            vm.GptTranscribePrompt = "Vocabulary";
            vm.GptTranscribeConcurrency = 3;
            vm.SaveGptSettingsCommand.Execute(null);
            Assert.Equal(Se.Language.Video.AudioToText.GptSettingsSaved, vm.GptSettingsSaveStatus);
            using (var stream = File.OpenRead(Se.SettingsFilePathOverride))
            {
                var saved = JsonSerializer.Deserialize(stream, SeJsonContext.Default.Se)!;
                Assert.Equal("current-key", saved.Tools.GptTranscribeApiKey);
                Assert.Equal("https://current-input.test/v1/audio/transcriptions", saved.Tools.GptTranscribeUrl);
                Assert.Equal("gpt-4o-mini-transcribe", saved.Tools.GptTranscribeModel);
                Assert.Equal("zh", saved.Tools.GptTranscribeLanguage);
                Assert.Equal(3, saved.Tools.GptTranscribeConcurrency);
            }
            window.Close(); window = null;
            Se.Settings = new Se();
            Se.LoadSettings(Se.SettingsFilePathOverride);
            var reopened = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
            window = new SpeechToTextWindow(reopened);
            Assert.Equal("current-key", reopened.GptTranscribeApiKey);
            Assert.Equal("gpt-4o-mini-transcribe", reopened.SelectedGptTranscribeModel!.Id);
            Assert.Equal("Vocabulary", reopened.GptTranscribePrompt);
            Assert.Equal(string.Empty, reopened.GptSettingsSaveStatus);
            reopened.GptTranscribeApiKey = "edited-key";
            Assert.Equal(Se.Language.Video.AudioToText.GptSettingsUnsaved, reopened.GptSettingsSaveStatus);
            Assert.True(reopened.CanEditBatch);
        }
        finally
        {
            window?.Close();
            Se.Settings = previous;
            Se.SettingsFilePathOverride = previousFile;
            Se.UpdateLibSeSettings();
            folder.Delete(true);
        }
    }

    [AvaloniaFact]
    public void SaveSettings_WriteFailure_DoesNotClaimSuccess()
    {
        var previous = Se.Settings;
        var previousFile = Se.SettingsFilePathOverride;
        var folder = Directory.CreateTempSubdirectory("se-gpt-save-fail-test-");
        SpeechToTextWindow? window = null;
        try
        {
            Se.Settings = new Se();
            var blocked = Path.Combine(folder.FullName, "config-is-a-directory");
            Directory.CreateDirectory(blocked);
            Se.SettingsFilePathOverride = Path.Combine(folder.FullName, "Settings.json");
            var vm = Create();
            window = new SpeechToTextWindow(vm);
            Se.SettingsFilePathOverride = blocked;
            vm.SaveGptSettingsCommand.Execute(null);
            Assert.Equal(Se.Language.Video.AudioToText.GptSettingsSaveFailed, vm.GptSettingsSaveStatus);
            Assert.True(vm.CanEditBatch);
        }
        finally
        {
            window?.Close();
            Se.Settings = previous;
            Se.SettingsFilePathOverride = previousFile;
            Se.UpdateLibSeSettings();
            folder.Delete(true);
        }
    }

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
