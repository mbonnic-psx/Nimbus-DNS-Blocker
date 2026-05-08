# Nimbus DNS Blocker — Claude Code Context

## What This App Does

Nimbus DNS Blocker is a **Windows desktop application** built with .NET MAUI + Blazor WebView that helps users block distracting websites at the system level by modifying the Windows hosts file. It targets people dealing with digital compulsivity, focus issues, or parental control needs.

Blocking works by redirecting domains to `0.0.0.0` in the hosts file and flushing the DNS cache via `ipconfig /flushdns`. The app is **local-first** — no backend, no accounts, no telemetry.

---

## Tech Stack

|Layer|Technology|
|---|---|
|Framework|.NET MAUI (targets Windows only)|
|Language|C# 11|
|UI|Blazor WebView (Razor components in `.razor` files)|
|Styling|Custom CSS — neumorphic design in `wwwroot/css/app.css`|
|Storage|JSON via `System.Text.Json` stored in `%LOCALAPPDATA%`|
|DI|`Microsoft.Extensions.DependencyInjection` via `MauiProgram.cs`|

---

## Project Structure

```
Nimbus-Internet-Blocker/
├── Components/
│   └── Pages/
│       ├── Home.razor           # Dashboard — quote of the day + usage stats
│       ├── Blocking.razor       # Category toggles + custom site input
│       └── Settings.razor       # Password setup + hosts file restore
├── Models/
│   ├── PresetsRoot.cs           # Root model for category blocklists
│   └── CustomsRoot.cs           # Root model for user-added custom sites
├── Services/
│   ├── PresetService.cs         # Category blocklist load/save/normalize
│   ├── CustomSitesService.cs    # Custom sites CRUD (WIP)
│   ├── HostsFileService.cs      # Hosts file read/write/backup (TO BE BUILT)
│   ├── QuoteService.cs          # Loads daily inspirational quote
│   └── SnackbarService.cs       # Toast/snackbar notification system
├── Shared/
│   └── MainLayout.razor         # App shell with sidebar nav
├── Resources/
│   └── Raw/
│       ├── presets.seed.json    # Default category config (ships with app)
│       └── custom.seed.json     # Default custom sites config
├── wwwroot/
│   └── css/app.css              # All styles — do not inline styles in Razor
├── MauiProgram.cs               # App entry point + DI registrations
└── App.xaml.cs                  # MAUI application root
```

---

## Architecture Rules — Always Follow These

### Service Layer Pattern

- All business logic lives in `Services/`. Razor components call services — they do not contain logic directly.
- Services are registered as **Singletons** in `MauiProgram.cs`.
- Always inject via constructor or `@inject` in Razor — never `new` a service directly.

### Async/Await

- All file I/O and any potentially slow operation must be `async Task` — no `.Result` or `.Wait()` blocking calls.
- UI-bound state changes in Razor components call `StateHasChanged()` after async updates if needed.

### Data Persistence — Seed File Pattern

- App ships with `*.seed.json` files in `Resources/Raw/`.
- On first run, `PresetService` copies the seed file to `%LOCALAPPDATA%\...\Data\presets.json`.
- All reads/writes at runtime go to the AppData live file, never the seed.
- Same pattern applies to `CustomSitesService`.

### URL / Domain Normalization

- Always strip protocol (`https://`), ports (`:443`), and paths (`/foo`) from user input.
- Lowercase all domains.
- Deduplicate case-insensitively within a category.
- Normalize before saving, not after loading.

### Snackbar for User Feedback

- All user-facing success/error feedback goes through `ISnackbarService`.
- Never use `Console.WriteLine`, `Debug.WriteLine`, or alerts for user feedback.

---

## Key Data Models

```csharp
// Root config object loaded from presets.json
public class PresetsRoot
{
    public Dictionary<string, PresetCategory> Categories { get; set; }
}

public class PresetCategory
{
    public bool Enabled { get; set; }
    public List<PresetEntry> Entries { get; set; }
}

public class PresetEntry
{
    public string Host { get; set; }   // e.g. "facebook.com"
    public string Ipv4 { get; set; }   // default "0.0.0.0"
    public string Ipv6 { get; set; }   // default "::"
}
```

---

## Critical File Paths

```
Hosts file:       C:\Windows\System32\drivers\etc\hosts
Hosts backup:     C:\Windows\System32\drivers\etc\hosts.nimbus.bak
AppData base:     %LOCALAPPDATA%\<username>\com.companyname.nimbusinternetblocker\Data\
Presets config:   ...Data\presets.json
Custom config:    ...Data\custom.json
```

---

## Hosts File Format

Nimbus owns a clearly delimited section. Never touch lines outside this section.

```
# --- Nimbus-managed section BEGIN ---
0.0.0.0      facebook.com
::           facebook.com
0.0.0.0      www.facebook.com
::           www.facebook.com
0.0.0.0      x.com
::           x.com
# --- Nimbus-managed section END ---
```

- Both `host.com` and `www.host.com` are written for each entry.
- Write both IPv4 (`0.0.0.0`) and the entry's `Host` field.
- After every write, run `ipconfig /flushdns` in a hidden background process.

---

## DI Registration (MauiProgram.cs)

All services registered as Singletons. When adding a new service, register it here:

```csharp
builder.Services.AddSingleton<ISnackbarService, SnackbarService>();
builder.Services.AddSingleton<QuoteService>();
builder.Services.AddSingleton<PresetService>();
builder.Services.AddSingleton<CustomSitesService>();
// Add new services below:
builder.Services.AddSingleton<IHostsFileService, HostsFileService>();
```

Always create an interface for services that touch the file system or OS (testability + mockability).

---

## What's Done

- [x] Category-based blocking UI (9 categories)
- [x] JSON config persistence with seed file pattern
- [x] Neumorphic UI design + CSS
- [x] Inspirational quote system (`QuoteService`)
- [x] Snackbar notification system
- [x] `PresetService` with load/save/normalize
- [x] DI wiring in `MauiProgram.cs`

## What's NOT Done Yet (Build in this order)

- [ ] `HostsFileService` — reads/writes/backups the hosts file (MOST CRITICAL)
- [ ] Apply button wired end-to-end (calls `HostsFileService` + DNS flush)
- [ ] `CustomSitesService` — full CRUD + integration with apply flow
- [ ] Settings page — restore from backup
- [ ] Password protection (Phase 2)
- [ ] Usage statistics on dashboard (Phase 2)
- [ ] Timer-based blocking (Phase 2)
- [ ] Resources page (Phase 2)

---

## Coding Style

- Use `var` for local variables where the type is obvious.
- Prefer expression-bodied members for simple getters/one-liners.
- XML doc comments (`/// <summary>`) on all public service methods.
- No magic strings — use `const` for repeated literals (especially section markers and file paths).
- Wrap all file I/O in try/catch and surface errors via `ISnackbarService`.

---

## What NOT To Do

- Do not put business logic in Razor components — move it to a service.
- Do not write to the hosts file without creating a backup first.
- Do not block the UI thread — everything async.
- Do not add NuGet packages without checking if the functionality exists in `System.*` first.
- Do not modify lines outside the Nimbus-managed section in the hosts file.
- Do not store passwords in plaintext — Phase 2 uses libsodium/Argon2id hashing.
- Do not inline CSS styles in Razor — all styles belong in `wwwroot/css/app.css`.