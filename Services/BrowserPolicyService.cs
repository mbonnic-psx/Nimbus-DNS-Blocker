using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// HKLM policy writes that disable browser Secure DNS while blocking is
/// applied. See IBrowserPolicyService for the contract.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BrowserPolicyService : IBrowserPolicyService
{
    private const string PREF_ENABLED = "nimbus_doh_policies_enabled";

    private const string ChromeKeyPath   = @"SOFTWARE\Policies\Google\Chrome";
    private const string EdgeKeyPath     = @"SOFTWARE\Policies\Microsoft\Edge";
    private const string FirefoxKeyPath  = @"SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS";

    private const string ChromiumValueName = "DnsOverHttpsMode";
    private const string ChromiumValueData = "off";
    private const string FirefoxValueName  = "Enabled";
    private const int    FirefoxValueData  = 0;

    public bool IsEnabled => Preferences.Get(PREF_ENABLED, true);

    public Task SetEnabledAsync(bool enabled)
    {
        Preferences.Set(PREF_ENABLED, enabled);
        return Task.CompletedTask;
    }

    public Task<bool> WritePoliciesAsync()
    {
        try
        {
            using (var chrome = Registry.LocalMachine.CreateSubKey(ChromeKeyPath))
                chrome.SetValue(ChromiumValueName, ChromiumValueData, RegistryValueKind.String);

            using (var edge = Registry.LocalMachine.CreateSubKey(EdgeKeyPath))
                edge.SetValue(ChromiumValueName, ChromiumValueData, RegistryValueKind.String);

            using (var firefox = Registry.LocalMachine.CreateSubKey(FirefoxKeyPath))
                firefox.SetValue(FirefoxValueName, FirefoxValueData, RegistryValueKind.DWord);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BrowserPolicyService.WritePoliciesAsync failed: {ex}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> RemovePoliciesAsync()
    {
        try
        {
            RemoveValueIfOurs(ChromeKeyPath,  ChromiumValueName, ChromiumValueData);
            RemoveValueIfOurs(EdgeKeyPath,    ChromiumValueName, ChromiumValueData);
            RemoveValueIfOurs(FirefoxKeyPath, FirefoxValueName,  FirefoxValueData);
            DeleteKeyIfEmpty(FirefoxKeyPath);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BrowserPolicyService.RemovePoliciesAsync failed: {ex}");
            return Task.FromResult(false);
        }
    }

    // Deletes the value only when its current data equals what Nimbus writes,
    // so a policy set by an administrator to anything else is never removed.
    private static void RemoveValueIfOurs(string keyPath, string valueName, object expectedData)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
        if (key is null) return;

        var current = key.GetValue(valueName);
        if (current is null) return;

        if (Equals(current.ToString(), expectedData.ToString()))
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }

    // The Firefox policy lives in its own subkey; remove the subkey only when
    // Nimbus's value was the last thing in it.
    private static void DeleteKeyIfEmpty(string keyPath)
    {
        using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
        {
            if (key is null) return;
            if (key.ValueCount > 0 || key.SubKeyCount > 0) return;
        }

        var parent = Path.GetDirectoryName(keyPath)!.Replace('/', '\\');
        var name   = Path.GetFileName(keyPath);

        using var parentKey = Registry.LocalMachine.OpenSubKey(parent, writable: true);
        parentKey?.DeleteSubKey(name, throwOnMissingSubKey: false);
    }
}
