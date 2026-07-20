# Nimbus DNS Blocker — Claude Code Context

> Roadmap and business plan live in **PLAN.md**. This file describes what the code *is*;
> PLAN.md describes where it's *going*. Keep both updated when features land.

## What This App Does

Nimbus DNS Blocker is a **Windows desktop application** built with .NET MAUI + Blazor WebView
that blocks distracting websites at the system level by writing a managed section into the
Windows hosts file. It targets people dealing with digital compulsivity, focus issues, or
parental-control needs.

Blocking works by redirecting domains to `0.0.0.0` / `::` in the hosts file and flushing the
DNS cache via `ipconfig /flushdns`. The app is **local-first** — no backend, no accounts,
no telemetry. That privacy stance is a product decision, not an accident; don't add
network calls or telemetry without an explicit request.

**Known honest limitation:** the app requires Administrator to apply rules, so the user can
always bypass it (edit hosts directly, clear AppData/Preferences). Blocking is *friction*,
not enforcement. Browser DNS-over-HTTPS currently bypasses hosts-file blocking entirely —
fixing that via browser policy registry keys is a top roadmap item (see PLAN.md).

---

## Tech Stack

|Layer|Technology|
|---|---|
|Framework|.NET MAUI (net9.0; Windows is the only real target — see Tech Debt)|
|Language|C# (nullable enabled, implicit usings)|
|UI|Blazor WebView (Razor components in `.razor` files)|
|Styling|Custom CSS — neumorphic design in `wwwroot/css/app.css`|
|Storage|JSON via `System.Text.Json` in `%LOCALAPPDATA%`; flags/hash in MAUI `Preferences`|
|DI|`Microsoft.Extensions.DependencyInjection` via `MauiProgram.cs`|
|Password hashing|PBKDF2 via `Rfc2898DeriveBytes.Pbkdf2` (`Utilities/PasswordHash.cs`)|

---

## Project Structure (actual, verified)

```
Nimbus-DNS-Blocker/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor         # App shell with sidebar nav
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   ├── Home.razor               # Dashboard — quote of the day
│   │   ├── Blocking.razor           # Category toggles, custom sites, Apply button
│   │   └── Settings.razor           # Protection mode setup + Restore/Unblock All
│   ├── Shared/
│   │   ├── UnlockModal.razor        # Auth gate — routes to password/accountability/guardian views
│   │   ├── AccountabilityFlow.razor # 5-question grounding flow with timed delays
│   │   └── GuardianFlow.razor       # Recovery-code flow (KNOWN FLAWED — see Tech Debt)
│   ├── Routes.razor
│   └── _Imports.razor
├── Models/
│   ├── PresetsRoot.cs               # Categories → entries (host/ipv4/ipv6)
│   ├── CustomsRoot.cs               # User-added custom sites
│   └── JsonLoad.cs                  # EMPTY placeholder — delete or use
├── Services/
│   ├── HostsFileService.cs          # Hosts read/write/backup/splice/restore + DNS flush (DONE)
│   ├── IHostsFileService.cs
│   ├── BrowserPolicyService.cs      # HKLM Secure-DNS-off policies (Chrome/Edge/Firefox) (DONE)
│   ├── IBrowserPolicyService.cs
│   ├── PresetService.cs             # Category blocklist load/save/normalize (no interface yet)
│   ├── CustomSitesService.cs        # Custom sites CRUD (no interface yet)
│   ├── PasswordService.cs           # Modes, PBKDF2 hash, guardian code generation
│   ├── IPasswordService.cs
│   ├── QuoteService.cs              # Random quote per app launch (not truly daily)
│   └── SnackbarService.cs           # Toast notifications (ISnackbarService)
├── Shared/
│   └── SnackbarHost.razor           # Renders snackbar messages
├── Utilities/
│   ├── AtomicFile.cs                # Crash-safe temp-file + File.Replace writes (DONE)
│   ├── HostValidation.cs            # Shared NormalizeHost — used by both config services (DONE)
│   ├── HostsSection.cs              # Pure hosts-block splice logic, extracted for testability (DONE)
│   └── PasswordHash.cs              # PBKDF2 hash/verify, self-describing v1 string (DONE)
├── Resources/Raw/
│   ├── presets.seed.json            # Default categories (ships with app)
│   └── custom.seed.json             # Default custom sites
├── wwwroot/
│   ├── css/app.css                  # All styles — do not inline styles in Razor
│   ├── css/js/rain.js               # Background effect (yes, JS lives under css/ — move someday)
│   └── index.html
├── Platforms/                       # Android/iOS/MacCatalyst/Tizen scaffolding — unused, Windows only
├── Nimbus.Tests/                    # Plain net9.0 xUnit project — compiles Utilities/*.cs directly
│                                     # (HostValidation, HostsSection, PasswordHash), no MAUI
│                                     # dependency, runs on Windows or Linux (DONE)
├── MauiProgram.cs                   # Entry point + DI registrations
└── App.xaml.cs
```

