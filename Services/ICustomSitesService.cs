using Nimbus_Internet_Blocker.Models;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Loads, saves, and edits the user's custom blocked sites. Program against this
/// interface — never depend on CustomSitesService directly.
/// </summary>
public interface ICustomSitesService
{
    /// <summary>
    /// Loads the live custom sites file. Returns <see langword="null"/> when the file
    /// exists but cannot be read or parsed — callers must treat null as "do not
    /// touch disk", never as an empty config.
    /// </summary>
    Task<CustomsRoot?> LoadAsync();

    /// <summary>
    /// Normalizes and saves the custom sites atomically. Returns <see langword="false"/>
    /// on failure; the previous file is left intact.
    /// </summary>
    Task<bool> SaveAsync(CustomsRoot root);

    /// <summary>
    /// Validates and adds a host to <paramref name="root"/> in memory. Nothing is
    /// saved — pending changes persist at apply time (see Blocking.razor).
    /// </summary>
    (bool success, string message) AddSite(CustomsRoot root, string inputHost);

    /// <summary>
    /// Removes a host from <paramref name="root"/> in memory. Nothing is saved —
    /// pending changes persist at apply time.
    /// </summary>
    (bool success, string message) RemoveSite(CustomsRoot root, string host);
}
