namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Reads, writes, and manages the Nimbus-managed section of the Windows hosts file.
/// </summary>
public interface IHostsFileService
{
    /// <summary>
    /// Returns <see langword="true"/> when the current process is running with
    /// Administrator (elevated) privileges — required to write to the hosts file.
    /// </summary>
    bool IsElevated { get; }

    /// <summary>
    /// Collects every enabled preset category and every enabled custom site,
    /// writes their domains into the Nimbus-managed block inside the system hosts file,
    /// and flushes the DNS cache.  A one-time backup of the original hosts file is
    /// created before the very first write. Also writes (or removes, if the user's
    /// Secure DNS preference is off) the browser Secure-DNS-off policies via
    /// <see cref="IBrowserPolicyService"/> — a policy failure only produces a warning
    /// snackbar, it never fails the apply itself.
    /// Returns <see langword="true"/> when the hosts file was written successfully,
    /// <see langword="false"/> when nothing was applied (not elevated, or the write failed).
    /// Never throws — failures are reported via the snackbar and the return value.
    /// </summary>
    Task<bool> ApplyAsync();

    /// <summary>
    /// Removes the entire Nimbus-managed section from the hosts file and flushes
    /// the DNS cache, unblocking every site Nimbus was blocking. The one-time
    /// backup is created first if it doesn't exist yet. Also removes the browser
    /// Secure-DNS-off policies via <see cref="IBrowserPolicyService"/> — a cleanup
    /// failure only produces a warning snackbar, it never fails the restore. Returns
    /// <see langword="false"/> when nothing was restored (not elevated, or the
    /// write failed). Never throws.
    /// </summary>
    Task<bool> RestoreAsync();
}