---

## Architecture Rules — Always Follow These

### Service Layer Pattern
- All business logic lives in `Services/`. Razor components call services — they do not
  contain logic directly.
- Services are registered as **Singletons** in `MauiProgram.cs`.
- Always inject via constructor or `@inject` — never `new` a service directly.
- Create an interface for any service that touches the file system or OS (testability).
  `PresetService` and `CustomSitesService` currently violate this — fix when touched.

### Async/Await
- All file I/O and any potentially slow operation must be `async Task` — no `.Result`
  or `.Wait()` blocking calls.
- UI-bound state changes call `StateHasChanged()` after async updates if needed.

### Data Persistence — Seed File Pattern
- App ships with `*.seed.json` in `Resources/Raw/`. On first run the service copies the
  seed to `%LOCALAPPDATA%\...\Data\<name>.json`. All runtime reads/writes go to the live
  file, never the seed.
- **Saves should be atomic** (write temp file, then `File.Replace`) — not yet implemented,
  do it this way when touching save paths.
- Never let a failed load silently produce an empty root that a later save persists —
  that is a data-loss path (see Tech Debt).

### URL / Domain Normalization
- Strip protocol (`https://`), ports (`:443`), and paths (`/foo`) from user input.
- Lowercase all domains, trim trailing dots, deduplicate case-insensitively.
- Normalize before saving, not after loading.

### User Feedback
- All user-facing success/error feedback goes through `ISnackbarService`.
- Never `Console.WriteLine`/`Debug.WriteLine` as the *only* record of a user-affecting
  failure — surface it.
- Operations that can fail must report success/failure to the caller (return `bool` or a
  result type), not swallow exceptions and return `void`.

---

## Key Data Models

```csharp
public sealed class PresetsRoot
{
    public Dictionary<string, PresetCategory> Categories { get; set; } = new();
}

public sealed class PresetCategory
{
    public bool Enabled { get; set; }
    public List<PresetEntry> Entries { get; set; } = new();
}

public sealed class PresetEntry
{
    public string Host { get; set; } = "";   // e.g. "facebook.com"
    public string Ipv4 { get; set; } = "0.0.0.0";
    public string Ipv6 { get; set; } = "::";
}

public sealed class CustomsRoot
{
    public List<CustomEntry> Sites { get; set; } = new();
}

public sealed class CustomEntry   // Enabled is bool? — null means "default to enabled"
{
    public bool?  Enabled { get; set; }
    public string Host    { get; set; } = "";
    public string Ipv4    { get; set; } = "0.0.0.0";
    public string Ipv6    { get; set; } = "::";
}
```

---

## Critical File Paths

```
Hosts file:       C:\Windows\System32\drivers\etc\hosts
Hosts backup:     C:\Windows\System32\drivers\etc\hosts.nimbus.bak   (one-time, pre-Nimbus state)
AppData base:     %LOCALAPPDATA%\...\com.companyname.nimbusinternetblocker\Data\
Presets config:   ...Data\presets.json
Custom config:    ...Data\custom.json
Password state:   MAUI Preferences (keys prefixed nimbus_)
```

## Hosts File Format

Nimbus owns a clearly delimited section. **Never touch lines outside this section.**

```
# --- Nimbus-managed section BEGIN ---
0.0.0.0         facebook.com
::              facebook.com
0.0.0.0         www.facebook.com
::              www.facebook.com
# --- Nimbus-managed section END ---
```

