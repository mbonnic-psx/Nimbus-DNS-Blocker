# Slice 007 — Phase 1: Close the DoH bypass (BrowserPolicyService)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the manual test checklist at the bottom.

---

## 1. Goal

Chrome, Edge, and Firefox with "Secure DNS" (DNS-over-HTTPS) enabled resolve domains over
HTTPS and **never read the hosts file** — Nimbus's blocks are silently bypassed (Tech Debt
#9). Browsers honor enterprise policy registry keys that force Secure DNS off. This slice
adds a `BrowserPolicyService` (behind an interface) that writes those keys when blocking is
applied and removes them on Restore/Unblock All:

| Browser | HKLM key | Value |
|---|---|---|
| Chrome  | `SOFTWARE\Policies\Google\Chrome` | `DnsOverHttpsMode` = `"off"` (REG_SZ) |
| Edge    | `SOFTWARE\Policies\Microsoft\Edge` | `DnsOverHttpsMode` = `"off"` (REG_SZ) |
| Firefox | `SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS` | `Enabled` = `0` (REG_DWORD) |

**Decisions already made (do not deviate):**

1. **Lifecycle follows PLAN.md:** policies are written during `ApplyAsync` and removed
   during `RestoreAsync`. The Settings toggle only stores a preference (default **ON**)
   that Apply consults — it does not touch the registry itself. When the preference is
   OFF, Apply *removes* the policies (so toggling off + applying clears them).
2. **Policy failure is a warning, not an apply failure.** Hosts blocking still worked;
   `ApplyAsync` returns `true` and surfaces a `Warn` snackbar about the policies.
3. **Surgical removal only.** Never delete the browser policy *keys* — enterprise machines
   may carry other policies under them. Delete only the specific value, and only when its
   current data equals what Nimbus writes (`"off"` / `0`); a value someone else set to
   something different is left alone. The Firefox `DNSOverHTTPS` subkey may be deleted
   only when it ends up with no values and no subkeys.
4. **Honest UI.** The Settings section says plainly what is done to the user's machine,
   that browsers must be **restarted** to notice policy changes, and that the policies are
   removed on Restore/Unblock All.
5. Registry APIs (`Microsoft.Win32.Registry`) exist only on the Windows TFM. That's fine —
   only `net9.0-windows10.0.19041.0` is ever built (Phase 2 trims the phantom TFMs). Mark
   the service `[SupportedOSPlatform("windows")]` like `HostsFileService`. No NuGet
   package is needed on the Windows TFM.
6. **No unit tests** — the logic is all registry I/O; PLAN.md says nothing beyond
   splice/normalize needs tests for v1. Verification is the human's real-browser checklist.

**Scope discipline:** don't touch `Blocking.razor`, the flows, `UnlockModal`, or the
password code. No network calls, no extra policies beyond the three values above.

---

## 2. Before you start

Read in full: `Services/HostsFileService.cs`, `Services/IHostsFileService.cs`,
`Components/Pages/Settings.razor`, `MauiProgram.cs`. Skim `wwwroot/css/app.css` for the
`.switch`/`.slider` classes (reused for the toggle) — add new CSS only if a small layout
rule is genuinely needed, in `app.css`, never inline.

---

## 3. Edits

### Edit A — new file `Services/IBrowserPolicyService.cs`

```csharp
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
```

### Edit B — new file `Services/BrowserPolicyService.cs`

```csharp
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
```

### Edit C — `MauiProgram.cs`

Register before the hosts service (which will depend on it):

```csharp
builder.Services.AddSingleton<IBrowserPolicyService, BrowserPolicyService>();
```

### Edit D — `Services/HostsFileService.cs`: wire policies into apply/restore

**D1.** Constructor gains an `IBrowserPolicyService browserPolicy` parameter stored in a
`private readonly IBrowserPolicyService _browserPolicy;` field.

**D2. `ApplyAsync`:** after `FlushDns()` and the existing success snackbar, before
`return true;`, add:

```csharp
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
```

The method still returns `true` in this branch — policy trouble never fails an apply
(decision 2).

**D3. `RestoreAsync`:** in **both** success paths (section removed, and nothing-to-remove),
before returning `true`:

```csharp
if (!await _browserPolicy.RemovePoliciesAsync())
    _snackbar.Warn("Browser policies",
        "Blocking was removed, but the Secure-DNS policies couldn't be cleaned up.");
```

Update `IHostsFileService`'s doc comments for both methods to mention the policy behaviour.

### Edit E — `Components/Pages/Settings.razor`: the toggle + honest text

**E1.** Add `@inject IBrowserPolicyService BrowserPolicyService`.

**E2.** Add a new card between the Restore card and the Password Protection card:

