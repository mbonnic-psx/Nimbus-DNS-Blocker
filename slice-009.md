# Slice 009 — Phase 2: Windows-only trim + junk removal

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the checklist at the bottom.

---

## 1. Goal

Nimbus only ever ships on Windows, but the project still carries the full MAUI multi-platform
scaffold (android / ios / maccatalyst / tizen target frameworks and `Platforms/` folders) plus
a handful of dead files and a misplaced script. This is Tech Debt #7 and the file-hygiene half
of #11. This slice strips the project to Windows-only and removes the junk. **No app behaviour
changes** — it's pure structure/hygiene.

**Explicitly in scope (and nothing else):**

1. **Trim `TargetFrameworks` to Windows only** and delete the four unused `Platforms/`
   subfolders.
2. **Delete the empty `Models/JsonLoad.cs`.**
3. **Move `rain.js` out of `wwwroot/css/js/`** into `wwwroot/js/` and fix its one reference.
4. **Remove the stray `"exclude"` block** at the top of `Resources/Raw/presets.seed.json`.

**Decisions already made (do not deviate):**

- **Keep `Platforms/Windows/`.** The Windows MAUI head (`App.xaml`, `app.manifest` — which
  carries the admin-elevation request — `Package.appxmanifest`) is required to build. Delete
  only `Platforms/Android`, `Platforms/iOS`, `Platforms/MacCatalyst`, `Platforms/Tizen`.
- **The `[SupportedOSPlatform("windows")]` attributes stay.** PLAN.md says "remove the CA1416
  pragma" but there is **no** `#pragma warning`/`NoWarn CA1416` anywhere in the repo — the
  Windows-only services (`HostsFileService`, `BrowserPolicyService`) use the
  `[SupportedOSPlatform("windows")]` attribute, which is correct and must remain. That PLAN
  sub-item is already satisfied; do not touch those attributes.
- **No stale personal-path comments exist** in `Services/` (verified — the grep is empty), so
  there is nothing to strip there. Do **not** go hunting for comments to delete in unrelated
  files; comment cleanup in this slice is limited to what's named below.
- **Don't touch app behaviour.** No changes to Razor logic, services, DI, the quote system,
  or CSS beyond the mechanical items above. The daily-quote fix, service dedup/interfaces, and
  the AccountabilityFlow Q5 change are **later slices (010 / 011), not this one.**
- **No rename.** The app keeps its current name — `ApplicationTitle`, `ApplicationId`,
  `RootNamespace` are all left exactly as they are. (Product decision: this is the free
  version, staying as-is.)

---

## 2. Before you start

Read in full: `Nimbus-Internet-Blocker.csproj` (lines 1–43 hold the platform config),
`wwwroot/index.html`, and `Resources/Raw/presets.seed.json` (just the top). Skim
`Platforms/Windows/` so you can see what you're keeping.

---

## 3. Edits

### Edit A — `Nimbus-Internet-Blocker.csproj`: Windows-only target

**A1.** Replace the two `TargetFrameworks` lines (currently lines 4–5):

```xml
<TargetFrameworks>net9.0-android;net9.0-ios;net9.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net9.0-windows10.0.19041.0</TargetFrameworks>
```