- Both apex and `www.` variants are written for each entry (skip `www.` if host already has it).
- Write UTF-8 without BOM. After every write, run `ipconfig /flushdns` hidden (non-fatal on failure).
- The splice logic (`HostsFileService.SpliceSection`) normalizes CRLF/LF and replaces only
  the delimited block; if markers are missing it appends a new block.

---

## Password Protection — How It Actually Works

Two mutually exclusive modes, stored in MAUI Preferences (`PasswordService`):

- **Accountability mode** — no password. Applying rules opens `UnlockModal`, which routes
  to `AccountabilityFlow`: 5 questions with a 3-second delay each; answering "urge" (Q3)
  or "no" (Q4) intentionally cancels the flow; Q5 requires typing the daily quote (paste
  blocked via JS interop).
- **Guardian mode** — password (PBKDF2 hash in Preferences) gates Apply. "Forgot password"
  routes to `GuardianFlow`.

✅ **Guardian recovery is now verifiable.** At Guardian setup, the displayed recovery code's
PBKDF2 hash is stored in Preferences (`nimbus_guardian_recovery_hash`) via
`SetPasswordAsync(password, confirmPassword, recoveryCode)`. `GuardianFlow` no longer
generates or displays a code during recovery — it asks the user to type the code they saved
at setup and verifies it against the stored hash via `VerifyRecoveryCode`. Changing the
password (no `recoveryCode` passed) preserves the original recovery hash. Guardian users who
set up *before* this fix have no stored hash and must remove + re-add Guardian mode to get a
verifiable code.

---

## DI Registration (MauiProgram.cs) — current

```csharp
builder.Services.AddSingleton<ISnackbarService, SnackbarService>();
builder.Services.AddSingleton<QuoteService>();
builder.Services.AddSingleton<PresetService>();
builder.Services.AddSingleton<CustomSitesService>();
builder.Services.AddSingleton<IBrowserPolicyService, BrowserPolicyService>();  // Windows-only, CA1416 suppressed
builder.Services.AddSingleton<IHostsFileService, HostsFileService>();  // Windows-only, CA1416 suppressed
builder.Services.AddSingleton<IPasswordService, PasswordService>();
```

---

## What's Done

- [x] Category-based blocking UI + JSON persistence with seed pattern
- [x] `HostsFileService` — backup, delimited-section splice, DNS flush, elevation check
- [x] Apply button wired end-to-end (with auth gate when a protection mode is active)
- [x] `CustomSitesService` — add/remove/toggle custom sites, integrated with apply
- [x] Password protection: Accountability + Guardian modes, UnlockModal, flows
- [x] Verifiable Guardian recovery — setup-time code hash stored (PBKDF2) and checked
      against the typed code at recovery, instead of the old "regenerate and compare" theater
- [x] Snackbar notifications, quote system, neumorphic CSS
- [x] Truthful apply feedback — `ApplyAsync` returns `bool`, never throws
- [x] Pending-until-apply category and custom-site changes — toggles, adds, and removes
      are in-memory only until Apply succeeds; disk always reflects the last-applied
      state, so Cancel and page navigation both discard unapplied changes honestly
- [x] Safe persistence — nullable `LoadAsync` (never masks a corrupt file as empty),
      atomic `SaveAsync` via `Utilities/AtomicFile.cs`, save failures surfaced
- [x] Unit tests for `HostsSection.Splice` and `HostValidation.NormalizeHost` — both
      extracted from their MAUI-bound services into `Utilities/` so a plain net9.0 xUnit
      project (`Nimbus.Tests/`) can compile and test them without the MAUI workload
- [x] Restore / Unblock All — Settings button removes the Nimbus hosts section
      (`HostsSection.Remove`, unit tested), disables every category and custom site in
      sync so disk keeps describing the applied state, gated behind the unlock modal when
      a protection mode is active, flushes DNS
- [x] DoH bypass fix — `BrowserPolicyService` writes/removes HKLM Secure-DNS-off policies
      for Chrome, Edge, and Firefox alongside Apply/Restore, with a Settings toggle and
      surgical (match-before-delete) policy removal