```razor
<!-- ── Browser Secure DNS ──────────────────────────────────────────────── -->
<div class="card card-pad">
    <h2 class="pw-heading">Browser Secure DNS</h2>
    <p class="subhead">
        Chrome, Edge, and Firefox can bypass blocking entirely via "Secure DNS"
        (DNS over HTTPS). When this switch is on, applying blocking rules also
        disables Secure DNS in those browsers through a system policy, so the
        blocks actually work. The policy is removed when you use
        Restore / Unblock All. Browsers must be restarted to notice the change.
    </p>

    <div class="category-row">
        <div class="category-name">Disable browser Secure DNS while blocking</div>
        <label class="switch" title="Disable browser Secure DNS while blocking">
            <input type="checkbox"
                   checked="@_dohPoliciesEnabled"
                   @onchange="OnDohToggleChangedAsync" />
            <span class="slider"></span>
        </label>
    </div>
</div>
```

(`category-row`/`category-name`/`switch`/`slider` already exist in `app.css`. If
`category-row` needs a margin tweak inside this card, add a small rule to `app.css` — no
inline styles.)

**E3.** In `@code`: add `private bool _dohPoliciesEnabled;`, initialize it in
`OnInitialized()` from `BrowserPolicyService.IsEnabled`, and add:

```csharp
/*
 * OnDohToggleChangedAsync()
 * Stores the preference only — the registry is touched at apply/restore time,
 * inside the elevated hosts-file path (see HostsFileService).
 */
private async Task OnDohToggleChangedAsync(ChangeEventArgs e)
{
    bool enabled = e.Value is bool b
        ? b
        : e.Value is string s && bool.TryParse(s, out var parsed) && parsed;

    _dohPoliciesEnabled = enabled;
    await BrowserPolicyService.SetEnabledAsync(enabled);

    Snackbar.Info("Secure DNS setting saved", enabled
        ? "Secure DNS will be disabled the next time you apply blocking rules."
        : "The policies will be removed the next time you apply or restore.");
}
```

---

## 4. Docs to update

- **`CLAUDE.md`**
  - Tech Debt #9 (DoH bypass): mark **RESOLVED** — describe the three policy values, the
    apply/restore lifecycle, the match-before-delete removal rule, and the
    `nimbus_doh_policies_enabled` preference (default true).
  - "What's Done": add the DoH policy fix.
  - Project structure + DI Registration sections: add the two new service files and the
    registration line.
- **`PLAN.md`** → Phase 1: check boxes **1** (service) and **3** (settings toggle + honest
  text). Leave box **2** (test against real installed browsers) unchecked — only the human
  can check it, after the checklist below passes.

---

## 5. Verify

- `dotnet build -f net9.0-windows10.0.19041.0` if available; in this Linux/WSL environment
  the MAUI Windows target likely cannot build — if so, say that plainly and instead re-read
  every edited file: constructor/DI wiring consistent, interface and implementation match,
  Settings markup handlers exist, braces balanced. Run
  `dotnet test Nimbus.Tests/Nimbus.Tests.csproj` if dotnet exists (nothing should have
  broken it — this slice adds no testable pure logic).
- **Do NOT** run git, commit, or push.

### Manual test checklist for the human (include in your report)

On Windows **as Administrator**, with at least one site blocked. Pick a blocked site you
can recognize failing (e.g. `facebook.com`).

1. **Demonstrate the bypass first** (so the fix is provable): Settings → turn the Secure
   DNS switch OFF → Apply → in Chrome or Edge enable Secure DNS with a public provider
   (Settings → Privacy → Security → Use secure DNS → Cloudflare) → restart the browser →
   the "blocked" site **loads** (that's the bypass).
2. **Close it:** Nimbus Settings → switch ON → Apply → snackbar mentions restarting the
   browser → check `regedit`: the three values exist under the paths in §1 → restart the
   browser → `chrome://policy` (or `edge://policy`) lists `DnsOverHttpsMode = off`; the
   Secure DNS setting shows as managed/greyed → the blocked site now **fails**.
3. **Firefox** (if installed): `about:policies` shows the DNSOverHTTPS policy; blocked
   site fails with Secure DNS previously on.
4. **Restore removes them:** Restore / Unblock All → `regedit`: the three values are gone
   (and the Firefox `DNSOverHTTPS` subkey too, if Nimbus's value was its only content) →
   after browser restart, Secure DNS is user-controllable again.
5. **Toggle-off path:** switch OFF → Apply → values removed from the registry while hosts
   blocking stays active.
6. **Foreign-policy safety (optional):** set `DnsOverHttpsMode` to `automatic` by hand in
   regedit → run Restore → the value survives (data didn't match Nimbus's `off`). Clean up
   by hand afterwards.
7. Regression: apply/cancel/restore flows from earlier slices still behave; `dotnet test`
   still green.

When 1–5 pass, check PLAN.md Phase 1 box 2 yourself — that closes Phase 1.

---

## 6. When finished

Summarize for the human: files changed/created (one line each), anything that didn't match
this spec and how you adapted, and the manual checklist above. Remind them: browsers cache
policy reads — every policy check needs a browser restart first.
