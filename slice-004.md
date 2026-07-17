# Slice 004 — Custom-sites parity (pending-until-apply) + safe persistence

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the manual test checklist at the bottom.

---

## 1. Goals

Two parts, one slice (they touch the same load/save paths):

**Part 1 — Custom sites get the slice-003 treatment.** Human-verified bug: flip a custom
site's switch → Apply → modal → Cancel → **the switch stays on** (categories revert
correctly). Root cause: custom mutations still save to disk immediately, so the disk ≠
last-applied invariant from slice 003 doesn't hold for `custom.json`. Fix: custom-site
**toggle, add, and remove** all become pending-in-memory; `custom.json` is written only at
apply time; cancel reloads it. Full parity with categories.

> Why add/remove must be pending too: the whole `CustomsRoot` is saved as one file. If add
> saved immediately, it would persist every pending toggle along with it, silently breaking
> the invariant. All-or-nothing is the only coherent design. UI messages must tell the user
> an added site activates on Apply.

**Part 2 — Safe persistence (Phase 0 item 3 / Tech Debt #4).**
- Atomic saves: write a temp file, then `File.Replace` (or `File.Move` when the live file
  doesn't exist yet).
- A failed load must **never** return an empty root that a later save persists: `LoadAsync`
  returns `null` on read/parse failure; every caller handles `null` explicitly.
- Save failures return `false` and surface via snackbar.
- Drive-by bug fixes in the same methods (verified in code): `CustomSitesService`'s fallback
  seed shape is `{ "categories": {} }` — must be `{ "sites": [] }`; its Debug log messages
  say `PresetService` — must say `CustomSitesService`.

**Scope discipline:** do NOT extract the duplicated `NormalizeHost`/seed plumbing into
`Utilities/` (that's Phase 2), do NOT add interfaces to the two config services (Phase 2),
do NOT touch `Settings.razor`, `UnlockModal`, or the flows.

---

## 2. Before you start

Read in full: `Services/PresetService.cs`, `Services/CustomSitesService.cs`,
`Services/HostsFileService.cs`, `Components/Pages/Blocking.razor`.

Grep for callers before changing signatures: `LoadAsync`, `SaveAsync`, `AddCustomSite`,
`RemoveCustomSiteAsync`, `ToggleCustomEnabledAsync`, `NormalizeCustoms` — the expected
callers are only `Blocking.razor` and `HostsFileService`. If you find others, stop and
report instead of guessing.

---

## 3. Part 2 first — service layer (persistence)

### Edit A — new file `Utilities/AtomicFile.cs`

Shared atomic-write helper (both services use it; the empty `HostValidation.cs` placeholder
stays for Phase 2's dedup — don't touch it):

```csharp
using System.Diagnostics;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Crash-safe file writes: content lands in a temp file first, then atomically
/// replaces the target, so a failure mid-write can never truncate the live file.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically.
    /// Returns <see langword="false"/> on any failure; the previous file, if one
    /// existed, is left intact. Never throws.
    /// </summary>
    public static async Task<bool> WriteAllTextAtomicAsync(string path, string content)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            await File.WriteAllTextAsync(tmp, content);

            if (File.Exists(path))
                File.Replace(tmp, path, destinationBackupFileName: null);
            else
                File.Move(tmp, path);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AtomicFile.WriteAllTextAtomicAsync failed for {path}: {ex}");
            return false;
        }
    }
}
```

### Edit B — `Services/PresetService.cs`

**B1. `LoadAsync` returns `Task<PresetsRoot?>` — `null` means "load failed, do not save
over this".** Missing file still seeds normally (that's first-run, not failure). An
existing-but-empty file is damage, not "no data" — return `null`:

```csharp
/// <summary>
/// Loads the live presets file. Returns <see langword="null"/> when the file
/// exists but cannot be read or parsed — callers must treat null as "do not
/// touch disk", never as an empty config (that would be a data-loss path).
/// </summary>
public async Task<PresetsRoot?> LoadAsync()
{
    try
    {
        await EnsureLiveFileExistsAsync();

        string json = await File.ReadAllTextAsync(GetLivePath());
        if (string.IsNullOrWhiteSpace(json)) return null;

        var root = JsonSerializer.Deserialize<PresetsRoot>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (root is null) return null;

        NormalizePresets(root);
        return root;
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"PresetService.LoadAsync failed: {ex}");
        return null;
    }
}
```

**B2. `SaveAsync` returns `Task<bool>` and writes atomically:**

```csharp
/// <summary>
/// Normalizes and saves the presets atomically. Returns <see langword="false"/>
/// on failure; the previous file is left intact.
/// </summary>
public async Task<bool> SaveAsync(PresetsRoot root)
{
    if (root is null) return false;

    try
    {
        NormalizePresets(root);
        var json = JsonSerializer.Serialize(root,
            new JsonSerializerOptions { WriteIndented = true });
        return await AtomicFile.WriteAllTextAtomicAsync(GetLivePath(), json);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"PresetService.SaveAsync failed: {ex}");
        return false;
    }
}
```

Add `using Nimbus_Internet_Blocker.Utilities;` to the file. While editing, delete the
tutorial-style comments in the methods you rewrite (per CLAUDE.md coding style) — do not
reproduce the "Task<T> = this work will finish later" narration.

### Edit C — `Services/CustomSitesService.cs`

**C1.** Mirror B1/B2 exactly for `CustomsRoot` (`LoadAsync` → `Task<CustomsRoot?>`,
`SaveAsync` → `Task<bool>` via `AtomicFile`).

**C2.** In `EnsureLiveFileExistsAsync`, fix the fallback seed shape:
`"{ \"categories\": {} }"` → `"{ \"sites\": [] }"`.

**C3.** Fix the copy-paste log messages: `PresetService.LoadAsync failed` →
`CustomSitesService.LoadAsync failed`, and same for `SaveAsync`.

**C4. Replace the load-modify-save mutators with in-memory ones.** Delete
`AddCustomSite` and `RemoveCustomSiteAsync` entirely and add:

```csharp
/// <summary>
/// Validates and adds a host to <paramref name="root"/> in memory. Nothing is
/// saved — pending changes persist at apply time (see Blocking.razor).
/// </summary>
public (bool success, string message) AddSite(CustomsRoot root, string inputHost)
{
    var normalizedHost = NormalizeHost(inputHost);

    if (string.IsNullOrWhiteSpace(normalizedHost) || !normalizedHost.Contains('.'))
        return (false, "Enter a valid host like example.com.");

    if (root.Sites.Any(s => string.Equals(s.Host, normalizedHost, StringComparison.OrdinalIgnoreCase)))
        return (false, "This host already exists in your custom sites.");

    root.Sites.Add(new CustomEntry { Host = normalizedHost, Enabled = true, Ipv4 = "0.0.0.0", Ipv6 = "::" });
    NormalizeCustoms(root);

    return (true, $"{normalizedHost} added — click Apply Blocking Rules to activate.");
}

/// <summary>
/// Removes a host from <paramref name="root"/> in memory. Nothing is saved —
/// pending changes persist at apply time.
/// </summary>
public (bool success, string message) RemoveSite(CustomsRoot root, string host)
{
    var normalizedHost = NormalizeHost(host);

    if (string.IsNullOrWhiteSpace(normalizedHost))
        return (false, "Invalid host provided.");

    var before = root.Sites.Count;
    root.Sites = root.Sites
        .Where(s => !string.Equals(s.Host, normalizedHost, StringComparison.OrdinalIgnoreCase))
        .ToList();

    return before == root.Sites.Count
        ? (false, $"{normalizedHost} was not found in your custom sites.")
        : (true, $"{normalizedHost} removed — apply to update your blocking rules.");
}
```

### Edit D — `Services/HostsFileService.cs`

`ApplyAsync` loads both configs; with nullable loads it must abort instead of applying an
empty root. After awaiting both tasks, replace the two `.Result` reads with:

```csharp
var presets = presetsTask.Result;
var customs = customsTask.Result;

if (presets is null || customs is null)
{
    _snackbar.Error(
        "Apply aborted",
        "Your saved blocking configuration couldn't be read. Nothing was changed.");
    return false;
}
```

No other changes in this file.

---

## 4. Part 1 — `Components/Pages/Blocking.razor` (customs parity + null handling)

**E1. Custom mutations become synchronous, in-memory.** Replace `AddCustomAsync`,
`RemoveCustomAsync`, `ToggleCustomEnabledAsync`, `OnCustomInputKeyDown` with:

```csharp
private void AddCustom()
{
    ClearApplyStatus();
    _customMessage = "";

    if (_customRoot is null)
    {
        _customMessage = "Custom sites are unavailable — the saved config failed to load.";
        return;
    }

    var (success, message) = CustomSitesService.AddSite(_customRoot, _customInput);
    _customMessage = message;

    if (success)
        _customInput = "";
}

private void RemoveCustom(string host)
{
    ClearApplyStatus();

    if (_customRoot is null) return;

    var (success, message) = CustomSitesService.RemoveSite(_customRoot, host);

    if (success)
        Snackbar.Success("Site removed", message);
    else
        Snackbar.Error("Remove failed", message);
}

/*
 * ToggleCustomEnabled()
 * Pending in-memory flip, exactly like category toggles — persisted only by
 * ApplyPendingAsync; discarded by cancel or navigation.
 */
private void ToggleCustomEnabled(CustomEntry site, ChangeEventArgs e)
{
    ClearApplyStatus();

    bool newValue = e.Value is bool b
        ? b
        : e.Value is string s && bool.TryParse(s, out var parsed) && parsed;

    site.Enabled = newValue;
}

private void OnCustomInputKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Enter")
        AddCustom();
}
```

Update the markup to the new handler names/signatures:
- Add button: `@onclick="AddCustom"`
- Remove button: `@onclick="() => RemoveCustom(site.Host)"`
- Custom switch: `@onchange="e => ToggleCustomEnabled(site, e)"`

**E2. `OnInitializedAsync` handles null loads** (don't silently show an empty page):

```csharp
protected override async Task OnInitializedAsync()
{
    _presets = await PresetService.LoadAsync();
    if (_presets is null)
        Snackbar.Error("Load failed",
            "Couldn't read your saved categories (presets.json). Editing is disabled until it's fixed or deleted.");

    _categoryCount = _presets?.Categories?.Count ?? 0;

    _customRoot = await CustomSitesService.LoadAsync();
    if (_customRoot is null)
        Snackbar.Error("Load failed",
            "Couldn't read your saved custom sites (custom.json). Editing is disabled until it's fixed or deleted.");
}
```

**E3. `ApplyPendingAsync` saves and restores both roots.** Replace it with:

```csharp
/*
 * ApplyPendingAsync()
 * Persists the pending category and custom-site changes, then writes the hosts
 * file. Invariant: the config files are only ever written here, so outside an
 * apply they always describe the last-applied state — which is what
 * OnModalCancelled reloads on cancel. Saves are atomic; on any failure the
 * last-applied configs are written back so the invariant survives, while the
 * pending changes stay visible in the UI for a retry.
 */
private async Task ApplyPendingAsync()
{
    _isApplying = true;
    StateHasChanged();

    var lastAppliedPresets = await PresetService.LoadAsync();
    var lastAppliedCustoms = await CustomSitesService.LoadAsync();

    bool applied = false;

    if (_presets is null || _customRoot is null ||
        lastAppliedPresets is null || lastAppliedCustoms is null)
    {
        Snackbar.Error("Apply aborted",
            "Your saved configuration couldn't be read — nothing was changed.");
    }
    else if (!await PresetService.SaveAsync(_presets) ||
             !await CustomSitesService.SaveAsync(_customRoot))
    {
        Snackbar.Error("Save failed", "Couldn't save your changes — nothing was applied.");
        await PresetService.SaveAsync(lastAppliedPresets);       // best-effort rollback
        await CustomSitesService.SaveAsync(lastAppliedCustoms);  // (atomic saves keep old file on failure)
    }
    else
    {
        applied = await HostsFileService.ApplyAsync();

        if (!applied)
        {
            await PresetService.SaveAsync(lastAppliedPresets);
            await CustomSitesService.SaveAsync(lastAppliedCustoms);
        }
    }

    _applyStatusMessage = applied
        ? "Changes applied successfully"
        : "Failed to apply changes, please try again";
    _changesApplied = applied;

    _isApplying = false;
}
```

**E4. `OnModalCancelled` reloads both roots**, keeping current in-memory state if a reload
fails (never blank the page):

```csharp
private async Task OnModalCancelled()
{
    _showUnlockModal = false;

    var presets = await PresetService.LoadAsync();
    var customs = await CustomSitesService.LoadAsync();

    if (presets is null || customs is null)
    {
        Snackbar.Error("Revert failed", "Couldn't reload the last-applied configuration.");
        return;
    }

    _presets       = presets;
    _customRoot    = customs;
    _categoryCount = _presets?.Categories?.Count ?? 0;
    StateHasChanged();
}
```

**E5.** After editing, grep the file: no references to `AddCustomAsync`,
`RemoveCustomAsync`, `ToggleCustomEnabledAsync`, `AddCustomSite`, or
`RemoveCustomSiteAsync` may remain.

---

## 5. Docs to update

- **`CLAUDE.md`**
  - Tech Debt #4 (data-loss path): mark **RESOLVED** — nullable loads, atomic saves via
    `Utilities/AtomicFile.cs`, save failures surfaced.
  - Tech Debt #5: note the seed-shape and wrong-service log messages are fixed; the
    ~120-line dedup itself remains for Phase 2.
  - The (former) #3 note: custom sites now have full parity — add/remove/toggle are all
    pending until Apply.
  - "What's Done": add safe persistence + custom-sites pending-until-apply.
  - Project structure note for `Utilities/`: `AtomicFile.cs` now exists alongside the
    empty `HostValidation.cs` placeholder.
- **`PLAN.md`** → Phase 0: check the box on **"Safe persistence"** (`- [x]`).

---

## 6. Verify

- Attempt `dotnet build -f net9.0-windows10.0.19041.0`; in this Linux/WSL environment the
  MAUI Windows target likely cannot build — if it fails for that reason, that's expected.
  Instead re-read every edited file: all `LoadAsync` callers handle `null`; all `SaveAsync`
  callers use the `bool`; markup handler names match the `@code` block; no stale method
  references (Edit E5 grep); braces balanced.
- **Do NOT** run git, commit, or push.

### Manual test checklist for the human (include in your report)

Run on Windows **as Administrator**:

1. **Custom toggle reverts:** flip a custom site's switch → Apply → modal → Cancel →
   switch snaps back (the bug this slice exists for).
2. **Add is pending:** add a site → it appears in the list with the "click Apply … to
   activate" message → Cancel out of the Apply modal → **site is gone from the list**.
   Add it again → Apply → authenticate → site persists, survives navigation and restart.
3. **Remove is pending:** remove a site → Cancel out of Apply → site comes back.
   Remove → Apply → authenticate → site stays gone.
4. **Categories regression:** slice-003 tests still pass (flip → cancel reverts; applied
   state survives navigation/restart).
5. **Corrupt-file safety (the data-loss fix):** close the app, open
   `%LOCALAPPDATA%\...\com.companyname.nimbusinternetblocker\Data\presets.json`, replace
   its contents with `garbage{{{`, reopen the app → Blocking shows a "Load failed" error;
   clicking Apply shows "Apply aborted" and **presets.json still contains `garbage{{{`**
   (nothing overwrote it). Delete the file → reopen → it reseeds with defaults.
6. **Behaviour note (intentional):** adding a custom site and navigating away without
   applying discards it — the UI message warns about this.

---

## 7. When finished

Summarize for the human: files changed (one line each), anything that didn't match this
spec and how you adapted, the manual checklist above, and a reminder that build/run happens
on Windows and they do the push.
