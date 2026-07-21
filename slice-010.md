# Slice 010 — Phase 2: Service dedup + interfaces (PresetService / CustomSitesService)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the checklist at the bottom.

---

## 1. Goal

`PresetService` and `CustomSitesService` are the last two services that (a) still duplicate
their seed-file plumbing and (b) have no interface — they're injected as concrete types, which
violates the CLAUDE.md rule "create an interface for any service that touches the file system."
This is the remaining half of Tech Debt #5. This slice:

1. **Deduplicates the shared seed plumbing** — `GetLivePath`, `EnsureLiveFileExistsAsync`, and
   `ReadSeedTextAsync` are byte-for-byte identical between the two services except for the
   live/seed file names and the empty-fallback JSON. Pull them into one abstract base class.
2. **Adds `IPresetService` and `ICustomSitesService`** and registers the services against them,
   so every consumer depends on the interface, not the concrete type.

**No behaviour change.** Load/Save/Normalize/AddSite/RemoveSite semantics stay exactly as they
are (including the null-on-failure load contract and atomic saves from slice 004). This is a
refactor.

**Decisions already made (do not deviate):**

- **Dedup via an abstract base class, not a `Utilities/` helper.** The shared code calls
  `FileSystem.AppDataDirectory` and `FileSystem.OpenAppPackageFileAsync` — MAUI Essentials
  APIs. A `Utilities/` file must stay MAUI-free so the plain net9.0 test project can compile it
  (that's the whole reason `HostValidation`/`HostsSection` live there). This plumbing can't, so
  it does **not** go in `Utilities/` and gets **no** unit test — it's I/O, and PLAN.md says
  nothing beyond the two pure functions needs tests for v1. Put the base class in `Services/`.
- **Interfaces expose only the consumer surface** (mirrors `IHostsFileService`, which exposes
  only what callers use). Verified call sites:
  - `IPresetService`: `LoadAsync`, `SaveAsync` (used by `HostsFileService`, `Blocking.razor`,
    `Settings.razor`).
  - `ICustomSitesService`: `LoadAsync`, `SaveAsync`, `AddSite`, `RemoveSite` (used by the same
    three; `AddSite`/`RemoveSite` only from `Blocking.razor`).
  - `GetLivePath`, `EnsureLiveFileExistsAsync`, `NormalizePresets`/`NormalizeCustoms`,
    `ReadSeedTextAsync` are **not** on the interfaces — no consumer calls them; they stay
    public/protected on the classes as implementation detail.
- **Drop the tutorial comments while you're in here.** These files carry `CLAUDE.md`-banned
  narration (`Task<T> = this work will finish later...`, `async = ...`, `await means ...`, the
  `GetLivePath` "Links the path of AppData" blurb). Since the methods are being moved/rewritten,
  delete that narration — do **not** copy it into the base class. Keep the genuinely useful
  comments that state *invariants* (e.g. the `LoadAsync` null-means-do-not-touch-disk XML docs,
  the "pending until apply" notes on `AddSite`/`RemoveSite`).
- **No rename, no other Phase 2 items.** The daily-quote / Q5 work is slice 011.

**Scope discipline:** don't touch `HostsFileService`'s hosts logic, the Razor flows' behaviour,
CSS, or the models. Only the two services, the new base class, the two interfaces, the DI
registrations, and the injection sites change.

---

## 2. Before you start

Read in full: `Services/PresetService.cs`, `Services/CustomSitesService.cs`,
`Services/IHostsFileService.cs` (for the interface house style — XML docs, namespace),
`MauiProgram.cs`, and the injection sites: `Services/HostsFileService.cs` (fields/ctor, lines
~21–33), `Components/Pages/Blocking.razor` (`@inject`, lines 3–4), and
`Components/Pages/Settings.razor` (`@inject`, lines 5–6).

---

## 3. Edits

### Edit A — new file `Services/SeedBackedConfigService.cs`

The shared base. Subclasses supply three values; everything else is inherited.

```csharp
namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Shared seed-file plumbing for the JSON-backed config services: the app ships a
/// read-only <c>*.seed.json</c> in Resources/Raw and, on first run, copies it to a
/// writable live file in AppData. All runtime reads/writes go to the live file.
/// Subclasses supply the file names and the empty-fallback JSON; Load/Save/Normalize
/// stay in the concrete services because their root types differ.
/// </summary>
public abstract class SeedBackedConfigService
{
    /// <summary>Live (writable) file name in AppData, e.g. "presets.json".</summary>
    protected abstract string LiveFileName { get; }

    /// <summary>Packaged seed file name in Resources/Raw, e.g. "presets.seed.json".</summary>
    protected abstract string SeedFileName { get; }

    /// <summary>
    /// JSON written to the live file when the seed can't be read — a valid empty
    /// root of the subclass's shape (e.g. <c>{ "categories": {} }</c>).
    /// </summary>
    protected abstract string EmptyFallbackJson { get; }

    /// <summary>Absolute path of the live file in AppData.</summary>
    public string GetLivePath()
        => Path.Combine(FileSystem.AppDataDirectory, LiveFileName);

    /// <summary>
    /// Ensures the live file exists, copying the packaged seed (or the empty
    /// fallback if the seed can't be read) on first run. Returns the live path.
    /// </summary>
    public async Task<string> EnsureLiveFileExistsAsync()
    {
        string livePath = GetLivePath();
        if (File.Exists(livePath)) return livePath;

        string seedJson = await ReadSeedTextAsync();
        if (string.IsNullOrWhiteSpace(seedJson))
            seedJson = EmptyFallbackJson;

        var folder = Path.GetDirectoryName(livePath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(livePath, seedJson);
        return livePath;
    }

    /// <summary>Reads the packaged seed file from the app package (Resources/Raw).</summary>
    protected async Task<string> ReadSeedTextAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(SeedFileName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
```

### Edit B — new file `Services/IPresetService.cs`

```csharp
using Nimbus_Internet_Blocker.Models;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Loads and saves the category blocklist (presets). Program against this
/// interface — never depend on PresetService directly.
/// </summary>
public interface IPresetService
{
    /// <summary>
    /// Loads the live presets file. Returns <see langword="null"/> when the file
    /// exists but cannot be read or parsed — callers must treat null as "do not
    /// touch disk", never as an empty config (that would be a data-loss path).
    /// </summary>
    Task<PresetsRoot?> LoadAsync();

    /// <summary>
    /// Normalizes and saves the presets atomically. Returns <see langword="false"/>
    /// on failure; the previous file is left intact.
    /// </summary>
    Task<bool> SaveAsync(PresetsRoot root);
}
```

### Edit C — new file `Services/ICustomSitesService.cs`

```csharp
using Nimbus_Internet_Blocker.Models;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>
/// Loads, saves, and edits the user's custom blocked sites. Program against this
/// interface — never depend on CustomSitesService directly.
/// </summary>
public interface ICustomSitesService
{
    /// <summary>
    /// Loads the live custom sites file. Returns <see langword="null"/> when the file
    /// exists but cannot be read or parsed — callers must treat null as "do not
    /// touch disk", never as an empty config.
    /// </summary>
    Task<CustomsRoot?> LoadAsync();

    /// <summary>
    /// Normalizes and saves the custom sites atomically. Returns <see langword="false"/>
    /// on failure; the previous file is left intact.
    /// </summary>
    Task<bool> SaveAsync(CustomsRoot root);

    /// <summary>
    /// Validates and adds a host to <paramref name="root"/> in memory. Nothing is
    /// saved — pending changes persist at apply time (see Blocking.razor).
    /// </summary>
    (bool success, string message) AddSite(CustomsRoot root, string inputHost);

    /// <summary>
    /// Removes a host from <paramref name="root"/> in memory. Nothing is saved —
    /// pending changes persist at apply time.
    /// </summary>
    (bool success, string message) RemoveSite(CustomsRoot root, string host);
}
```

### Edit D — `Services/PresetService.cs`

- Change the declaration to
  `public class PresetService : SeedBackedConfigService, IPresetService`.
- **Delete** the now-inherited members: the `GetLivePath()` method, `EnsureLiveFileExistsAsync()`,
  and the private `ReadSeedTextAsync(string)`. Also delete the two `const` file-name fields and
  re-express them as the base's abstract overrides:

  ```csharp
  protected override string LiveFileName      => "presets.json";
  protected override string SeedFileName      => "presets.seed.json";
  protected override string EmptyFallbackJson => "{ \"categories\": {} }";
  ```

  (If `PresetService.liveFileName`/`seedFileName` consts are referenced anywhere else, grep
  first — they are not, per the repo search — so removing them is safe.)
- **Keep** `LoadAsync`, `SaveAsync`, and `NormalizePresets` exactly as they are (they call the
  inherited `EnsureLiveFileExistsAsync`/`GetLivePath`, which still resolve). Keep the XML doc
  comments on Load/Save.
- Delete the tutorial-comment blocks (the `Task<T> =`/`async`/`await` narration and the
  `GetLivePath` blurb) — they're gone with the moved methods; don't reintroduce them.

### Edit E — `Services/CustomSitesService.cs`

Same treatment:

- Declaration → `public class CustomSitesService : SeedBackedConfigService, ICustomSitesService`.
- Delete inherited `GetLivePath()`, `EnsureLiveFileExistsAsync()`, private `ReadSeedTextAsync`,
  and the two file-name consts; add:

  ```csharp
  protected override string LiveFileName      => "custom.json";
  protected override string SeedFileName      => "custom.seed.json";
  protected override string EmptyFallbackJson => "{ \"sites\": [] }";
  ```
- **Keep** `LoadAsync`, `SaveAsync`, `NormalizeCustoms`, `AddSite`, `RemoveSite` unchanged,
  including their XML docs.
- Drop the tutorial comments.

### Edit F — `MauiProgram.cs`: register against the interfaces

```csharp
builder.Services.AddSingleton<PresetService>();          // before
builder.Services.AddSingleton<CustomSitesService>();     // before
```
→
```csharp
builder.Services.AddSingleton<IPresetService, PresetService>();
builder.Services.AddSingleton<ICustomSitesService, CustomSitesService>();
```

### Edit G — update the three injection sites to the interfaces

- **`Services/HostsFileService.cs`** (~lines 21–33): change field types and ctor params from
  `PresetService`/`CustomSitesService` to `IPresetService`/`ICustomSitesService`. The only
  call is `.LoadAsync()`, which is on both interfaces — no body change.
- **`Components/Pages/Blocking.razor`** (lines 3–4): `@inject IPresetService PresetService` and
  `@inject ICustomSitesService CustomSitesService`. **Keep the variable names** (`PresetService`,
  `CustomSitesService`) so the ~10 usages below (`LoadAsync`, `SaveAsync`, `AddSite`,
  `RemoveSite`) are untouched — all four are on the interfaces.
- **`Components/Pages/Settings.razor`** (lines 5–6): same, `@inject IPresetService PresetService`
  / `@inject ICustomSitesService CustomSitesService`. Usages are `LoadAsync`/`SaveAsync` only —
  both on the interface.

---

## 4. Docs to update

- **`CLAUDE.md`**
  - **Tech Debt #5**: mark **RESOLVED** — the seed plumbing (`GetLivePath`,
    `EnsureLiveFileExistsAsync`, `ReadSeedTextAsync`) is now shared via an abstract
    `SeedBackedConfigService` base, and both services have interfaces. Nothing left open in #5.
  - **Architecture Rules → Service Layer Pattern**: remove the line "`PresetService` and
    `CustomSitesService` currently violate this — fix when touched" (now fixed).
  - **Project Structure**: add `Services/SeedBackedConfigService.cs`,
    `Services/IPresetService.cs`, `Services/ICustomSitesService.cs`; drop the "(no interface
    yet)" notes on `PresetService`/`CustomSitesService`.
  - **DI Registration** block: update the two lines to the interface-registered form.
  - **What's Done**: add a bullet — preset/custom services deduped into a shared seed base and
    put behind interfaces.