- [x] Dropped the EOL/legacy Identity packages — `PasswordHasher<T>` replaced by
      `Utilities/PasswordHash` (PBKDF2-HMAC-SHA256, 600k iterations, self-describing
      `v1.iterations.salt.subkey` string), covered by `Nimbus.Tests/PasswordHashTests.cs`

## Known Bugs / Tech Debt (verified against code — fix these before new features)

1. ~~**Guardian recovery is security theater**~~ — **RESOLVED.** See Password Protection
   section above.
2. ~~**Contradictory apply feedback**~~ — **RESOLVED.** `IHostsFileService.ApplyAsync`
   returns `Task<bool>` (never throws); `Blocking.razor` branches on the result to set the
   status line instead of assuming success.
3. ~~**Dead snapshot logic**~~ — **RESOLVED, full parity.** The slice-002 snapshot approach
   (`_appliedPresetsSnapshot`/`DeepCopy`) had a hole: category flips saved to disk
   immediately, so disk could hold pending (unapplied) state that got baked into the
   snapshot as if applied. Fixed by changing the invariant instead: category flips
   (`OnCategoryChanged`) are pending, in-memory only — nothing is saved on flip. Custom-site
   toggle, add, and remove (`ToggleCustomEnabled`, `AddCustom`, `RemoveCustom`,
   `CustomSitesService.AddSite`/`RemoveSite`) got the same treatment — the whole
   `CustomsRoot` is one file, so partial persistence there would silently break the
   invariant, making all-or-nothing pending state the only coherent design.
   `Blocking.razor`'s `ApplyPendingAsync` is the *only* place that writes either config
   file, immediately before the hosts write, so disk always equals the last-applied state.
   Cancel (`OnModalCancelled`) and navigation both just reload from disk, discarding any
   pending flips/adds/removes by design. All snapshot machinery was deleted.
4. ~~**Data-loss path**~~ — **RESOLVED.** `PresetService.LoadAsync`/`CustomSitesService.LoadAsync`
   return `null` on read/parse failure instead of an empty root — callers (`Blocking.razor`,
   `HostsFileService.ApplyAsync`) treat `null` as "do not touch disk" and abort with a
   snackbar rather than saving over the damaged file. `SaveAsync` on both services returns
   `bool` and writes atomically via `Utilities/AtomicFile.cs` (temp file + `File.Replace`),
   so a failed write never truncates the live file, and callers surface save failures.
5. **~120 lines duplicated** between `PresetService` and `CustomSitesService` — the
   `NormalizeHost` half is now resolved: both services call the shared
   `Utilities/HostValidation.NormalizeHost` instead of keeping private copies (pulled
   forward from Phase 2 by slice 005, since it needed to be testable outside MAUI). The
   copy-paste artifacts are also fixed: `CustomSitesService`'s fallback seed shape is now
   `{"sites":[]}` (was wrongly `{"categories":{}}`), and its `LoadAsync`/`SaveAsync` debug
   log messages now name `CustomSitesService` instead of `PresetService`. Remaining dedup
   (seed plumbing — `EnsureLiveFileExistsAsync`, `ReadSeedTextAsync`) is still open for
   Phase 2.
6. ~~**Wrong packages**~~ — **RESOLVED.** `Microsoft.AspNetCore.Identity` 2.3.9 (EOL) and
   `Microsoft.AspNet.Identity.Core` 2.2.4 (legacy) are both removed from the csproj.
   `PasswordHasher<T>` is replaced by `Utilities/PasswordHash` (PBKDF2-HMAC-SHA256,
   600,000 iterations, self-describing `v1.iterations.salt.subkey` string). Migration:
   any hash not in the `v1.` format fails verification rather than being read — there
   were no production hashes to preserve pre-release, so anyone with a password/recovery
   code set on an earlier build must re-set it (see Release Notes in PLAN.md).
7. **Phantom platforms** — csproj targets android/ios/maccatalyst; `HostsFileService`
   would throw on them. Trim to the Windows TFM.
