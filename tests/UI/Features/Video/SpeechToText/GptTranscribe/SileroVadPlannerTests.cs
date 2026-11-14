using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class SileroVadPlannerTests
{
    [Fact]
    public void SilenceGapsRemainOnTheOriginalTimeline()
    {
        var probabilities = Enumerable.Repeat(0f, 400).ToArray();
        Array.Fill(probabilities, 0.95f, 40, 50);
        Array.Fill(probabilities, 0.95f, 180, 70);
        var result = SileroVadSpeechDetector.PlanSegments(probabilities, 12.8);
        Assert.Equal(2, result.Count);
        Assert.InRange(result[0].Start, 1.15, 1.17);
        Assert.InRange(result[0].End, 2.99, 3.01);
        Assert.InRange(result[1].Start, 5.63, 5.65);
        Assert.True(result[1].Start - result[0].End > 2);
    }

    [Fact]
    public void LongSpeechIsBoundedAndAdjacentCuesDoNotOverlap()
    {
        var result = SileroVadSpeechDetector.PlanSegments(Enumerable.Repeat(0.9f, 1200).ToArray(), 38.4);
        Assert.True(result.Count >= 4);
        Assert.Equal(0, result[0].Start);
        Assert.Equal(38.4, result[^1].End);
        Assert.All(result, segment => Assert.InRange(segment.End - segment.Start, 0.01, 12.25));
        for (var i = 1; i < result.Count; i++) Assert.True(result[i].Start >= result[i - 1].End);
    }

    [Fact]
    public void SilenceAndTinySpikesAreNotUploadedAsSpeech()
    {
        var probabilities = new float[100];
        probabilities[20] = 1;
        Assert.Empty(SileroVadSpeechDetector.PlanSegments(probabilities, 3.2));
    }
}
