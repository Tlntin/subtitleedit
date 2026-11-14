using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText;

internal enum BatchSaveAction { Retry, ChooseFolder, Stop }
internal sealed record BatchSaveFailure(string Target, Exception Error, string? RecoveryFile, Exception? RecoveryError);
internal sealed record BatchSaveResult(string? SavedFile, string? RecoveryFile, string? OutputFolder);

/// <summary>Retains a completed transcript while an unavailable destination is repaired or changed.</summary>
internal sealed class SpeechToTextBatchSaveRecovery
{
    private readonly Func<string, string, Task> _write;
    internal SpeechToTextBatchSaveRecovery(Func<string, string, Task>? write = null) => _write = write ?? WriteNewFileAsync;

    internal async Task<BatchSaveResult> SaveAsync(string text, Func<string?, string> getTarget,
        string? outputFolder, string recoveryFolder, Func<BatchSaveFailure, Task<BatchSaveAction>> ask,
        Func<Task<string>> chooseFolder)
    {
        string? recovery = null;
        Exception? recoveryError = null;
        while (true)
        {
            var target = string.Empty;
            try
            {
                // Both name probing and opening SMB files can block. Never run them on the UI thread.
                await Task.Run(async () =>
                {
                    target = getTarget(outputFolder);
                    await _write(target, text).ConfigureAwait(false);
                });
                // Retain any recovery file even after a successful retry; it is the user's backup.
                return new BatchSaveResult(target, recovery, outputFolder);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                if (recovery == null)
                {
                    try
                    {
                        var localFile = Path.Combine(recoveryFolder, Guid.NewGuid().ToString("N"),
                            string.IsNullOrEmpty(target) ? "transcription.srt" : Path.GetFileName(target));
                        await Task.Run(async () =>
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
                            await _write(localFile, text).ConfigureAwait(false);
                        });
                        recovery = localFile;
                        recoveryError = null;
                    }
                    catch (Exception backupEx) when (IsFileError(backupEx)) { recoveryError = backupEx; }
                }

                var action = await ask(new BatchSaveFailure(target, ex, recovery, recoveryError));
                if (action == BatchSaveAction.Retry) continue;
                if (action == BatchSaveAction.ChooseFolder)
                {
                    var selected = await chooseFolder();
                    if (!string.IsNullOrWhiteSpace(selected)) outputFolder = selected;
                    // Cancelling the folder picker returns to the recovery choices.
                    continue;
                }
                return new BatchSaveResult(null, recovery, outputFolder);
            }
        }
    }

    private static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException;

    private static async Task WriteNewFileAsync(string file, string text)
    {
        var created = false;
        try
        {
            // Name probing is advisory; CreateNew also protects against another process winning the race.
            await using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous);
            created = true;
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(text).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
            // Delete only a partial file created by this attempt, never a pre-existing subtitle.
            if (created)
            {
                try { File.Delete(file); } catch (Exception ex) when (IsFileError(ex)) { }
            }
            throw;
        }
    }
}