8. ~~**No restore/unblock-all feature**~~ — **RESOLVED.** Settings has a "Restore / Unblock
   All" button (`IHostsFileService.RestoreAsync`, backed by the pure `HostsSection.Remove`).
   It removes the entire Nimbus-managed section from the hosts file — the pre-existing
   `hosts.nimbus.bak` is left untouched as a manual escape hatch, never copied back, since
   that could clobber hosts edits the user made since Nimbus first ran. Both config files
   are then rewritten with every category and custom site set to `Enabled = false`,
   preserving list membership so the disk-equals-applied-state invariant (slices 003/004)
   holds after the unblock. Gated behind the unlock modal when a protection mode is active,
   same as Apply; does not remove the protection mode itself.
9. ~~**DoH bypass**~~ — **RESOLVED.** `IBrowserPolicyService`/`BrowserPolicyService` write
   three HKLM policy values to force browser Secure DNS off:
   `SOFTWARE\Policies\Google\Chrome\DnsOverHttpsMode` = `"off"`,
   `SOFTWARE\Policies\Microsoft\Edge\DnsOverHttpsMode` = `"off"`, and
   `SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS\Enabled` = `0` (DWORD). Lifecycle:
   written (or removed, if the preference is off) during `HostsFileService.ApplyAsync`;
   always removed during `RestoreAsync`. Removal is surgical — a value is only deleted
   when its current data still equals what Nimbus wrote, so an administrator's own policy
   is never clobbered; the Firefox `DNSOverHTTPS` subkey is deleted only when Nimbus's
   value was the last thing in it. Preference lives in MAUI Preferences
   (`nimbus_doh_policies_enabled`, default **true**) and is surfaced as a Settings toggle
   that only stores the preference — the registry itself is touched at apply/restore time,
   inside the already-elevated hosts-file path. A policy write/removal failure is a warning
   snackbar only; it never fails the apply or restore. Windows-only
   (`[SupportedOSPlatform("windows")]`), same as `HostsFileService`.
10. ~~**No tests**~~ — **RESOLVED for the pure functions.** `SpliceSection` (now
    `Utilities/HostsSection.Splice`), `NormalizeHost` (now
    `Utilities/HostValidation.NormalizeHost`), and `PasswordHash.Hash`/`Verify` were
    extracted into `Utilities/` — all were previously `private`/MAUI-bound (or, for
    `PasswordHash`, wrapped a MAUI-adjacent Identity type), so a plain test project
    couldn't reach them otherwise. Covered by `Nimbus.Tests/` (plain `net9.0` xUnit
    project, compiles the three utility files directly, no MAUI/service dependency —
    runs on Windows or Linux). No behavior changed by the extraction. Other code paths
    (services, Razor components) remain untested — out of scope for Phase 0.
11. Misc: `presets.seed.json` has a stray `"exclude"` block at the top; empty
    `JsonLoad.cs`/`HostValidation.cs`; `rain.js` lives under `css/`; quote is per-launch
    random, not per-day; stale personal-path comments in services.

## What's NOT Done (see PLAN.md for order and rationale)

- [ ] Track 1 v1.0: bug fixes above + restore/unblock-all + DoH policy fix + rename + release
- [ ] Track 2 (paid, gated on validation): Windows service, timed/locked sessions,
      browser extension, verified Guardian mode, stats

---

## Coding Style

- `var` where the type is obvious; expression-bodied members for one-liners.
- XML doc comments on public service methods.
- No magic strings — `const` for section markers, file names, Preferences keys.
- Wrap file I/O in try/catch and surface errors via `ISnackbarService`.
- Comments state *invariants and why*, not C# tutorials or narration of the next line.
  Delete tutorial-style comments when editing a file; never let a comment describe
  behavior the code doesn't have.

## What NOT To Do

- Do not put business logic in Razor components — move it to a service.
- Do not write to the hosts file without the one-time backup existing first.
- Do not modify lines outside the Nimbus-managed section in the hosts file.
- Do not block the UI thread — everything async.
- Do not add NuGet packages without checking `System.*` first (see Tech Debt #6 for how
  this went wrong once already).
- Do not store passwords or recovery codes in plaintext — PBKDF2 hash only.
- Do not inline CSS in Razor — all styles belong in `wwwroot/css/app.css`.
- Do not add telemetry, accounts, or network calls — local-first is the product.