with a single line (keep the plural `<TargetFrameworks>` element name — MAUI `SingleProject`
is happy with one value in it, and it's the lowest-risk change):

```xml
<TargetFrameworks>net9.0-windows10.0.19041.0</TargetFrameworks>
```

**A2.** Delete the now-pointless Tizen/MacCatalyst comment blocks that sit just below (the
`<!-- Uncomment to also build the tizen app ... -->`, the `<!-- <TargetFrameworks>...tizen -->`
line, and the `<!-- Note for MacCatalyst: ... -->` block through the
`<!-- For example: <RuntimeIdentifiers>... -->` line). They only describe platforms we no
longer build.

**A3.** In the `SupportedOSPlatformVersion` group (currently lines ~37–42), remove the
conditions for the dead platforms — the `ios`, `maccatalyst`, `android`, and `tizen` lines —
and keep only the two `windows` lines:

```xml
<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</SupportedOSPlatformVersion>
<TargetPlatformMinVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</TargetPlatformMinVersion>
```

(With a single Windows TFM the conditions are always true; you may leave them as-is with the
condition intact — they're harmless — but the dead-platform lines must go.)

Leave everything else in the csproj untouched: `OutputType`, `RootNamespace`, `UseMaui`,
`SingleProject`, `ApplicationTitle`, `ApplicationId`, versions, `WindowsPackageType`, and all
`<ItemGroup>`s (packages, MauiImages, etc.).

### Edit B — delete the four unused platform folders

Remove these directories entirely (keep `Platforms/Windows/`):

```
Platforms/Android/
Platforms/iOS/
Platforms/MacCatalyst/
Platforms/Tizen/
```

Use the filesystem (e.g. `rm -rf`), not git. If any file under them is referenced anywhere
outside those folders, stop and report it instead of deleting — but they're standard MAUI
platform heads and nothing else should reference them.

### Edit C — delete `Models/JsonLoad.cs`

It's an empty placeholder (a `static class JsonLoad {}` with no members). Delete the file.
Confirm first that nothing references the `JsonLoad` type anywhere (`grep -rn "JsonLoad"` over
`.cs`/`.razor`, excluding `obj/`/`bin/`) — expected: no hits outside the file itself. If
there's a reference, stop and report.

### Edit D — move `rain.js` to `wwwroot/js/`

**D1.** Move the file:

```
wwwroot/css/js/rain.js   →   wwwroot/js/rain.js
```

Remove the now-empty `wwwroot/css/js/` directory afterward if nothing else is in it.

**D2.** Fix its single reference in `wwwroot/index.html` (line 27):

```html
<script src="css/js/rain.js"></script>   <!-- before -->
<script src="js/rain.js"></script>       <!-- after -->
```

That's the **only** reference — verified by grep. `MainLayout.razor` calls the
`nimbusRain.start` global that `rain.js` defines; it doesn't reference the path, so no change
there. Don't touch `MainLayout.razor` or the CSS.

### Edit E — strip the `"exclude"` block from `presets.seed.json`

Remove the leading `"exclude": [ ... ],` array (the `**/bin`, `**/node_modules`, etc. glob
list) so the file starts straight at `"categories"`:

```jsonc
{
  "categories": {
    ...
```

This block is a leftover from some tool's config; `PresetsRoot` only has a `Categories`
property, so `System.Text.Json` already ignores `"exclude"` on load — removal is cosmetic and
safe. The already-deployed live file at `%LOCALAPPDATA%\...\Data\presets.json` self-heals
(the property isn't in the model, so the next save drops it); no migration needed. Keep the
rest of the categories/entries byte-for-byte. Validate the file is still well-formed JSON
after the edit.

---

## 4. Docs to update

- **`CLAUDE.md`**
  - **Tech Debt #7** ("Phantom platforms"): mark **RESOLVED** — `TargetFrameworks` trimmed to
    `net9.0-windows10.0.19041.0`, the four non-Windows `Platforms/` folders deleted; note the
    `[SupportedOSPlatform("windows")]` attributes stay (there was never a CA1416 pragma to
    remove).
  - **Tech Debt #11**: strike the resolved items — empty `JsonLoad.cs` deleted, `rain.js`
    moved to `wwwroot/js/`, stray `"exclude"` block removed from `presets.seed.json`. Leave
    the remaining #11 items (per-launch quote → daily; quote typos; AccountabilityFlow Q5)
    open — those are slice 011.
  - **Project Structure** block: drop `Models/JsonLoad.cs`, update the `rain.js` path (remove
    the "yes, JS lives under css/ — move someday" aside since it's done), and update the
    `Platforms/` line to reflect Windows-only.
  - **Tech Stack** table: the Framework row can drop the "Windows is the only real target —
    see Tech Debt" caveat now that the csproj says so.
  - **What's Done**: add a bullet — project trimmed to Windows-only, dead scaffold/files removed.
- **`PLAN.md`** → Phase 2: check the box **"Trim `TargetFrameworks` to Windows only; delete
  unused `Platforms/` folders; remove the CA1416 pragma."** and the file-junk parts of the
  **"Junk removal"** box (stray `"exclude"`, empty `JsonLoad.cs`, move `rain.js`). Leave the
  "delete tutorial comments / stale personal paths" note and the daily-quote box unchecked —
  they belong to later slices.

---

## 5. Verify

This slice changes the build target and can't be fully built in a Linux/WSL environment (the
MAUI Windows workload only builds on Windows). So:

- Run `dotnet test Nimbus.Tests/Nimbus.Tests.csproj` — the plain net9.0 test project is
  independent of the MAUI target and must still pass (this slice touches nothing it compiles;
  it's a regression guard that you didn't break the shared `Utilities/` files).
- Static review, since the MAUI build likely won't run here — state plainly that you couldn't
  build the Windows head, then confirm by inspection:
  - `Nimbus-Internet-Blocker.csproj` now has exactly one `TargetFrameworks` value
    (`net9.0-windows10.0.19041.0`), no android/ios/maccatalyst/tizen references remain, and
    the XML is well-formed (tags balanced).
  - `Platforms/` contains only `Windows/`.
  - `grep -rn "JsonLoad"` (excluding obj/bin) returns nothing.
  - `grep -rn "css/js/rain"` returns nothing; `wwwroot/js/rain.js` exists;
    `index.html` points at `js/rain.js`.
  - `presets.seed.json` parses as JSON and starts at `"categories"`.
- **Do NOT** run git, commit, or push.

### Manual check for the human (Windows, include in your report)

1. `dotnet build -f net9.0-windows10.0.19041.0` (or open in VS and build) — the solution
   restores and builds with the single Windows TFM, no missing-platform errors.
2. Run the app — the **rain overlay still renders** (confirms `js/rain.js` loads from its new
   path) and the dashboard shows a quote.
3. Blocking page loads its categories (confirms `presets.seed.json` is still valid after the
   `"exclude"` removal), Apply / Restore still behave as before.

---

## 6. When finished

Summarize for the human: files/folders changed and deleted (one line each), the `dotnet test`
result, whether the MAUI Windows build was verifiable in this environment (say so if not), and
the manual checklist above. Confirm in one line that **no app behaviour changed** — this was
structure and hygiene only.
