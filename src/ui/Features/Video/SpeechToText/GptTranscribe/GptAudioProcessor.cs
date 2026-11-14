using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal interface IGptAudioProcessor
{
    Task DecodeAsync(string audioFile, string pcmFile, CancellationToken token);
    IReadOnlyList<SpeechInterval> Detect(string pcmFile, CancellationToken token);
}

internal sealed class GptAudioProcessor(string ffmpeg) : IGptAudioProcessor
{
    public async Task DecodeAsync(string audioFile, string pcmFile, CancellationToken token)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(string.IsNullOrWhiteSpace(ffmpeg) ? "ffmpeg" : ffmpeg)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true,
            },
        };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-i", audioFile,
                     "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", "-f", "s16le", pcmFile })
            process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var error = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        if (process.ExitCode != 0) throw new InvalidOperationException("Unable to decode audio for VAD: " + await error.ConfigureAwait(false));
    }

    public IReadOnlyList<SpeechInterval> Detect(string pcmFile, CancellationToken token) => SileroVadSpeechDetector.Detect(pcmFile, token);

    internal static void WriteClip(string pcmFile, string waveFile, SpeechInterval interval, CancellationToken token)
    {
        using var input = File.OpenRead(pcmFile);
        var firstByte = Math.Clamp((long)Math.Floor(interval.Start * 16000) * 2, 0, input.Length);
        var endByte = Math.Clamp((long)Math.Ceiling(interval.End * 16000) * 2, firstByte, input.Length);
        var length = checked((int)(endByte - firstByte));
        using var output = File.Create(waveFile);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
        writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(length);
        input.Position = firstByte;
        var buffer = new byte[32768];
        while (length > 0)
        {
            token.ThrowIfCancellationRequested();
            var count = Math.Min(length, buffer.Length);
            input.ReadExactly(buffer.AsSpan(0, count));
            output.Write(buffer, 0, count);
            length -= count;
        }
    }

    internal static bool HasSignal(string pcmFile, SpeechInterval interval, CancellationToken token)
    {
        using var input = File.OpenRead(pcmFile);
        input.Position = Math.Clamp((long)(interval.Start * 16000) * 2, 0, input.Length);
        var end = Math.Clamp((long)Math.Ceiling(interval.End * 16000) * 2, input.Position, input.Length);
        var buffer = new byte[32768];
        while (input.Position < end)
        {
            token.ThrowIfCancellationRequested();
            var count = (int)Math.Min(buffer.Length, end - input.Position);
            input.ReadExactly(buffer.AsSpan(0, count));
            // Only exact digital silence is safe to skip. Quiet/low-confidence speech is uploaded.
            if (buffer.AsSpan(0, count).IndexOfAnyExcept((byte)0) >= 0) return true;
        }
        return false;
    }
}
