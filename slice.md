# Slice 001 — Guardian Recovery Fix (verifiable recovery code)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> described below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or
> run any `git` command** — the human handles all pushes. When done, report what you changed
> and the manual test checklist at the bottom.

---

## 1. Goal

Fix the single worst flaw in the app: **Guardian recovery is security theater.**

Today the recovery code is *generated fresh at recovery time* and shown on screen for the
user to retype — it verifies nothing. This slice makes the recovery code a real secret:

- **At Guardian setup**, hash the recovery code shown to the user (same PBKDF2 machinery as
  the password) and store the hash in MAUI Preferences.
- **At recovery**, the user types the code they saved during setup; verify the typed code
  against the stored hash. No code is ever displayed during recovery.

This is Phase 0, item 1 in `PLAN.md`, and Tech Debt item #1 in `CLAUDE.md`.

**Scope discipline:** change ONLY what is needed for verifiable guardian recovery. Do not
rename services, do not touch the ASP.NET Identity package swap (that is Phase 2), do not
refactor unrelated code, do not add unit tests (separate Phase 0 item).

---

## 2. Before you start

Read these files in full so your edits match the real current state (line numbers below are
guidance, not guarantees — match on content, not position):

- `Services/IPasswordService.cs`
- `Services/PasswordService.cs`
- `Components/Pages/Settings.razor`
- `Components/Shared/GuardianFlow.razor`
- `Components/Shared/UnlockModal.razor` (context only — you should NOT need to edit it)

**Decisions already made for you (do not deviate):**

1. **Hashing:** use the *existing* `PasswordHasher<string>` (ASP.NET Identity) — the same
   machinery the password uses today. When Phase 2 swaps this for
   `Rfc2898DeriveBytes.Pbkdf2`, both the password and recovery hashes move together.
2. **Storage key:** new Preference key `nimbus_guardian_recovery_hash`.
3. **Integration point:** extend `SetPasswordAsync` with an *optional* third parameter
   `string? recoveryCode = null`. Initial Guardian setup passes the code; the
   change-password path passes nothing, so the original recovery code the guardian holds is
   preserved across password changes. This is deliberate — recovery clears the password, so
   the same code must keep working after a password change.
4. **Paste prevention in `GuardianFlow`: KEEP it.** Leave the existing `OnAfterRenderAsync`
   paste-block untouched. Removing it is a separate UX decision, out of scope here.
5. **No migration shim.** Guardian users who set up before this fix have no stored recovery
   hash; `VerifyRecoveryCode` returns `false` for them and recovery is unavailable until
   they remove + re-add Guardian mode. This is acceptable (their old recovery was fake
   anyway). It is called out in the release-notes item below — do not build a fallback.

---

## 3. Edits

### Edit A — `Services/IPasswordService.cs`

**A1.** Change the `SetPasswordAsync` signature to add the optional recovery-code param:

```csharp
Task<(bool success, string message)> SetPasswordAsync(
    string password,
    string confirmPassword,
    string? recoveryCode = null);
```

Update its doc comment to note: "During initial Guardian setup, pass the one-time recovery
code so its PBKDF2 hash is stored for later verification. Omit it when merely changing the
password — the existing recovery code is preserved."

**A2.** Add a new method to the `// ── Password Operations ──` section:

```csharp
/*
 * VerifyRecoveryCode()
 * Checks a typed recovery code against the PBKDF2 hash stored during Guardian
 * setup. Returns true only on an exact match. Returns false when no recovery
 * hash is stored (Guardian mode set up before recovery verification existed)
 * or when the code is wrong.
 */
bool VerifyRecoveryCode(string attempt);
```

**A3.** Update the `GenerateGuardianHash()` doc comment. The line claiming the code is
"NEVER stored anywhere" is now wrong — the *plaintext* code is still never stored, but its
*hash* is. Reword to:

> Generates a cryptographically random one-time recovery code in the format
> `xxxxxx-xxxxxx-xxxxxx-xxxxxx`. The plaintext code is shown once during Guardian setup and
> never stored; its PBKDF2 hash is persisted (see `SetPasswordAsync`) so a typed code can be
> verified later via `VerifyRecoveryCode`.

### Edit B — `Services/PasswordService.cs`

**B1.** Add a new Preference key alongside the others (in the `// ── Preference Keys ──`
block):

```csharp
private const string PREF_RECOVERY_HASH  = "nimbus_guardian_recovery_hash";
```

**B2.** Change the `SetPasswordAsync` signature to match the interface and store the recovery
hash on the success path. Keep ALL existing validation exactly as-is. After the four existing
`Preferences.Set(...)` calls and before `return Task.FromResult((true, ...))`, add the
recovery-hash store:

