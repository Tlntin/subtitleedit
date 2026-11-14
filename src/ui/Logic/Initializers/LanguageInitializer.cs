using Avalonia.Platform;
using Nikse.SubtitleEdit.Logic.Compression;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Logic.Initializers;

public interface ILanguageInitializer
{
    Task UpdateLanguagesIfNeeded();
}

public class LanguageInitializer(IZipUnpacker zipUnpacker) : ILanguageInitializer
{
    private static readonly Uri LanguagesZipUri = new("avares://SubtitleEdit/Assets/Languages.zip");
    private static readonly Lazy<string> BundleFingerprint = new(() =>
    {
        using var stream = AssetLoader.Open(LanguagesZipUri);
        return Convert.ToHexString(SHA256.HashData(stream));
    });

    public async Task UpdateLanguagesIfNeeded()
    {
        if (await NeedsUpdate())
        {
            // Only stamp the version once everything actually landed. Stamping regardless would
            // mark a half-unpacked folder as current, and this version would then never retry.
            if (Unpack())
            {
                WriteNewVersionFile();
                // Startup may already have loaded the old cached language before extraction.
                // Reload it so newly opened dialogs use the refreshed strings in this session.
                if (Se.Settings.General.Language != "English")
                {
                    var selected = Path.Combine(Se.TranslationFolder, Se.Settings.General.Language + ".json");
                    if (File.Exists(selected)) await Se.LoadLanguageFromFileAsync(selected);
                }
            }
        }
    }

    private static void WriteNewVersionFile()
    {
        string outputDir = Se.TranslationFolder;
        try
        {
            if (!Directory.Exists(outputDir))
            {
                return;
            }

            var versionFileName = Path.Combine(outputDir, "version.txt");
            File.WriteAllText(versionFileName, Se.Version);
            File.WriteAllText(Path.Combine(outputDir, "bundle-sha256.txt"), BundleFingerprint.Value);
        }
        catch
        {
            Se.LogError($"Could not write version file in \"{outputDir}\" folder.");
        }
    }

    private static Task<bool> NeedsUpdate() => NeedsUpdate(Se.TranslationFolder, Se.Version, BundleFingerprint.Value);

    internal static async Task<bool> NeedsUpdate(string outputDir, string applicationVersion, string bundleFingerprint)
    {
        if (!Directory.Exists(outputDir))
        {
            return true;
        }

        var versionFileName = Path.Combine(outputDir, "version.txt");
        if (!File.Exists(versionFileName))
        {
            return true;
        }

        var currentNormalizedVersion = new SemanticVersion(applicationVersion);

        var version = await File.ReadAllTextAsync(versionFileName);
        var normalizedVersion = new SemanticVersion(version);

        if (normalizedVersion.IsLessThan(currentNormalizedVersion))
        {
            return true;
        }
        if (currentNormalizedVersion.IsLessThan(normalizedVersion)) return false;

        // Test builds and patched releases can share a version number while shipping new
        // translations. A content fingerprint prevents old caches from leaving new fields in English.
        var fingerprintFile = Path.Combine(outputDir, "bundle-sha256.txt");
        return !File.Exists(fingerprintFile) ||
            !string.Equals((await File.ReadAllTextAsync(fingerprintFile)).Trim(), bundleFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    /// <returns><see langword="true"/> when every language file was written.</returns>
    private bool Unpack()
    {
        try
        {
            using var zipStream = AssetLoader.Open(LanguagesZipUri);
            zipUnpacker.UnpackZipStream(zipStream, Se.TranslationFolder);
            return true;
        }
        catch (Exception exception)
        {
            Se.LogError(exception, $"Could not unpack language files into \"{Se.TranslationFolder}\".");
            return false;
        }
    }
}
