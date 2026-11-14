using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

public sealed record GptTranscribeModel(string Id, string Name, OpenAiSttRequestProfile Profile)
{
    public bool HasNativeTimestamps => Profile == OpenAiSttRequestProfile.GptDiarize;
    public override string ToString() => Name;
}

public static class GptTranscribeModels
{
    public static IReadOnlyList<GptTranscribeModel> All { get; } = new[]
    {
        new GptTranscribeModel("gpt-transcribe", "GPT Transcribe", OpenAiSttRequestProfile.GptTranscribe),
        new GptTranscribeModel("gpt-4o-transcribe", "GPT-4o Transcribe", OpenAiSttRequestProfile.Gpt4o),
        new GptTranscribeModel("gpt-4o-mini-transcribe", "GPT-4o Mini Transcribe", OpenAiSttRequestProfile.Gpt4o),
        new GptTranscribeModel("gpt-4o-mini-transcribe-2025-12-15", "GPT-4o Mini Transcribe (2025-12-15)", OpenAiSttRequestProfile.Gpt4o),
        new GptTranscribeModel("gpt-4o-transcribe-diarize", "GPT-4o Transcribe Diarize", OpenAiSttRequestProfile.GptDiarize),
    };

    public static GptTranscribeModel? Find(string? id) => All.FirstOrDefault(m => m.Id.Equals(id, StringComparison.Ordinal));
}