```csharp
public Task<(bool success, string message)> SetPasswordAsync(
    string password, string confirmPassword, string? recoveryCode = null)
{
    // ── Validation ──  (UNCHANGED — keep every existing check exactly)
    ...

    // ── Hashing ──  (UNCHANGED)
    var hash = new PasswordHasher<string>().HashPassword("nimbus", password);

    // ── Persist ──  (UNCHANGED four Set calls)
    Preferences.Set(PREF_HASH, hash);
    Preferences.Set(PREF_ENABLED, true);
    Preferences.Set(PREF_ACCOUNTABILITY, false);
    Preferences.Set(PREF_RECOVERY, nameof(RecoveryMode.Guardian));

    // Store the recovery-code hash only during initial Guardian setup.
    // Change-password calls pass no recoveryCode, preserving the original code
    // the guardian already holds.
    if (!string.IsNullOrWhiteSpace(recoveryCode))
    {
        var recoveryHash = new PasswordHasher<string>().HashPassword("nimbus", recoveryCode);
        Preferences.Set(PREF_RECOVERY_HASH, recoveryHash);
    }

    return Task.FromResult((true, "Password set successfully."));
}
```

**B3.** Add the `VerifyRecoveryCode` implementation (put it right after `VerifyPassword`):

```csharp
/*
 * VerifyRecoveryCode()
 * Re-hashes the typed code with the salt embedded in the stored recovery hash
 * and compares. Trims surrounding whitespace but preserves case and dashes,
 * which are significant. Returns false when no recovery hash is stored.
 */
public bool VerifyRecoveryCode(string attempt)
{
    var storedHash = Preferences.Get(PREF_RECOVERY_HASH, string.Empty);
    if (string.IsNullOrEmpty(storedHash)) return false;

    var result = new PasswordHasher<string>()
        .VerifyHashedPassword("nimbus", storedHash, attempt.Trim());

    return result == PasswordVerificationResult.Success ||
           result == PasswordVerificationResult.SuccessRehashNeeded;
}
```

**B4.** In `ClearPasswordAsync()`, also remove the recovery hash so removing protection wipes
everything:

```csharp
Preferences.Remove(PREF_RECOVERY_HASH);
```

**B5.** Update the `GenerateGuardianHash()` code comment (the "This hash is NEVER stored"
line) to match the interface wording from Edit A3.

### Edit C — `Components/Pages/Settings.razor`

Only ONE functional change. In the **initial-setup** handler `SetPasswordAsync()` (the one
under the "Guardian setup" mode picker — NOT `ChangePasswordAsync`), pass the displayed
recovery code into the service call so its hash is stored:

```csharp
var (success, message) = await PasswordService.SetPasswordAsync(
    _newPassword, _confirmPassword, _guardianHash);
```

- Leave the rest of that method unchanged. It already clears `_guardianHash` *after* this
  call on success — that ordering is correct, keep it.
- **Do NOT change `ChangePasswordAsync()`.** It must keep calling
  `SetPasswordAsync(_newPassword, _confirmPassword)` with no recovery code, so a password
  change preserves the original recovery code.

### Edit D — `Components/Shared/GuardianFlow.razor`

Replace the whole file with the version below. Key differences from the current file:
- No `_generatedHash` field, no `OnInitialized()` generating a code, no on-screen code box.
- The banner now instructs the user to enter the code they saved at setup.
- `OnConfirmClickedAsync` verifies via `PasswordService.VerifyRecoveryCode(...)` instead of
  an exact string match against a freshly generated code.
- Paste prevention (`OnAfterRenderAsync`) is KEPT unchanged.

