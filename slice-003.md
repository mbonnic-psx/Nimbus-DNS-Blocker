# Slice 003 — Real toggle-revert (pending-until-apply) + truthful ApplyAsync

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the manual test checklist at the bottom.

---

## 1. Why this slice exists (root cause of the still-broken revert)

Slice 002 added a "last-applied snapshot" to `Blocking.razor`, seeded from disk at page
load. That design has a hole: **category flips save to disk immediately**, so disk holds
*pending* (unapplied) changes too. Any flip that survives a page navigation or app restart
gets baked into the snapshot as if it were applied — cancel then "reverts" to a wrong
baseline and the toggle appears stuck. Human-verified failing.

**New design — change the invariant instead of snapshotting:**

- Category flips are **pending, in-memory only**. Nothing is saved on flip.
- The presets file is written **only at apply time** (immediately before the hosts write,
  because `HostsFileService.ApplyAsync` loads presets from disk).
- Therefore **disk always equals the last-applied state**. Cancel = reload from disk.
  Navigation/restart with pending flips discards them — honest UI, and exactly the
  behaviour the human's tests demand.
- All snapshot machinery (`_appliedPresetsSnapshot`, `DeepCopy`) is **deleted**.

While rewriting the apply paths, also complete Phase 0 item 2 / Tech Debt #2:
**`ApplyAsync` returns `Task<bool>`** so the status line stops claiming success on failure.
(Today it swallows all exceptions and returns `void`; the caller's `try/catch` is dead code.)

**Scope discipline:** category (preset) toggles only. Custom sites keep their current
save-immediately behaviour — their list membership (add/remove) is list management, not
pending blocking state, and reverting the shared `CustomsRoot` could delete a site the user
deliberately added. Do not touch `AddCustomAsync`, `RemoveCustomAsync`,
`ToggleCustomEnabledAsync`, `AccountabilityFlow`, `GuardianFlow`, `UnlockModal`, or
`Settings.razor`.

---

## 2. Before you start

Read in full: `Components/Pages/Blocking.razor`, `Services/HostsFileService.cs`,
`Services/IHostsFileService.cs`.

---

## 3. Edits

### Edit A — `Services/IHostsFileService.cs`

Change `ApplyAsync` to return `Task<bool>` and update its doc comment:

```csharp
/// <summary>
/// Collects every enabled preset category and every enabled custom site,
/// writes their domains into the Nimbus-managed block inside the system hosts file,
/// and flushes the DNS cache.  A one-time backup of the original hosts file is
/// created before the very first write.
/// Returns <see langword="true"/> when the hosts file was written successfully,
/// <see langword="false"/> when nothing was applied (not elevated, or the write failed).
/// Never throws — failures are reported via the snackbar and the return value.
/// </summary>
Task<bool> ApplyAsync();
```

### Edit B — `Services/HostsFileService.cs`

Change `ApplyAsync`'s signature to `public async Task<bool> ApplyAsync()` and:

- the `!IsElevated` early-out: `return false;` (keep its snackbar)
- after the success snackbar (`"Rules applied"`): `return true;`
- both `catch` blocks: `return false;` (keep their snackbars)

No other logic changes in this file.

### Edit C — `Components/Pages/Blocking.razor`

**C1. Delete the snapshot machinery entirely:**

- the `_appliedPresetsSnapshot` field and its comment block
- the `_appliedPresetsSnapshot = DeepCopy(_presets);` line in `OnInitializedAsync`
- the whole `DeepCopy` helper method and its comment
- After editing, `grep` the file: no references to `_appliedPresetsSnapshot` or `DeepCopy`
  may remain.

**C2. Flips become pending (no save).** Replace `OnCategoryChangedAsync` with a synchronous
version — it no longer touches disk:

```csharp
/*
 * OnCategoryChanged()
 * Flips a category toggle in memory only. Pending flips are persisted by
 * ApplyPendingAsync at apply time; disk always holds the last-applied state,
 * so cancelling (or navigating away) discards pending flips by design.
 */
private void OnCategoryChanged(string name)
{
    ClearApplyStatus();

    if (_presets is null) return;
    if (!_presets.Categories.TryGetValue(name, out var cat)) return;

    cat.Enabled = !cat.Enabled;
}
```

Update the checkbox markup to match the new name/signature:

```razor
@onchange="_ => OnCategoryChanged(name)"
```

**C3. One shared apply path.** Replace `OnApplyClicked` and `OnUnlocked` with:

```csharp
/*
 * OnApplyClicked()
 * No protection configured → applies directly. Otherwise opens the unlock
 * modal; the actual apply happens in OnUnlocked after authentication.
 */
private async Task OnApplyClicked()
{
    if (_isApplying) return;

    bool needsAuth = PasswordService.IsPasswordEnabled()
                  || PasswordService.IsAccountabilityModeActive();

    if (needsAuth)
    {
        _showUnlockModal = true;
        StateHasChanged();
        return;
    }

    await ApplyPendingAsync();
}

/*
 * OnUnlocked()
 * Called by UnlockModal when the user passes authentication.
 */
private async Task OnUnlocked()
{
    _showUnlockModal = false;
    await ApplyPendingAsync();
}

/*
 * ApplyPendingAsync()
 * Persists the pending category toggles, then writes the hosts file.
 * Invariant: the presets file is only ever written here, so outside an apply
 * it always describes the last-applied state — which is what OnModalCancelled
 * reloads on cancel. If the hosts write fails, the previous config is written
 * back so the invariant survives failure (pending flips stay visible in the
 * UI for a retry).
 */
private async Task ApplyPendingAsync()
{
    _isApplying = true;
    StateHasChanged();

    var lastApplied = await PresetService.LoadAsync();

    if (_presets is not null)
        await PresetService.SaveAsync(_presets);

    bool applied = await HostsFileService.ApplyAsync();

    if (!applied)
        await PresetService.SaveAsync(lastApplied);

    _applyStatusMessage = applied
        ? "Changes applied successfully"
        : "Failed to apply changes, please try again";
    _changesApplied = applied;

    _isApplying = false;
}
```

Note: no `try/catch` around `ApplyAsync` — its contract (Edit A) is that it never throws.

**C4. Cancel = reload the last-applied state from disk.** Replace `OnModalCancelled`:

```csharp
/*
 * OnModalCancelled()
 * The user backed out of the unlock modal without authenticating. Pending
 * flips were never saved, so reloading from disk restores the last-applied
 * state exactly.
 */
private async Task OnModalCancelled()
{
    _showUnlockModal = false;

    _presets       = await PresetService.LoadAsync();
    _categoryCount = _presets?.Categories?.Count ?? 0;
    StateHasChanged();
}
```

---

## 4. Docs to update

- **`CLAUDE.md`**
  - Tech Debt #2 (contradictory apply feedback): mark **RESOLVED** — `ApplyAsync` returns
    `bool`, `Blocking.razor` branches on it.
  - Tech Debt #3 (snapshot logic): replace the slice-002 wording with the new design —
    category flips are pending-in-memory; presets save only at apply; cancel/navigation
    reloads last-applied from disk; snapshot machinery deleted. Custom-site toggle revert
    remains a scoped-out follow-up.
  - "What's Done": add truthful apply feedback + pending-until-apply category toggles.
- **`PLAN.md`** → Phase 0: check the box on **"Truthful apply feedback"** (`- [x]`) — both
  halves (bool return, dead snapshot deleted) are done in this slice.

---

## 5. Verify

- Attempt `dotnet build -f net9.0-windows10.0.19041.0`; in this Linux/WSL environment the
  MAUI Windows target likely cannot build — if it fails for that reason, that's expected.
  Instead re-read every edited file: interface and implementation signatures match
  (`Task<bool>`), all `return` paths in `ApplyAsync` return a value, no remaining
  references to `_appliedPresetsSnapshot`/`DeepCopy`/`OnCategoryChangedAsync`, Razor markup
  matches the renamed handler, braces balanced.
- **Do NOT** run git, commit, or push.

### Manual test checklist for the human (include in your report)

Run on Windows **as Administrator** (apply must actually succeed for these):

0. **One-time sync:** after updating, apply once (authenticate) so the hosts file and the
   saved config agree — earlier sessions may have left unapplied flips on disk that the new
   design will treat as applied until this first sync.
1. **Cancel reverts:** flip a category ON → Apply → modal → Cancel → toggle snaps back.
2. **Cancel survives navigation:** flip a category ON → go to Home → back to Blocking →
   the toggle already shows the last-applied state (pending flip discarded — expected).
3. **Success sticks:** flip ON → Apply → authenticate → toggle stays ON; navigate away and
   back → still ON.
4. **New baseline:** after step 3, flip a *different* category → Apply → Cancel → only the
   new flip reverts; the step-3 category stays ON.
5. **Restart:** close and reopen the app → toggles match what was last applied.
6. **Truthful failure (no admin):** run without admin → Apply button is disabled and the
   warning banner shows (pre-existing behaviour, just confirm nothing regressed).

Behaviour change to be aware of (intentional): flipping toggles and leaving the page
without applying now discards the flips — a switch never shows unapplied state after
navigation.

---

## 6. When finished

Summarize for the human: files changed (one line each), anything that didn't match this
spec and how you adapted, the manual checklist above, and a reminder that build/run happens
on Windows and they do the push.
