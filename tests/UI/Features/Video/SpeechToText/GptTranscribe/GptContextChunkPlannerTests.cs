using Nikse.SubtitleEdit.Features.Video.SpeechToText.GptTranscribe;

namespace UITests.Features.Video.SpeechToText.GptTranscribe;

public class GptContextChunkPlannerTests
{
    [Fact]
    public void HundredsOfTinyUtterances_AreCombinedIntoBoundedContinuousContext()
    {
        var speech = Enumerable.Range(0, 1151).Select(i => new SpeechInterval(i * 2.0, i * 2.0 + .5)).ToArray();
        var result = GptContextChunkPlanner.Plan(speech, 2400);
        Assert.InRange(result.Count, 80, 135);
        Assert.Equal(0, result[0].Start);
        Assert.Equal(2400, result[^1].End);
        Assert.All(result, r => Assert.InRange(r.End - r.Start, 0.01, 30));
        for (var i = 1; i < result.Count; i++) Assert.Equal(result[i - 1].End, result[i].Start);
        Assert.Equal(2400, result.Sum(r => r.End - r.Start));
    }

    [Fact]
    public void RejectedVadOrLongUnvoicedGaps_DoNotRemoveAnyAudio()
    {
        var result = GptContextChunkPlanner.Plan([], 180);
        Assert.InRange(result.Count, 6, 8);
        Assert.Equal(0, result[0].Start);
        Assert.Equal(180, result[^1].End);
        Assert.Equal(180, result.Sum(r => r.End - r.Start));
    }

    [Fact]
    public void CutsPreferAQuietGapNearTarget()
    {
        var result = GptContextChunkPlanner.Plan([new(0, 22), new(24, 90)], 90);
        Assert.Equal(23, result[0].End);
        Assert.Equal(result[0].End, result[1].Start);
    }
}
