# Slice 006 — Restore / Unblock All (Phase 0 finale)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the manual test checklist at the bottom.

---

## 1. Goal

The last Phase 0 item: a **Restore / Unblock All** button in Settings (replacing "Coming
soon") that removes every Nimbus block from the hosts file and flushes DNS.

**Decisions already made (do not deviate):**

1. **Remove the Nimbus block, don't copy back `hosts.nimbus.bak`.** Restoring the backup
   would clobber any manual hosts edits the user made since Nimbus first ran. The backup
   stays on disk as a manual escape hatch only.
2. **Restore must keep the disk=applied invariant** (established in slices 003/004): after
   unblocking, the applied state is "nothing enabled", so both config files are rewritten
   with every category and custom site set to `Enabled = false`. **List membership is
   preserved** — custom sites stay in the list, just switched off — so the user can
   re-apply later with one click per toggle.
3. **Gated behind the unlock modal** when a protection mode is active — unblocking
   everything is exactly the impulsive action Accountability/Guardian modes exist to slow
   down. No protection configured → runs directly.
4. **Restore does NOT remove the protection mode.** It unblocks sites; the password/flow
   stays active.
5. New pure logic (`HostsSection.Remove`) gets unit tests in the existing `Nimbus.Tests`
   project — that's why slice 005 came first.

**Scope discipline:** do not touch `Blocking.razor`, the flows, or `UnlockModal`'s
internals. No new CSS classes unless truly needed — reuse `pw-heading`, `btn-danger`,
`pw-action-btn`, `admin-warning`, `pw-divider`.

---

## 2. Before you start

Read in full: `Utilities/HostsSection.cs`, `Services/IHostsFileService.cs`,
`Services/HostsFileService.cs`, `Components/Pages/Settings.razor`,
`Nimbus.Tests/HostsSectionTests.cs`. Skim `Components/Pages/Blocking.razor` only to mirror
its elevation-warning pattern.

The services' current shapes (from slices 004/005): `LoadAsync` returns a nullable root
(`null` = load failed, never save over it), `SaveAsync` returns `bool` and writes
atomically, `ApplyAsync` returns `Task<bool>` and never throws.

---

## 3. Edits

### Edit A — `Utilities/HostsSection.cs`: add `Remove`

A pure function that deletes the whole Nimbus block, markers included:

```csharp
/// <summary>
/// Removes the entire Nimbus-managed section — markers included — from
/// <paramref name="hostsContent"/>. When no complete section is found (missing
/// or reversed markers), the content is returned unchanged: never destructive
/// outside the delimited block.
/// </summary>
public static string Remove(string hostsContent)
```

Implementation mirrors `Splice`'s marker scan exactly (normalize `\r\n`/`\r` to `\n`,
split, find first `BeginMarker` and first `EndMarker` by trimmed match): when
`beginIdx >= 0 && endIdx >= beginIdx`, join everything before BEGIN and after END with
`Environment.NewLine`; otherwise return `hostsContent` unchanged. If removing the block
leaves the file ending in multiple blank lines, that's acceptable — do not get clever
trimming interior lines.

### Edit B — `Services/IHostsFileService.cs`: add `RestoreAsync`

```csharp
/// <summary>
/// Removes the entire Nimbus-managed section from the hosts file and flushes
/// the DNS cache, unblocking every site Nimbus was blocking. The one-time
/// backup is created first if it doesn't exist yet. Returns
/// <see langword="false"/> when nothing was restored (not elevated, or the
/// write failed). Never throws.
/// </summary>
Task<bool> RestoreAsync();
```

### Edit C — `Services/HostsFileService.cs`: implement `RestoreAsync`

Follow `ApplyAsync`'s structure exactly:

1. `IsElevated` check → same "Administrator required" snackbar → `return false;`.
2. Read the hosts file; `await EnsureBackupAsync(hostsContent);` (CLAUDE.md rule: never
   write without the backup existing).
