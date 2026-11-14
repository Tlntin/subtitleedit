using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

public partial class SpeechToTextViewModel
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanTestGptConnection)),
        NotifyPropertyChangedFor(nameof(CanEditBatch)), NotifyPropertyChangedFor(nameof(IsGptConfigurationEnabled))]
    private bool _isTestingGptConnection;
    [ObservableProperty] private string _gptConnectionTestStatus = string.Empty;
    private CancellationTokenSource? _gptConnectionTestCts;
    public bool IsGptConfigurationEnabled => IsTranscribeEnabled && !IsTestingGptConnection;
    public bool CanTestGptConnection => CanEditBatch && IsGptTranscribeVisible;

    partial void OnGptTranscribeUrlChanged(string value) => ClearGptConnectionStatus();
    partial void OnGptTranscribeApiKeyChanged(string value) => ClearGptConnectionStatus();
    partial void OnGptTranscribeExtraHeadersChanged(string value) => ClearGptConnectionStatus();
    partial void OnSelectedGptTranscribeModelChanged(GptTranscribeModel? value) => ClearGptConnectionStatus();
    private void ClearGptConnectionStatus()
    {
        if (!IsTestingGptConnection) GptConnectionTestStatus = string.Empty;
    }

    [RelayCommand]
    private Task TestGptConnection() => RunGptConnectionTestAsync();
    [RelayCommand]
    private void CancelGptConnectionTest() => _gptConnectionTestCts?.Cancel();

    internal async Task RunGptConnectionTestAsync(
        Func<OpenAiCompatibleSettings, CancellationToken, Task<GptConnectionTestResult>>? test = null)
    {
        if (!CanTestGptConnection) return;
        var language = Se.Language.Video.AudioToText;
        if (SelectedGptTranscribeModel == null || !Uri.TryCreate(GptTranscribeUrl.Trim(), UriKind.Absolute, out var url) ||
            url.Scheme is not ("http" or "https"))
        {
            GptConnectionTestStatus = language.GptConfigurationRequired;
            return;
        }
        if (string.IsNullOrWhiteSpace(GptTranscribeApiKey))
        {
            GptConnectionTestStatus = language.GptApiKeyRequired;
            return;
        }
        // Snapshot the current controls. Testing neither saves settings nor starts a batch.
        var settings = new OpenAiCompatibleSettings
        {
            EndpointUrl = url.AbsoluteUri, ApiKey = GptTranscribeApiKey.Trim(),
            Model = SelectedGptTranscribeModel.Id, RequestProfile = SelectedGptTranscribeModel.Profile,
            ExtraHeaders = GptTranscribeExtraHeaders, TimeoutSeconds = 30, AudioFormat = "wav", Language = "en",
        };
        using var cts = new CancellationTokenSource();
        _gptConnectionTestCts = cts;
        IsTestingGptConnection = true;
        GptConnectionTestStatus = language.GptConnectionTesting;
        try
        {
            var result = await (test ?? new GptConnectionTestService().TestAsync)(settings, cts.Token);
            GptConnectionTestStatus = result.Message;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            GptConnectionTestStatus = language.GptConnectionCancelled;
        }
        catch (Exception)
        {
            // Do not display arbitrary exception text which could contain a key or header value.
            GptConnectionTestStatus = string.Format(language.GptConnectionFailed, language.GptConnectionRequestError);
        }
        finally
        {
            _gptConnectionTestCts = null;
            IsTestingGptConnection = false;
        }
    }
}
