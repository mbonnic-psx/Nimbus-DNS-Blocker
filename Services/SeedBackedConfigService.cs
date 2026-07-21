namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Shared seed-file plumbing for the JSON-backed config services: the app ships a
/// read-only <c>*.seed.json</c> in Resources/Raw and, on first run, copies it to a
/// writable live file in AppData. All runtime reads/writes go to the live file.
/// Subclasses supply the file names and the empty-fallback JSON; Load/Save/Normalize
/// stay in the concrete services because their root types differ.
/// </summary>
public abstract class SeedBackedConfigService
{
    /// <summary>Live (writable) file name in AppData, e.g. "presets.json".</summary>
    protected abstract string LiveFileName { get; }

    /// <summary>Packaged seed file name in Resources/Raw, e.g. "presets.seed.json".</summary>
    protected abstract string SeedFileName { get; }

    /// <summary>
    /// JSON written to the live file when the seed can't be read — a valid empty
    /// root of the subclass's shape (e.g. <c>{ "categories": {} }</c>).
    /// </summary>
    protected abstract string EmptyFallbackJson { get; }

    /// <summary>Absolute path of the live file in AppData.</summary>
    public string GetLivePath()
        => Path.Combine(FileSystem.AppDataDirectory, LiveFileName);

    /// <summary>
    /// Ensures the live file exists, copying the packaged seed (or the empty
    /// fallback if the seed can't be read) on first run. Returns the live path.
    /// </summary>
    public async Task<string> EnsureLiveFileExistsAsync()
    {
        string livePath = GetLivePath();
        if (File.Exists(livePath)) return livePath;

        string seedJson = await ReadSeedTextAsync();
        if (string.IsNullOrWhiteSpace(seedJson))
            seedJson = EmptyFallbackJson;

        var folder = Path.GetDirectoryName(livePath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(livePath, seedJson);
        return livePath;
    }

    /// <summary>Reads the packaged seed file from the app package (Resources/Raw).</summary>
    protected async Task<string> ReadSeedTextAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(SeedFileName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
