using Nikse.SubtitleEdit.Logic.Initializers;

namespace UITests.Logic.Initializers;

public class LanguageInitializerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "se-language-cache-" + Guid.NewGuid().ToString("N"));
    public LanguageInitializerTests() => Directory.CreateDirectory(_folder);
    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public async Task SameVersion_OldCacheWithoutFingerprint_RefreshesNewTranslations()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "version.txt"), "5.5.0-beta5", TestContext.Current.CancellationToken);
        Assert.True(await LanguageInitializer.NeedsUpdate(_folder, "5.5.0-beta5", "new-bundle"));
    }

    [Theory]
    [InlineData("old-bundle", true)]
    [InlineData("new-bundle", false)]
    public async Task SameVersion_UsesBundleContentToDecideRefresh(string installedHash, bool expected)
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "version.txt"), "5.5.0-beta5", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_folder, "bundle-sha256.txt"), installedHash, TestContext.Current.CancellationToken);
        Assert.Equal(expected, await LanguageInitializer.NeedsUpdate(_folder, "5.5.0-beta5", "new-bundle"));
    }

    [Fact]
    public async Task OlderApplication_DoesNotDowngradeNewerLanguageVersion()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "version.txt"), "5.6.0", TestContext.Current.CancellationToken);
        Assert.False(await LanguageInitializer.NeedsUpdate(_folder, "5.5.0-beta5", "old-bundle"));
    }
}
