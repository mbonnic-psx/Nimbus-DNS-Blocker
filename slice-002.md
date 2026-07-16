# Slice 002 — Password feedback + toggle-revert-on-cancel

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> described below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or
> run any `git` command** — the human handles all pushes. When done, report what you changed
> and the manual test checklist at the bottom.

---

## 1. Goals (three user-reported bugs)

1. **Password setup gives no visible feedback when it fails validation.** In `Settings.razor`,
   a password that doesn't meet the rules only fires a snackbar — there's no inline text.
   Add inline red error text under the password form, plus a persistent requirements hint.
2. **Wrong password typed in the main window (Settings) shows no warning.** The "Change
   Password" form's wrong-current-password case only snackbars. Add inline red error text
   the same way. (The modal already has inline text — this is about the Settings page only.)
3. **Cancelling the unlock modal leaves a flipped category toggle flipped.** The toggle's
   state no longer matches what's actually applied. Make cancel revert the category toggles
   (and the saved file) to the last-applied state.

**Scope discipline:** keep the existing `Snackbar.*` calls — inline text is *added*, not a
replacement. Do NOT redesign the snackbar system. For bug 3, fix **category (preset) toggles
only** — see the explicit note in Edit C about why custom-site toggles are out of scope.

---

## 2. Before you start

Read these in full so edits match the real current state (line numbers are guidance):

- `Components/Pages/Settings.razor`
- `Components/Pages/Blocking.razor`
- `wwwroot/css/app.css` (only the `.um-error` / `.gf-error` blocks, for style matching)

**Why bug 3 needs a *last-applied* snapshot (read this before editing):** category toggles
call `PresetService.SaveAsync` immediately on flip (`OnCategoryChangedAsync`), and
`HostsFileService.ApplyAsync` re-loads presets **from disk** — so the flip must be persisted
for Apply to see it. That means "just don't save on flip" would break Apply. The correct fix
is to remember the last-applied state and, on cancel, roll both memory and disk back to it.

---

## 3. Edits

### Edit A — `wwwroot/css/app.css`  (new inline-error + hint styles)

Add two new rules. Match the existing `.um-error` look (red text on a faint red pill). Put
them near the other `.pw-*` styles:

```css
.pw-error {
    font-size: 13px;
    color: rgba(255, 140, 140, 0.92);
    text-align: center;
    padding: 10px 14px;
    border-radius: 12px;
    background: rgba(255, 120, 120, 0.07);
    margin-top: 10px;
}

.pw-hint {
    font-size: 12px;
    color: rgba(255, 255, 255, 0.55);
    text-align: center;
    line-height: 1.55;
    margin-top: 10px;
}
```

### Edit B — `Components/Pages/Settings.razor`

**B1. Add two state fields** alongside the existing string fields (near `_newPassword` etc.):

```csharp
private string _setupError  = "";   // inline error for the Guardian setup form
private string _changeError = "";   // inline error for the Change Password form
```

**B2. Guardian setup form — show the hint + error.** In the Guardian setup branch (the
`else if (_selectedMode == RecoveryMode.Guardian)` block), find the password inputs
`<div class="pw-field-group"> … </div>` and the "Set Password" button. Insert, **between**
the field group and the Set Password button:

```razor
<div class="pw-hint">
    Password must be at least 8 characters and include a letter, a number,
    and a special character.
</div>

@if (!string.IsNullOrEmpty(_setupError))
{
    <div class="pw-error">@_setupError</div>
}
```

**B3. Change Password form — show the error.** In the "Guardian Mode Active" branch, find the
three-input `<div class="pw-field-group"> … </div>` under "Change Password" and the "Change
Password" button. Insert, **between** them:

```razor
@if (!string.IsNullOrEmpty(_changeError))
{
    <div class="pw-error">@_changeError</div>
}
```

**B4. Populate `_setupError`** in the setup handler `SetPasswordAsync()`. Clear it at the
top, and set it on the failure paths (keep the snackbars):

```csharp
private async Task SetPasswordAsync()
{
    _setupError = "";
    try
    {
        var (success, message) = await PasswordService.SetPasswordAsync(
            _newPassword, _confirmPassword, _guardianHash);

        if (success)
        {
            Snackbar.Success("Password set", message);

            _newPassword          = "";
            _confirmPassword      = "";
            _guardianHash         = "";
            _selectedMode         = null;
            _passwordEnabled      = PasswordService.IsPasswordEnabled();
            _accountabilityActive = PasswordService.IsAccountabilityModeActive();
            StateHasChanged();
        }
        else
        {
            _setupError = message;                       // inline red text
            Snackbar.Error("Failed to set password", message);
        }
    }
    catch (Exception ex)
    {
        _setupError = ex.Message;
        Snackbar.Error("Unexpected error", ex.Message);
    }
}
```

> Note: the original method reset the inputs inside a second `if (success)` block. Fold that
> into the single `if (success)` above (as shown) so the logic reads once. Behaviour is
> unchanged on success.

**B5. Populate `_changeError`** in `ChangePasswordAsync()`. Clear at the top; set it on the
wrong-current-password path and on the new-password validation failure (keep the snackbars):

```csharp
private async Task ChangePasswordAsync()
{
    _changeError = "";

    bool currentCorrect = PasswordService.VerifyPassword(_currentPassword);
    if (!currentCorrect)
    {
        _changeError = "Current password is incorrect.";
        Snackbar.Error("Incorrect password", "Current password is incorrect.");
        return;
    }

    var (success, message) = await PasswordService.SetPasswordAsync(
        _newPassword, _confirmPassword);

    if (success)
    {
        Snackbar.Success("Password changed", message);
        _currentPassword = "";
        _newPassword     = "";
        _confirmPassword = "";
    }
    else
    {
        _changeError = message;
        Snackbar.Error("Failed to change password", message);
    }
}
```

