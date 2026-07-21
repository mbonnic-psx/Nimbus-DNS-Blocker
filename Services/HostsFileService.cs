using Nimbus_Internet_Blocker.Models;
using Nimbus_Internet_Blocker.Utilities;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Reads, writes, and manages the Nimbus-managed section of the Windows hosts file.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HostsFileService : IHostsFileService
{
    // ── File paths ─────────────────────────────────────────────────────────────
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string BackupPath    = @"C:\Windows\System32\drivers\etc\hosts.nimbus.bak";

    // ── Dependencies ───────────────────────────────────────────────────────────
    private readonly IPresetService        _presetService;
    private readonly ICustomSitesService   _customSitesService;
    private readonly ISnackbarService      _snackbar;
    private readonly IBrowserPolicyService _browserPolicy;

    public HostsFileService(
        IPresetService        presetService,
        ICustomSitesService   customSitesService,
        ISnackbarService      snackbar,
        IBrowserPolicyService browserPolicy)
    {
        _presetService      = presetService;
        _customSitesService = customSitesService;
        _snackbar           = snackbar;
        _browserPolicy      = browserPolicy;
    }

    // ── IHostsFileService ──────────────────────────────────────────────────────

    // Elevate Admin Privileges
    public bool IsElevated =>
        new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

    public async Task<bool> ApplyAsync()
    {
        if (!IsElevated)
        {
            // Snackbar Error Message seen on Host File Change Page
            _snackbar.Error(
                "Administrator required",
                "Nimbus must be run as Administrator to edit the hosts file. " +
                "Right-click the app and choose 'Run as administrator'.");
            return false;
        }

        try
        {
            // 1. Load both configs concurrently
            var presetsTask = _presetService.LoadAsync();
            var customsTask = _customSitesService.LoadAsync();
            await Task.WhenAll(presetsTask, customsTask);

            var presets = presetsTask.Result;
            var customs = customsTask.Result;

            if (presets is null || customs is null)
            {
                _snackbar.Error(
                    "Apply aborted",
                    "Your saved blocking configuration couldn't be read. Nothing was changed.");
                return false;
            }

            // 2. Read the current hosts file
            var hostsContent = await File.ReadAllTextAsync(HostsFilePath);

            // 3. One-time backup — preserves the original pre-Nimbus state
            await EnsureBackupAsync(hostsContent);

            // 4. Build the replacement Nimbus block
            var section = BuildSection(presets, customs);

            // 5. Splice the block into the hosts file content
            var updatedContent = HostsSection.Splice(hostsContent, section);

            // 6. Write back (UTF-8, no BOM — standard for hosts files)
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(HostsFilePath, updatedContent, utf8NoBom);

            // 7. Flush the DNS cache in a hidden background process
            FlushDns();

            _snackbar.Success("Rules applied", "All enabled categories and custom sites are now active.");

            // DoH policies ride along with apply: browsers with Secure DNS enabled
            // bypass the hosts file entirely, so blocking isn't real without this.
            bool policiesOk = _browserPolicy.IsEnabled
                ? await _browserPolicy.WritePoliciesAsync()
                : await _browserPolicy.RemovePoliciesAsync();

            if (!policiesOk)
                _snackbar.Warn("Browser policies",
                    "Blocking was applied, but the browser Secure-DNS policies couldn't be updated.");
            else if (_browserPolicy.IsEnabled)
                _snackbar.Info("Restart your browser",
                    "Secure DNS has been disabled by policy — restart open browsers for it to take effect.");

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _snackbar.Error(
                "Access denied",
                "Nimbus needs Administrator privileges to edit the hosts file. " +
                "Please restart the app as Administrator.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HostsFileService.ApplyAsync failed: {ex}");
            _snackbar.Error("Apply failed", ex.Message);
            return false;
        }
    }

    public async Task<bool> RestoreAsync()
    {
        if (!IsElevated)
        {
            _snackbar.Error(
                "Administrator required",
                "Nimbus must be run as Administrator to edit the hosts file. " +
                "Right-click the app and choose 'Run as administrator'.");
            return false;
        }

        try
        {
            var hostsContent = await File.ReadAllTextAsync(HostsFilePath);

            await EnsureBackupAsync(hostsContent);

            var updated = HostsSection.Remove(hostsContent);

            if (updated == hostsContent)
            {
                // No Nimbus section present — nothing to remove, still a successful unblock.
                FlushDns();
                _snackbar.Success("Blocking removed", "All Nimbus blocking rules have been removed.");

                if (!await _browserPolicy.RemovePoliciesAsync())
                    _snackbar.Warn("Browser policies",
                        "Blocking was removed, but the Secure-DNS policies couldn't be cleaned up.");

                return true;
            }

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(HostsFilePath, updated, utf8NoBom);

            FlushDns();

            _snackbar.Success("Blocking removed", "All Nimbus blocking rules have been removed.");

            if (!await _browserPolicy.RemovePoliciesAsync())
                _snackbar.Warn("Browser policies",
                    "Blocking was removed, but the Secure-DNS policies couldn't be cleaned up.");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _snackbar.Error(
                "Access denied",
                "Nimbus needs Administrator privileges to edit the hosts file. " +
                "Please restart the app as Administrator.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HostsFileService.RestoreAsync failed: {ex}");
            _snackbar.Error("Restore failed", ex.Message);
            return false;
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates <c>hosts.nimbus.bak</c> using the current hosts file content the first
    /// time Nimbus writes to the file.  Does nothing when the backup already exists,
    /// so the preserved state is always the original, pre-Nimbus version.
    /// </summary>
    private static async Task EnsureBackupAsync(string currentHostsContent)
    {
        if (File.Exists(BackupPath)) return;

        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(BackupPath, currentHostsContent, utf8NoBom);
    }

    /// <summary>
    /// Builds the complete Nimbus-managed section string — BEGIN marker, one IPv4 and one
    /// IPv6 block line for every enabled domain (plus its <c>www.</c> variant), and END marker.
    /// </summary>
    private static string BuildSection(PresetsRoot presets, CustomsRoot customs)
    {
        var sb = new StringBuilder();
        sb.AppendLine(HostsSection.BeginMarker);

        // Preset categories ────────────────────────────────────────────────────
        if (presets.Categories is not null)
        {
            foreach (var (_, category) in presets.Categories)
            {
                if (!category.Enabled || category.Entries is null) continue;

                foreach (var entry in category.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Host)) continue;
                    AppendHostLines(sb, entry.Host, entry.Ipv4 ?? "0.0.0.0", entry.Ipv6 ?? "::");
                }
            }
        }

        // Custom sites ─────────────────────────────────────────────────────────
        if (customs.Sites is not null)
        {
            foreach (var site in customs.Sites)
            {
                if (site.Enabled != true || string.IsNullOrWhiteSpace(site.Host)) continue;
                AppendHostLines(sb, site.Host, site.Ipv4 ?? "0.0.0.0", site.Ipv6 ?? "::");
            }
        }

        sb.Append(HostsSection.EndMarker);  // No trailing newline — HostsSection.Splice controls final endings
        return sb.ToString();
    }

    /// <summary>
    /// Appends IPv4 and IPv6 block lines for <paramref name="host"/> and, when the host
    /// does not already start with <c>www.</c>, its <c>www.</c> variant as well.
    /// </summary>
    private static void AppendHostLines(StringBuilder sb, string host, string ipv4, string ipv6)
    {
        // Root domain
        sb.AppendLine($"{ipv4,-16}{host}");
        sb.AppendLine($"{ipv6,-16}{host}");

        // www. variant — skip if the host is already a www. address
        if (!host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            var www = "www." + host;
            sb.AppendLine($"{ipv4,-16}{www}");
            sb.AppendLine($"{ipv6,-16}{www}");
        }
    }

    /// <summary>
    /// Runs <c>ipconfig /flushdns</c> in a hidden, no-window background process.
    /// A flush failure is non-fatal and is logged to the debug output only.
    /// </summary>
    private static void FlushDns()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "ipconfig",
                Arguments              = "/flushdns",
                WindowStyle            = ProcessWindowStyle.Hidden,
                CreateNoWindow         = true,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
            };
            using var _ = Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HostsFileService.FlushDns failed: {ex}");
        }
    }
}
