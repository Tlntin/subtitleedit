using Avalonia.Threading;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

public partial class SpeechToTextViewModel
{
    internal async Task StopGptTranscriptionAfterCancellationAsync(Subtitle partial, string? recoveryRoot = null)
    {
        Subtitle copy;
        lock (partial.Paragraphs) copy = new Subtitle(partial);
        var originalVideo = _videoFileName;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_windowClosing) return;
            TranscribedSubtitle = copy;
            IsTranscribeEnabled = true;
            HideProgressBar();
            EstimatedText = string.Empty;
            BatchEstimatedText = string.Empty;
            ProgressText = Se.Language.Video.AudioToText.GptCancelled;
        });
        if (copy.Paragraphs.Count == 0) return;
        copy.Renumber();
        recoveryRoot ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Subtitle Edit", "SpeechToTextRecovery");
        var folder = Path.Combine(recoveryRoot, Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, (Path.GetFileNameWithoutExtension(originalVideo) ?? "transcription") + ".partial.srt");
            await File.WriteAllTextAsync(path, new SubRip().ToText(copy, string.Empty), new UTF8Encoding(false));
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_windowClosing) return;
                var message = string.Format(Se.Language.Video.AudioToText.GptCancelledPartial, copy.Paragraphs.Count, path);
                LogToConsole(message);
                if (_abort && !_windowClosing)
                {
                    LastBatchSubtitleFileName = path;
                    ProgressText = message;
                }
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogToConsole(string.Format(Se.Language.Video.AudioToText.GptPartialSaveFailed, ex.Message));
        }
    }

    private async Task RunOwnedOnlineTranscriptionAsync(IOnlineSttEngine engine, string audio, string? language, CancellationTokenSource owner)
    {
        try { await ProcessOnlineSttTranscription(engine, audio, language, owner.Token); }
        catch (Exception ex)
        {
            Se.WriteToolsLog($"Online transcription completion failed: {ex.GetType().Name}", true);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_windowClosing) return;
                IsTranscribeEnabled = true;
                HideProgressBar();
            });
        }
        finally { _onlineCancellation.Complete(owner); }
    }
}
