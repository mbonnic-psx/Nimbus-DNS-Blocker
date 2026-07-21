using Nimbus_Internet_Blocker.Models;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Loads and saves the category blocklist (presets). Program against this
/// interface — never depend on PresetService directly.
/// </summary>
public interface IPresetService
{
    /// <summary>
    /// Loads the live presets file. Returns <see langword="null"/> when the file
    /// exists but cannot be read or parsed — callers must treat null as "do not
    /// touch disk", never as an empty config (that would be a data-loss path).
    /// </summary>
    Task<PresetsRoot?> LoadAsync();

    /// <summary>
    /// Normalizes and saves the presets atomically. Returns <see langword="false"/>
    /// on failure; the previous file is left intact.
    /// </summary>
    Task<bool> SaveAsync(PresetsRoot root);
}
