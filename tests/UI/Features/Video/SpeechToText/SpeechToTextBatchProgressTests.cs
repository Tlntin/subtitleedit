using Nikse.SubtitleEdit.Features.Video.SpeechToText;

namespace UITests.Features.Video.SpeechToText;

public class SpeechToTextBatchProgressTests
{
    private static readonly DateTime Start = new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SecondFileWithoutProgress_RetainsWholeBatchEstimateAndCountsDown()
    {
        var progress = new SpeechToTextBatchProgress([600, 1200, 1800], Start);
        Assert.Null(progress.EstimateRemaining(0, Start));
        Assert.Equal(TimeSpan.FromSeconds(660), progress.EstimateRemaining(0.5, Start.AddSeconds(60)));

        progress.CompleteItem(true, Start.AddSeconds(120));
        Assert.Equal(TimeSpan.FromSeconds(600), progress.EstimateRemaining(0, Start.AddSeconds(120)));
        Assert.Equal(TimeSpan.FromSeconds(570), progress.EstimateRemaining(0, Start.AddSeconds(150)));
        // Halfway through file two, file three must still be included (not just its 120 s).
        Assert.Equal(TimeSpan.FromSeconds(480), progress.EstimateRemaining(0.5, Start.AddSeconds(240)));
        progress.CompleteItem(true, Start.AddSeconds(360));
        Assert.Equal(TimeSpan.FromSeconds(360), progress.EstimateRemaining(0, Start.AddSeconds(360)));
        progress.CompleteItem(true, Start.AddSeconds(720));
        Assert.Equal(TimeSpan.Zero, progress.EstimateRemaining(0, Start.AddSeconds(720)));
    }

    [Fact]
    public void FirstFilePercentageOnlyAndStalledProgress_StillEstimate()
    {
        var progress = new SpeechToTextBatchProgress([100, 200], Start);
        Assert.Equal(TimeSpan.FromSeconds(58), progress.EstimateRemaining(0.1, Start.AddSeconds(2)));
        Assert.Equal(TimeSpan.FromSeconds(57), progress.EstimateRemaining(0.1, Start.AddSeconds(3)));
        // An engine's alignment phase may restart its percentage; do not regress the sample.
        Assert.Equal(TimeSpan.FromSeconds(56), progress.EstimateRemaining(0.05, Start.AddSeconds(4)));
    }

    [Fact]
    public void FailedFile_DoesNotMakeRemainingFilesLookFaster()
    {
        var progress = new SpeechToTextBatchProgress([600, 1200, 1800], Start);
        progress.CompleteItem(true, Start.AddSeconds(120));
        progress.CompleteItem(false, Start.AddSeconds(121));
        Assert.Equal(TimeSpan.FromSeconds(360), progress.EstimateRemaining(0, Start.AddSeconds(121)));
    }

    [Fact]
    public void NewRunDoesNotReuseEarlierSpeed_AndLongEstimatesKeepTheirHours()
    {
        var progress = new SpeechToTextBatchProgress([60, 86400], Start);
        progress.CompleteItem(true, Start.AddSeconds(120));
        var remaining = progress.EstimateRemaining(0, Start.AddSeconds(120));
        Assert.Equal("48:00:00", SpeechToTextBatchProgress.FormatDuration(remaining!.Value));
        Assert.Null(new SpeechToTextBatchProgress([60], Start).EstimateRemaining(0, Start.AddSeconds(1)));
    }
}
