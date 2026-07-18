namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Writes and removes the HKLM browser policies that force Secure DNS
/// (DNS-over-HTTPS) off in Chrome, Edge, and Firefox, so those browsers fall
/// back to system DNS and the hosts-file blocking actually applies.
/// </summary>
public interface IBrowserPolicyService
{
    /// <summary>
    /// User preference (MAUI Preferences, default true): should Apply write the
    /// Secure-DNS-off policies? The preference is consulted at apply time only.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Stores the preference. Registry is not touched here.</summary>
    Task SetEnabledAsync(bool enabled);

    /// <summary>
    /// Writes the three policy values. Requires elevation (callers run inside
    /// the already-elevated apply path). Returns false on failure. Never throws.
    /// </summary>
    Task<bool> WritePoliciesAsync();

    /// <summary>
    /// Removes the policy values Nimbus writes — each one only if its current
    /// data matches Nimbus's data, so foreign policies are never deleted.
    /// Returns false on failure. Never throws.
    /// </summary>
    Task<bool> RemovePoliciesAsync();
}
