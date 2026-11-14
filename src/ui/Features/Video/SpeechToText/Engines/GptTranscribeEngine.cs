using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.UiLogic.AudioToText;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;

public sealed class GptTranscribeEngine : IOnlineSttEngine
{
    public string Name => "OpenAI GPT Transcribe";
    public string Choice => WhisperChoice.GptTranscribe;
    public string Url => "https://developers.openai.com/api/docs/guides/speech-to-text";
    public List<WhisperLanguage> Languages => new();
    public List<WhisperModel> Models => new();
    public string Extension => string.Empty;
    public string UnpackSkipFolder => string.Empty;
    public string CommandLineParameter { get; set; } = string.Empty;
    public string ProbeUrl => Se.Settings.Tools.GptTranscribeUrl;
    private bool NativeTiming => GptTranscribeModels.Find(Se.Settings.Tools.GptTranscribeModel)?.HasNativeTimestamps == true;
    public long UploadThresholdBytes => NativeTiming ? OpenAiSttChunker.DefaultThresholdBytes : long.MaxValue;
    public long ChunkSizeBytes => OpenAiSttChunker.DefaultChunkSizeBytes;
    public double MaxChunkSeconds => NativeTiming ? 1200 : 0;

    public ISttTranscriber? CreateTranscriber(out string? configErrorMessage)
    {
        var tools = Se.Settings.Tools;
        var model = GptTranscribeModels.Find(tools.GptTranscribeModel);
        configErrorMessage = null;
        if (model == null || !Uri.TryCreate(tools.GptTranscribeUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            configErrorMessage = Se.Language.Video.AudioToText.GptConfigurationRequired;
            return null;
        }
        if (string.IsNullOrWhiteSpace(tools.GptTranscribeApiKey))
        {
            configErrorMessage = Se.Language.Video.AudioToText.GptApiKeyRequired;
            return null;
        }
        return new GptTranscribeService(new OpenAiCompatibleSettings
        {
            EndpointUrl = tools.GptTranscribeUrl, ApiKey = tools.GptTranscribeApiKey,
            Model = model.Id, RequestProfile = model.Profile, Language = tools.GptTranscribeLanguage,
            Prompt = tools.GptTranscribePrompt, TimeoutSeconds = tools.GptTranscribeTimeoutSeconds,
            ExtraHeaders = tools.GptTranscribeExtraHeaders, Stream = false, AudioFormat = "wav",
            Logger = message => Se.WriteToolsLog(message, true),
        }, tools.GptTranscribeConcurrency, useLocalAlignment: tools.GptTranscribeUseLocalAlignment);
    }

    public bool IsEngineInstalled() => true;
    public bool CanBeDownloaded() => false;
    public bool IsModelInstalled(WhisperModel model) => true;
    public string GetAndCreateWhisperFolder() => string.Empty;
    public string GetAndCreateWhisperModelFolder(WhisperModel? model) => string.Empty;
    public string GetExecutable() => string.Empty;
    public string GetModelForCmdLine(string modelName) => modelName;
    public Task<string> GetHelpText() => Task.FromResult(string.Empty);
    public string GetWhisperModelDownloadFileName(WhisperModel model, string url) => string.Empty;
    public override string ToString() => Name;
}
