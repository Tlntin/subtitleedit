using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

internal readonly record struct SpeechInterval(double Start, double End);

internal static class SileroVadSpeechDetector
{
    internal const int SampleRate = 16000;
    private const int FrameSamples = 512;
    private const int ContextSamples = 64;
    private static readonly Lazy<byte[]> Model = new(() =>
    {
        using var stream = typeof(SileroVadSpeechDetector).Assembly.GetManifestResourceStream("SileroVad.onnx")
            ?? throw new InvalidOperationException("Embedded Silero VAD model is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    });

    // PCM is streamed from disk: an hour-long recording never becomes a float[] in memory.
    internal static IReadOnlyList<SpeechInterval> Detect(string pcmFile, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var pcm = File.OpenRead(pcmFile);
        if (pcm.Length == 0) return Array.Empty<SpeechInterval>();
        using var options = new SessionOptions { InterOpNumThreads = 1, IntraOpNumThreads = 1, LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR };
        using var session = new InferenceSession(Model.Value, options);
        var input = new float[FrameSamples + ContextSamples];
        var state = new float[256];
        var buffer = new byte[FrameSamples * 2];
        var probabilities = new List<float>();
        var rate = NamedOnnxValue.CreateFromTensor("sr", new DenseTensor<long>(new long[] { SampleRate }, Array.Empty<int>()));
        while (pcm.Position < pcm.Length)
        {
            token.ThrowIfCancellationRequested();
            var bytes = (int)Math.Min(buffer.Length, pcm.Length - pcm.Position);
            pcm.ReadExactly(buffer.AsSpan(0, bytes));
            Array.Clear(input, ContextSamples, FrameSamples);
            for (var i = 0; i < bytes / 2; i++)
                input[ContextSamples + i] = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(i * 2, 2)) / 32768f;
            var inputs = new[]
            {
                NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(input, new[] { 1, input.Length })),
                NamedOnnxValue.CreateFromTensor("state", new DenseTensor<float>(state, new[] { 2, 1, 128 })),
                rate,
            };
            using var outputs = session.Run(inputs);
            probabilities.Add(outputs.First().AsTensor<float>().First());
            state = outputs.ElementAt(1).AsTensor<float>().ToArray();
            Array.Copy(input, input.Length - ContextSamples, input, 0, ContextSamples);
        }

        return PlanSegments(probabilities, pcm.Length / 2.0 / SampleRate);
    }

    internal static IReadOnlyList<SpeechInterval> PlanSegments(IReadOnlyList<float> probabilities, double duration,
        double maxSpeechSeconds = 12, double minSpeechSeconds = 0.20, double minSilenceSeconds = 0.30, double paddingSeconds = 0.12)
    {
        const double step = (double)FrameSamples / SampleRate;
        if (!double.IsFinite(maxSpeechSeconds) || maxSpeechSeconds < 2)
            throw new ArgumentOutOfRangeException(nameof(maxSpeechSeconds));
        var raw = new List<SpeechInterval>();
        var start = -1;
        var silence = -1;
        for (var i = 0; i < probabilities.Count; i++)
        {
            if (start < 0)
            {
                if (probabilities[i] >= 0.5f) { start = i; silence = -1; }
                continue;
            }
            if (probabilities[i] >= 0.5f) silence = -1;
            else if (probabilities[i] < 0.35f && silence < 0) silence = i;
            if (silence >= 0 && (i - silence) * step >= minSilenceSeconds)
            {
                if ((silence - start) * step >= minSpeechSeconds)
                    raw.Add(new SpeechInterval(start * step, Math.Min(duration, silence * step)));
                start = -1;
                silence = -1;
            }
        }
        if (start >= 0 && duration - start * step >= minSpeechSeconds)
            raw.Add(new SpeechInterval(start * step, duration));

        var pieces = new List<SpeechInterval>();
        foreach (var speech in raw)
        {
            var cursor = speech.Start;
            while (speech.End - cursor > maxSpeechSeconds)
            {
                // Prefer the quietest frame near the limit over a blind fixed-interval cut.
                var first = Math.Max(0, (int)Math.Ceiling((cursor + maxSpeechSeconds - 1.5) / step));
                var last = Math.Min(probabilities.Count - 1, (int)Math.Floor((cursor + maxSpeechSeconds) / step));
                var best = last;
                for (var i = first; i <= last; i++)
                    if (probabilities[i] <= probabilities[best]) best = i;
                var cut = Math.Max(cursor + minSpeechSeconds, Math.Min(cursor + maxSpeechSeconds, best * step));
                pieces.Add(new SpeechInterval(cursor, cut));
                cursor = cut;
            }
            if (speech.End > cursor) pieces.Add(new SpeechInterval(cursor, speech.End));
        }

        var result = pieces.Select(s => new SpeechInterval(Math.Max(0, s.Start - paddingSeconds), Math.Min(duration, s.End + paddingSeconds))).ToArray();
        for (var i = 1; i < result.Length; i++)
        {
            if (result[i - 1].End <= result[i].Start) continue;
            var middle = (result[i - 1].End + result[i].Start) / 2;
            result[i - 1] = result[i - 1] with { End = middle };
            result[i] = result[i] with { Start = middle };
        }
        return result;
    }
}