3. `var updated = HostsSection.Remove(hostsContent);`
4. If `updated == hostsContent` (no section present), skip the write — still flush DNS and
   return `true` (nothing to remove IS a successful unblock).
5. Otherwise write back UTF-8 no-BOM (same encoding object as `ApplyAsync`), `FlushDns();`,
   snackbar `Success("Blocking removed", "All Nimbus blocking rules have been removed.")`,
   `return true;`.
6. Same two catch blocks as `ApplyAsync` (`UnauthorizedAccessException` + general), each
   snackbar + `return false;`.

### Edit D — `Components/Pages/Settings.razor`

**D1. Injections.** Add `@inject IHostsFileService HostsFileService`,
`@inject PresetService PresetService`, `@inject CustomSitesService CustomSitesService`
alongside the existing injections.

**D2. Replace the "Coming soon" card** (the first `card card-pad` with
`<p class="subhead">Coming soon.</p>`) with a Restore section:

```razor
<div class="card card-pad">
    <h1 class="h1">Settings</h1>

    <h2 class="pw-heading">Restore / Unblock All</h2>
    <p class="subhead">
        Removes every Nimbus blocking rule from your system and switches all
        categories and custom sites off. Your custom site list is kept.
    </p>

    @if (!HostsFileService.IsElevated)
    {
        <div class="admin-warning">
            ⚠ Nimbus is not running as Administrator — blocking rules cannot be changed.
        </div>
    }

    <button class="btn btn-danger pw-action-btn"
            type="button"
            disabled="@(_isRestoring || !HostsFileService.IsElevated)"
            @onclick="OnRestoreClicked">
        @(_isRestoring ? "Restoring…" : "Restore / Unblock All")
    </button>
</div>
```

**D3. The modal now serves two purposes.** The existing `UnlockModal` instance in Settings
authenticates password-removal; restore reuses it. Add a purpose discriminator instead of a
second modal:

```csharp
private enum UnlockPurpose { RemoveProtection, Restore }
private UnlockPurpose _unlockPurpose = UnlockPurpose.RemoveProtection;
private bool _isRestoring = false;
```

- `OnRemovePasswordClicked()` sets `_unlockPurpose = UnlockPurpose.RemoveProtection;`
  before opening the modal (existing behaviour otherwise unchanged).
- Change the modal's `OnUnlocked` binding to a new router:

```csharp
private async Task OnUnlockedAsync()
{
    if (_unlockPurpose == UnlockPurpose.Restore)
    {
        _showUnlockModal = false;
        await RunRestoreAsync();
        return;
    }

    await OnRemovePasswordUnlocked();   // existing method, unchanged
}
```

(Update the `<UnlockModal ... OnUnlocked="OnUnlockedAsync" ...>` markup accordingly.
`OnRemovePasswordUnlocked` keeps its `_showUnlockModal = false;` first line — no change.)

**D4. Restore handlers:**

```csharp
/*
 * OnRestoreClicked()
 * Unblocking everything is the impulsive action the protection modes exist to
 * slow down, so an active mode gates restore behind the same unlock flow as
 * Apply. No protection → restore runs directly.
 */
private async Task OnRestoreClicked()
{
    if (_isRestoring) return;

    bool needsAuth = PasswordService.IsPasswordEnabled()
                  || PasswordService.IsAccountabilityModeActive();

    if (needsAuth)
    {
        _unlockPurpose   = UnlockPurpose.Restore;
        _showUnlockModal = true;
        StateHasChanged();
        return;
    }

    await RunRestoreAsync();
}

/*
 * RunRestoreAsync()
 * Order matters: unblock the hosts file first (the user's actual intent), then
 * rewrite both configs with everything disabled so disk keeps describing the
 * applied state (slice 003/004 invariant). A config-save failure after a
 * successful hosts restore is surfaced but doesn't undo the unblock.
 */
private async Task RunRestoreAsync()
{
    _isRestoring = true;
    StateHasChanged();

    bool restored = await HostsFileService.RestoreAsync();

    if (restored)
    {
        var presets = await PresetService.LoadAsync();
        var customs = await CustomSitesService.LoadAsync();

        bool configsSynced = presets is not null && customs is not null;

        if (configsSynced)
        {
            foreach (var category in presets!.Categories.Values)
                category.Enabled = false;

            foreach (var site in customs!.Sites)
                site.Enabled = false;

            configsSynced = await PresetService.SaveAsync(presets)
                          & await CustomSitesService.SaveAsync(customs);
        }

        if (!configsSynced)
            Snackbar.Warn("Toggles out of sync",
                "Blocking was removed, but the saved toggle states couldn't be updated. " +
                "They will correct on your next Apply.");
    }

    _isRestoring = false;
    StateHasChanged();
}
```