```razor
@inject IPasswordService PasswordService
@inject IJSRuntime JS

<div class="gf-container">

    <!-- Instruction banner -->
    <div class="gf-warning">
        Enter the recovery code you saved when you set up Guardian mode.
        Your guardian has a copy of this code.
    </div>

    <!-- Input label -->
    <div class="gf-input-label">Recovery code</div>

    <!-- Answer input — paste is disabled via JS interop in OnAfterRenderAsync -->
    <input class="input gf-input"
           id="guardian-input"
           type="text"
           placeholder="XXXXXX-XXXXXX-XXXXXX-XXXXXX"
           autocomplete="off"
           spellcheck="false"
           @bind="_typedHash"
           @bind:event="oninput" />

    <!-- Inline error message -->
    @if (!string.IsNullOrEmpty(_errorMessage))
    {
        <div class="gf-error">@_errorMessage</div>
    }

    <!-- Confirm button — disabled until the user has typed at least one character -->
    <button class="btn btn-primary gf-confirm"
            type="button"
            disabled="@(_typedHash.Length == 0)"
            @onclick="OnConfirmClickedAsync">
        Confirm
    </button>

    <!-- Cancel -->
    <button class="btn btn-ghost gf-cancel"
            type="button"
            @onclick="OnCancelClickedAsync">
        Cancel
    </button>

</div>

@code {
    // ── Parameters ─────────────────────────────────────────────────────────────

    /// <summary>Raised when the typed recovery code verifies against the stored hash.</summary>
    [Parameter] public EventCallback OnFlowCompleted { get; set; }

    /// <summary>Raised when the user clicks Cancel.</summary>
    [Parameter] public EventCallback OnCancelled { get; set; }

    // ── State ──────────────────────────────────────────────────────────────────

    private string _typedHash    = "";
    private string _errorMessage = "";

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    /*
     * OnAfterRenderAsync()
     * Wires the paste-prevention listener on the first render only, after the
     * DOM element with id="guardian-input" is guaranteed to exist.
     */
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await JS.InvokeVoidAsync("eval",
            "document.getElementById('guardian-input').addEventListener('paste', e => e.preventDefault())");
    }

    // ── Handlers ───────────────────────────────────────────────────────────────

    /*
     * OnConfirmClickedAsync()
     * Verifies the typed recovery code against the PBKDF2 hash stored during
     * Guardian setup. On success raises OnFlowCompleted (the parent UnlockModal
     * then clears the password). On failure shows an error and leaves the typed
     * value intact so the user can spot the discrepancy.
     */
    private async Task OnConfirmClickedAsync()
    {
        if (PasswordService.VerifyRecoveryCode(_typedHash))
        {
            await OnFlowCompleted.InvokeAsync();
        }
        else
        {
            _errorMessage = "That recovery code is not correct. Please try again.";
        }
    }

    private async Task OnCancelClickedAsync()
        => await OnCancelled.InvokeAsync();
}
```

> Note: the `gf-hash-box` / `gf-hash-text` CSS classes are no longer used by this component.
> Leave the CSS in `wwwroot/css/app.css` alone — the Guardian *setup* view in `Settings.razor`
> still uses `gf-hash-box` to show the code, so those classes are still needed.

---

## 4. Docs to update (in this repo, part of the slice)

- **`CLAUDE.md`** → "Known Bugs / Tech Debt" #1: this is now fixed. Reword it to reflect that
  the recovery code hash is stored at setup and verified at recovery, and move the item out
  of the active-debt framing (or mark it resolved). Also update the "Password Protection —
  How It Actually Works" section's ⚠ design-flaw note the same way. In "What's Done", add a
  line for verifiable Guardian recovery.
- **`PLAN.md`** → check the box on the Phase 0 "Guardian recovery fix" item (`- [x]`).
- Add a one-line **release-notes caveat** somewhere sensible (e.g. a `## Release Notes` note
  at the bottom of `PLAN.md` Phase 3, or leave a TODO comment): *"Guardian users who set up
  before this build must remove and re-add Guardian mode to get a verifiable recovery code;
  old recovery codes were never stored and cannot be verified."*

---

## 5. Verify

- **Build:** attempt `dotnet build -f net9.0-windows10.0.19041.0` if the Windows workload is
  available. This repo runs in a Linux/WSL environment where the MAUI **Windows** build most
  likely will NOT work — if it errors due to a missing Windows workload/target, that is
  expected. Do a careful manual re-read of every edited file for compile correctness instead
  (signatures match between interface and impl, no leftover references to the removed
  `_generatedHash`, braces balanced).
- **Do NOT** run git, commit, or push.
- Report: the list of files changed, and confirm the `_generatedHash` field and its
  `OnInitialized` are fully gone from `GuardianFlow.razor`.

### Manual test checklist for the human (put this in your report)

Run on Windows, as Administrator:

1. **Setup stores the code:** Settings → Guardian Mode → note the shown recovery code → set a
   valid password. (Behind the scenes its hash is now stored.)
2. **Recovery with correct code:** Apply blocking → Unlock modal → "Forgot your password?" →
   type the **exact** code from step 1 → Confirm → should unlock and clear the password.
3. **Recovery with wrong code:** repeat step 2 with a wrong code → should show
   "That recovery code is not correct" and NOT unlock.
4. **No code shown at recovery:** confirm the recovery screen shows an input only — it must
   NOT display any code.
5. **Password change preserves recovery:** set Guardian mode (code A) → change the password →
   recover using code A → should still work.
6. **Remove wipes it:** remove protection → set Guardian again → old code should no longer
   verify (a new code is now in effect).

---

## 6. When finished

Summarize for the human:
- Files changed (with a one-line description each).
- Anything that didn't match this spec and how you adapted.
- The manual test checklist above.
- Reminder that the build/run verification must happen on Windows, and that they do the push.