- **`PLAN.md`** → Phase 2: check the box **"Deduplicate `PresetService`/`CustomSitesService`
  shared logic ... add interfaces for both services."** (The `NormalizeHost` / seed-shape /
  log-message parts were already done in slice 005; this closes the seed-plumbing + interfaces
  remainder.)

---

## 5. Verify

- Run `dotnet test Nimbus.Tests/Nimbus.Tests.csproj` — the pure-utility tests are unaffected and
  must still pass (regression guard; this slice adds no testable pure logic — it's MAUI I/O).
- The MAUI Windows head only builds on Windows; if this is a Linux/WSL environment and it won't
  build, say so plainly and verify by inspection instead:
  - `SeedBackedConfigService` compiles conceptually: three abstract members, and
    `EnsureLiveFileExistsAsync`/`ReadSeedTextAsync`/`GetLivePath` reference only the abstracts
    and `FileSystem`/`File`/`Directory`.
  - Neither `PresetService` nor `CustomSitesService` still declares `GetLivePath`,
    `EnsureLiveFileExistsAsync`, `ReadSeedTextAsync`, or the file-name consts (no duplicate /
    hiding-inherited-member warnings), and each declares the three `override` properties.
  - `grep -rn "PresetService\|CustomSitesService"` (excluding obj/bin and the two class files)
    shows only interface-typed usages after the edits; `grep -rn "liveFileName\|seedFileName"`
    shows no stray references to the removed consts.
  - Every injection site (`HostsFileService` fields+ctor, both `.razor` `@inject`s) names the
    interface; DI registers `IPresetService`/`ICustomSitesService`.
- **Do NOT** run git, commit, or push.

### Manual check for the human (Windows, include in your report)

1. Build the Windows head — no compile errors, no CS0108 "hides inherited member" warnings.
2. Fresh run (or delete `%LOCALAPPDATA%\...\Data\presets.json` + `custom.json` first): the app
   recreates both live files from seed — confirms `EnsureLiveFileExistsAsync` still works from
   the base class. Categories and custom sites load on the Blocking page.
3. Add a custom site, toggle a category, Apply — saves succeed; Restore still works. (Confirms
   Load/Save/AddSite/RemoveSite behave identically through the interfaces.)

---

## 6. When finished

Summarize for the human: files created/changed (one line each), the `dotnet test` result,
whether the Windows build was verifiable here (say so if not), and the manual checklist above.
Confirm in one line that this was a **pure refactor — no behaviour changed**, and that Tech
Debt #5 is now fully closed.