**B6. Clear stale errors on mode switch.** In `OnAccountabilityModeSelected()` and
`OnGuardianModeSelected()`, add `_setupError = "";` so a leftover error doesn't linger when
the user flips between the two mode cards.

### Edit C — `Components/Pages/Blocking.razor`  (toggle reverts on cancel)

The idea: keep a snapshot of the **last-applied** preset state — set it once on load and
refresh it after every successful apply. On cancel, restore the toggles (memory **and** disk)
from that snapshot.

**C1. Rename the snapshot field and fix its comment.** Replace the `_preApplySnapshot` field
(and its comment block) with:

```csharp
/*
 * _appliedPresetsSnapshot holds the last preset state that was successfully
 * applied (seeded from the initial load). If the user flips category toggles
 * and then cancels the unlock modal, OnModalCancelled restores this snapshot to
 * both memory and disk so the switches reflect what is actually applied.
 */
private PresetsRoot? _appliedPresetsSnapshot = null;
```

**C2. Seed the snapshot on load.** In `OnInitializedAsync`, right after `_categoryCount` is
set, add:

```csharp
_appliedPresetsSnapshot = DeepCopy(_presets);
```

**C3. Stop snapshotting at apply-time; refresh it on success instead.**

- In `OnApplyClicked`, **delete** the line `_preApplySnapshot = DeepCopy(_presets);` (the one
  just before `_showUnlockModal = true;`). Leave `_showUnlockModal = true; StateHasChanged();`.
- In `OnApplyClicked`'s direct (no-auth) success branch, after
  `_changesApplied = true;`, add: `_appliedPresetsSnapshot = DeepCopy(_presets);`
- In `OnUnlocked`, **delete** the line `_preApplySnapshot = null;`. In its success branch,
  after `_changesApplied = true;`, add: `_appliedPresetsSnapshot = DeepCopy(_presets);`

**C4. Revert on cancel.** Replace `OnModalCancelled` with an async version that restores the
snapshot to memory and disk:

```csharp
/*
 * OnModalCancelled()
 * The user backed out of the unlock modal without authenticating. Restore the
 * category toggles to the last-applied state — in memory and on disk — so the
 * switches never show unapplied pending changes.
 */
private async Task OnModalCancelled()
{
    _showUnlockModal = false;

    if (_appliedPresetsSnapshot is not null)
    {
        _presets       = DeepCopy(_appliedPresetsSnapshot);
        _categoryCount = _presets?.Categories?.Count ?? 0;
        await PresetService.SaveAsync(_presets);
        StateHasChanged();
    }
}
```

> **Custom-site toggles are intentionally NOT reverted here.** Custom Add/Remove and the
> per-site enable toggle all persist immediately through `CustomSitesService`, and they share
> one `CustomsRoot`. Reverting it on cancel could delete a site the user deliberately added.
> Fixing custom-toggle revert safely is a separate follow-up; do not attempt it in this slice.
> Leave `ToggleCustomEnabledAsync`, `AddCustomAsync`, and `RemoveCustomAsync` unchanged.

**C5.** Confirm no `_preApplySnapshot` references remain anywhere in the file after the rename.

---

## 4. Docs to update

- **`CLAUDE.md`** → "Known Bugs / Tech Debt" #3 ("Dead snapshot logic"): this is now fixed
  for category toggles. Reword to note the last-applied snapshot reverts category toggles on
  cancel, and that custom-site toggle revert remains a follow-up.
- Leave `PLAN.md` Phase 0 items as-is (these three are polish under the broader Phase 0 work,
  not their own checkbox) — unless you see a clean place to note the toggle-revert fix.

---

## 5. Verify

- **Build:** attempt `dotnet build -f net9.0-windows10.0.19041.0` if the Windows workload is
  present. This repo's environment is Linux/WSL where the MAUI **Windows** build likely will
  not run — if it fails for that reason, that's expected; instead re-read every edited file
  carefully (Razor markup balanced, `OnModalCancelled` now `async Task`, no leftover
  `_preApplySnapshot`, `@code` braces balanced).
- **Do NOT** run git, commit, or push.

### Manual test checklist for the human (include in your report)

Run on Windows (no admin needed for 1–4; admin only if you want the real block in 5):

1. **Setup validation text:** Settings → Guardian Mode → type a weak password (e.g. `abc`) →
   Set Password → red inline text appears stating the specific rule; the requirements hint is
   always visible.
2. **Setup success clears it:** enter a valid password → red text gone, form collapses to the
   "Guardian Mode Active" view.
3. **Wrong current password:** in Guardian Mode Active → Change Password → wrong current
   password → red inline "Current password is incorrect." appears.
4. **Toggle revert on cancel:** with a protection mode active, flip a category toggle ON →
   Apply Blocking Rules → unlock modal appears → Cancel → the toggle snaps back to its
   previous position (and stays reverted after navigating away and back).
5. **Toggle sticks on success:** flip a category ON → Apply → authenticate successfully →
   toggle stays ON and the snapshot updates (flip another, cancel → reverts to this new
   applied state, not the original).

---

## 6. When finished

Summarize for the human: files changed (one line each), anything that didn't match this spec
and how you adapted, the manual checklist above, and a reminder that build/run happens on
Windows and they do the push.