Note the single `&` (not `&&`) so both saves are attempted even if the first fails.
Success/failure feedback for the hosts operation itself comes from `RestoreAsync`'s
snackbars — don't duplicate it here.

### Edit E — `Nimbus.Tests/HostsSectionTests.cs`: tests for `Remove`

Using the existing `Lines` helper and construction style:

1. **Block present** → markers and every line between them are gone; every line before
   BEGIN and after END preserved in order.
2. **No markers** → returns the input unchanged (reference-equal or string-equal).
3. **Reversed markers** (END before BEGIN) → input returned unchanged (non-destructive).
4. **CRLF input** → same removal result as LF input (compare via `Lines`).
5. **Block at end-of-file without trailing newline** → removed cleanly.
6. **Apply-then-restore round trip:** `Remove(Splice(original, section))` contains exactly
   the original's lines (compare `Lines` sequences ignoring trailing empty entries).

---

## 4. Docs to update

- **`CLAUDE.md`**
  - Tech Debt #8 (no restore feature): mark **RESOLVED**.
  - "What's Done": add Restore/Unblock All (hosts section removed, configs disabled in
    sync, auth-gated, DNS flushed).
  - Project structure / Settings description: Settings no longer says "Coming soon".
  - `IHostsFileService` doc row if one exists.
- **`PLAN.md`** → Phase 0: check the **"Restore / Unblock All"** box (`- [x]`). Phase 0 is
  now complete — add a one-line note `**Phase 0 complete.**` under its heading with
  today's date (2026-07-17).

---

## 5. Verify

- Run `dotnet test Nimbus.Tests/Nimbus.Tests.csproj` if the `dotnet` CLI exists here
  (`which dotnet` first); the new `Remove` tests must pass. If dotnet is unavailable, say
  so plainly — never claim tests pass without running them.
- Re-read every edited file: interface/impl signatures match, Settings markup handlers
  exist in `@code`, `OnUnlocked` binding updated, braces balanced.
- **Do NOT** run git, commit, or push.

### Manual test checklist for the human (include in your report)

On Windows **as Administrator**, with a protection mode active and at least one category +
one custom site applied and verifiably blocked:

1. **Gated restore:** Settings → Restore/Unblock All → unlock modal appears → authenticate
   → success snackbar → blocked site loads again; open
   `C:\Windows\System32\drivers\etc\hosts` → no Nimbus markers remain;
   `hosts.nimbus.bak` still exists untouched.
2. **Toggles synced:** Blocking page → all categories AND custom sites show OFF; custom
   sites are still in the list.
3. **Cancel path:** re-apply some blocks → Restore → modal → Cancel → nothing changed
   (hosts still blocking, toggles still ON).
4. **No protection:** deactivate protection mode → Restore runs immediately without a
   modal.
5. **Restore when nothing blocked:** click Restore again → succeeds quietly (no error).
6. **Not elevated:** run without admin → Restore button disabled with the warning row.
7. `dotnet test .\Nimbus.Tests\Nimbus.Tests.csproj` → all tests (old + new) pass.
8. Regression: Apply still works end-to-end after a restore (splice re-creates the block).

---

## 6. When finished

Summarize for the human: files changed (one line each), whether tests were actually run and
their results, anything that didn't match this spec and how you adapted, and the manual
checklist above. Phase 0 closes when the human's checklist passes.
