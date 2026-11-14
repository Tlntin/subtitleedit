using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.AudioToText;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class GptTranscribeEngineTests
{
    private static SettingsScope Scope() => new("Tools.AudioToText.WhisperChoice", "Tools.GptTranscribeSettingsInitialized",
        "Tools.GptTranscribeModel", "Tools.GptTranscribeUrl", "Tools.GptTranscribeApiKey", "Tools.GptTranscribeLanguage",
        "Tools.OpenAiCompatibleSttModel", "Tools.OpenAiCompatibleSttUrl", "Tools.OpenAiCompatibleSttApiKey",
        "Tools.OpenAiCompatibleSttLanguage", "Tools.OpenAiCompatibleSttPrompt", "Tools.GptTranscribePrompt");

    [AvaloniaFact]
    public void GptSelection_ShowsFiveModelsAndSwitchesTimestampCapability()
    {
        using var scope = Scope();
        Se.Settings.Tools.AudioToText.WhisperChoice = WhisperChoice.GptTranscribe;
        Se.Settings.Tools.GptTranscribeSettingsInitialized = true;
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        try
        {
            Assert.IsType<GptTranscribeEngine>(vm.SelectedEngine);
            Assert.True(vm.IsGptTranscribeVisible);
            Assert.False(vm.IsOpenAiCompatibleSttVisible);
            Assert.False(vm.IsModelDownloadVisible);
            Assert.Equal(5, vm.GptModels.Count);
            vm.SelectedGptTranscribeModel = GptTranscribeModels.Find("gpt-transcribe");
            Assert.True(vm.GptPromptAvailable);
            Assert.Equal(Se.Language.Video.AudioToText.GptVadTimingHint, vm.GptTimingHint);
            vm.SelectedGptTranscribeModel = GptTranscribeModels.Find("gpt-4o-transcribe-diarize");
            Assert.False(vm.GptPromptAvailable);
            Assert.Equal(Se.Language.Video.AudioToText.GptNativeTimingHint, vm.GptTimingHint);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Migration_KeepsConfiguredEndpointAndKeyTogether()
    {
        using var scope = Scope();
        Se.Settings.Tools.GptTranscribeSettingsInitialized = false;
        Se.Settings.Tools.GptTranscribeApiKey = "";
        Se.Settings.Tools.OpenAiCompatibleSttModel = "gpt-transcribe";
        Se.Settings.Tools.OpenAiCompatibleSttUrl = "https://gateway.example/v1/audio/transcriptions";
        Se.Settings.Tools.OpenAiCompatibleSttApiKey = "gateway-test-key";
        Se.Settings.Tools.OpenAiCompatibleSttLanguage = "zh";
        Se.Settings.Tools.OpenAiCompatibleSttPrompt = "Vocabulary";
        var vm = new SpeechToTextViewModel(new StubWindowService(), new StubFileHelper(), new FolderHelper());
        var window = new SpeechToTextWindow(vm);
        try
        {
            Assert.Equal(Se.Settings.Tools.OpenAiCompatibleSttUrl, vm.GptTranscribeUrl);
            Assert.Equal("gateway-test-key", vm.GptTranscribeApiKey);
            Assert.Equal("zh", vm.GptTranscribeLanguage);
            Assert.Equal("Vocabulary", vm.GptTranscribePrompt);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void VadModelProcessesLocallyBeforeUpload_WhileNativeModelKeepsProviderLimits()
    {
        using var scope = Scope();
        var engine = new GptTranscribeEngine();
        Se.Settings.Tools.GptTranscribeModel = "gpt-transcribe";
        Assert.Equal(long.MaxValue, engine.UploadThresholdBytes);
        Assert.Equal(0, engine.MaxChunkSeconds);
        Se.Settings.Tools.GptTranscribeModel = "gpt-4o-transcribe-diarize";
        Assert.True(engine.UploadThresholdBytes < 25L * 1024 * 1024);
        Assert.Equal(1200, engine.MaxChunkSeconds);
    }

    [Fact]
    public void MissingKey_ProducesConfigurationMessage()
    {
        using var scope = Scope();
        Se.Settings.Tools.GptTranscribeModel = "gpt-transcribe";
        Se.Settings.Tools.GptTranscribeUrl = "https://example.test/v1/audio/transcriptions";
        Se.Settings.Tools.GptTranscribeApiKey = "";
        Assert.Null(new GptTranscribeEngine().CreateTranscriber(out var message));
        Assert.Equal(Se.Language.Video.AudioToText.GptApiKeyRequired, message);
    }
}
