<div align="center">
<h1>Nimbus DNS Blocker</h1>
</div>

> **Your journey to digital wellness begins with small, mindful steps. Block distractions and cultivate presence.**

A modern .NET MAUI desktop application that helps users take control of their digital habits by blocking distracting websites at the system level.

![gif](https://github.com/mbonnic-psx/Nimbus-Internet-Blocker/blob/master/Nimbus%20SS/Nimbus%20Showcase%20Gif.gif)

---

## ☁️ Purpose

Nimbus DNS Blocker is a desktop application used to provide users with an easy way to remove distracting websites from the internet. As the internet becomes more ingrained in our life we use it every day and sometimes we need a break. If you are someone dealing with compulsivity problems or sometimes there is just too much going on this app is something that can help you.

### Use Cases
- **Personal Productivity**: Block social media and entertainment sites during work/study hours
- **Parental Controls**: Create safe browsing environments for children
- **Digital Detox**: Take intentional breaks from specific content categories
- **Focus Sessions**: Eliminate distractions during study or deep work
- **Habit Breaking**: Support behavioral change by blocking addictive sites

---

## 🌧️ Features

### Core Functionality
- **Category-Based Blocking**: Pre-configured categories (Adult, AI, Gambling, Gaming, Messenger, News, Shopping, Social, Streaming)
- **System-Level DNS Blocking**: Works across all browsers and applications
- **Custom Site Blocking**: Add your own domains/websites to block
- **Toggle Categories**: Enable/disable entire categories with one click
- **Persistent Configuration**: Settings maintain on application restart
- **Hosts File Management**: Safely modifies Windows hosts file with automatic backup and restore
- **Admin Elevation Guard**: Detects whether the app is running as Administrator and surfaces a clear warning if not
- **Apply Blocking Rules**: Single button press writes all enabled rules to the hosts file and flushes DNS

### Technical Highlights
- **JSON-Based Storage**: Configuration stored in AppData with seed file pattern
- **Async/Await**: Non-blocking operations throughout
- **Clean Architecture**: Service layer, dependency injection, separation of concerns
- **Input Validation**: URL normalization and domain sanitization
- **Data Persistence**: JSON serialization with error handling
- **Inspirational Quotes**: Motivational messages to support user journey
- **IPv4 + IPv6 Blocking**: Every domain is blocked on both `0.0.0.0` and `::` simultaneously
- **Automatic www. Coverage**: Root domain entries automatically generate `www.` variants
- **DNS Cache Flush**: Runs `ipconfig /flushdns` in a hidden background process after every apply

### Planned Features
- **Password Protection**: Prevent unauthorized configuration changes
- **Usage Statistics**: Track number of domains blocked
- **Wildcard Blocking**: Block entire domains with `*.example.com`
- **Timer**: Time-based blocking for studying/work
- **Resources Page**: A dedicated tab with links and tools for users seeking support with addiction, mental health, or emotional well-being

---

## ⛈️ Screenshots

![dashboard](https://github.com/mbonnic-psx/Nimbus-Internet-Blocker/blob/master/Nimbus%20SS/Nimbus%20Dashboard.png)
> Main Dashboard is where you will see the quote of the day and usage statistics

![blocking](https://github.com/mbonnic-psx/Nimbus-Internet-Blocker/blob/master/Nimbus%20SS/Nimbus%20Blocking.png)
> Blocking page is where you will block domains based on category or custom website you input

![setting](https://github.com/mbonnic-psx/Nimbus-Internet-Blocker/blob/master/Nimbus%20SS/Nimbus%20Setting.png)
> Setting page is where you will set password and or restore old/default host file

---

## 💧 Technology Stack

### Core Technologies
- **Framework**: .NET 9 / .NET MAUI
- **Language**: C# 11
- **UI**: Blazor WebView (Razor components)
- **Storage**: JSON (System.Text.Json)
- **Platform**: Windows Desktop

### Key Libraries & APIs
- `Microsoft.Maui.Controls` - Cross-platform UI framework
- `System.Text.Json` - High-performance JSON serialization
- `System.IO` - File system operations
- `System.Security.Principal` - Windows Administrator elevation detection
- `System.Diagnostics.Process` - Background DNS flush via `ipconfig /flushdns`
- Windows Hosts File (`C:\Windows\System32\drivers\etc\hosts`)

---

## 🌂 Security (Planned)

Nimbus is a **local-first** desktop application — all configuration stays on your machine.

### Password Protection

Nimbus DNS Blocker is adding an optional **Password Lock** so that once blocking rules are applied, you must enter a password to:
- Enable categories / add custom sites
- Disable categories / remove custom sites
- Restore the hosts file from a backup

> If a password is not set up you will be prompted to input the quote of the day. This is to prevent people that want to change habits but don't have or choose not to have an accountability person that holds the password.

This is meant to prevent **casual tampering** so people can't just easily switch off their blocks.

### Password Hashing (libsodium / Argon2id)

Nimbus will **never store your password**. Instead, it stores a *password hash string* generated by libsodium (Argon2id), which includes its own salt and parameters.

High-level flow:
1. User sets a password → app generates a verifier string (`crypto_pwhash_str`)
2. Verifier string is saved in local app settings (AppData)
3. To unlock, Nimbus verifies the entered password against the stored verifier (`crypto_pwhash_str_verify`)

### Input Validation & JSON Hardening

Nimbus treats all local configuration as **untrusted input** (even though it lives in AppData). Planned hardening includes:
- Strict domain normalization (strip protocol/paths, lowercase, dedupe)
- Bounds checks (max file size, max entries per category, max domain length)
- Strongly-typed JSON models (no dynamic type loading)
- Safe JSON parsing defaults (fail fast on malformed JSON)
- Unit tests for malformed/malicious JSON payloads

### Security Limitations

Because Nimbus uses **hosts-file based blocking**, it cannot stop a user with **Administrator** access from editing the hosts file directly. The lock is designed to prevent *unauthorized changes inside the Nimbus app*, not to provide tamper-proof enforcement against an admin user.

---

## ☔ Project Structure

```
Nimbus-Internet-Blocker/
├── Components/
│   └── Pages/
│       ├── Home.razor               # Dashboard with daily quote
│       ├── Blocking.razor           # Category & custom site management
│       └── Settings.razor           # App configuration
├── Layout/
│   ├── MainLayout.razor             # App shell with sidebar navigation
│   └── NavMenu.razor                # Sidebar navigation component
├── Models/
│   ├── PresetsRoot.cs               # Category blocklist data models
│   └── CustomsRoot.cs               # Custom sites data models
├── Services/
│   ├── IHostsFileService.cs         # Hosts file service interface
│   ├── HostsFileService.cs          # Hosts file read/write/backup/DNS flush
│   ├── PresetService.cs             # Category blocklist management
│   ├── CustomSitesService.cs        # Custom sites management
│   ├── QuoteService.cs              # Inspirational quote provider
│   └── SnackbarService.cs           # Toast notification system
├── wwwroot/
│   └── css/
│       └── app.css                  # Neumorphic UI styles
├── Resources/
│   └── Raw/
│       ├── presets.seed.json        # Default category configuration
│       └── custom.seed.json         # Default custom sites config
├── MauiProgram.cs                   # App initialization & DI setup
└── App.xaml.cs                      # Application entry point
```

---

## 🌦️ Usage

### Blocking Categories

This app includes 9 pre-configured categories:

| Category | Example Sites | Use Case |
|----------|--------------|----------|
| **Adult** | pornhub.com, onlyfans.com | Content filtering |
| **AI** | grok.com, claude.ai, chatgpt.com | Limit AI tool usage |
| **Gambling** | draftkings.com, fanduel.com | Prevent gambling access |
| **Gaming** | coolmathgames.com, roblox.com | Focus during work/study |
| **Messenger** | telegram.org, discord.com | Reduce chat distractions |
| **News** | cnn.com, foxnews.com, reddit.com | Avoid news doom-scrolling |
| **Shopping** | amazon.com, ebay.com, etsy.com | Control impulse purchases |
| **Social** | facebook.com, instagram.com, tiktok.com | Break social media habits |
| **Streaming** | youtube.com, netflix.com, twitch.tv | Limit entertainment |

> Each category contains multiple domains. Root domains and their `www.` variants are both blocked automatically.

### How It Works

1. Open **Nimbus DNS Blocker** (must be run as Administrator)
2. Navigate to the **Blocking** page
3. Toggle the switch for whichever categories you want to block
4. Add any custom sites you want blocked
5. Click **Apply Blocking Rules**
6. Blocking takes effect immediately

Nimbus modifies the Windows hosts file to redirect blocked domains to `0.0.0.0` and `::`:

```
# Normal hosts file
127.0.0.1    localhost

# After Nimbus blocks facebook.com
127.0.0.1    localhost

# --- Nimbus-managed section BEGIN ---
0.0.0.0         facebook.com
::              facebook.com
0.0.0.0         www.facebook.com
::              www.facebook.com
# --- Nimbus-managed section END ---
```

When your browser tries to access `facebook.com`, the hosts file routes it to `0.0.0.0` (nowhere), blocking the connection across all browsers and apps.

After writing the hosts file, Nimbus automatically flushes the DNS cache:

```cmd
ipconfig /flushdns
```

This runs silently in the background — no console window appears.

### Administrator Requirement

Editing the Windows hosts file requires Administrator privileges. If Nimbus is not running as Administrator, the Blocking page will display a warning banner and the Apply button will surface a clear error message explaining how to relaunch correctly.

---

## 🌪️ Architecture Deep Dive

### Service Layer Pattern

**HostsFileService.cs** — Manages all hosts file operations
```csharp
public class HostsFileService : IHostsFileService
{
    // Checks if the current process has Administrator privileges
    public bool IsElevated { get; }

    // Loads all enabled rules, writes to hosts file, flushes DNS
    public async Task ApplyAsync()
}
```

**Key Design Decisions:**
- **One-time backup**: Creates `hosts.nimbus.bak` on first write, preserving the original pre-Nimbus state forever
- **Marker-based splicing**: Only the `# --- Nimbus-managed section ---` block is ever touched — lines outside it are never modified
- **Concurrent loading**: Presets and custom sites are loaded simultaneously via `Task.WhenAll`
- **UTF-8 No BOM**: Hosts file is written without a BOM, which is the Windows standard

**PresetService.cs** — Manages category-based blocklists
```csharp
public class PresetService
{
    // Ensures live JSON file exists in AppData
    public async Task<string> EnsureLiveFileExistsAsync()

    // Loads blocklist configuration from AppData
    public async Task<PresetsRoot> LoadAsync()

    // Saves modified configuration back to AppData
    public async Task SaveAsync(PresetsRoot root)

    // Normalizes/validates all entries (removes dupes, cleans URLs)
    public void NormalizePresets(PresetsRoot root)
}
```

**Key Design Decisions:**
- **Seed File Pattern**: Ships with `presets.seed.json`, copies to `presets.json` in AppData on first run
- **Data Normalization**: Strips protocols, ports, paths from URLs (`https://example.com:443/path` → `example.com`)
- **Duplicate Removal**: Case-insensitive deduplication per category

### Data Models
```csharp
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
    public string Host { get; set; }  // e.g. "facebook.com"
    public string Ipv4 { get; set; }  // Default: "0.0.0.0"
    public string Ipv6 { get; set; }  // Default: "::"
}
```

### JSON Storage Format

**Location**: `%LOCALAPPDATA%\<username>\com.companyname.nimbusinternetblocker\Data\presets.json`

```json
{
  "categories": {
    "Social": {
      "enabled": true,
      "entries": [
        { "host": "facebook.com", "ipv4": "0.0.0.0", "ipv6": "::" },
        { "host": "instagram.com", "ipv4": "0.0.0.0", "ipv6": "::" }
      ]
    }
  }
}
```

---

## 🌩️ Roadmap

### Phase 1: Core Features
- [x] Category-based blocking UI
- [x] JSON configuration persistence with seed file pattern
- [x] Neumorphic UI design
- [x] Inspirational quotes system
- [x] Hosts file integration (read, write, backup, restore)
- [x] IPv4 + IPv6 blocking with automatic www. variant generation
- [x] Admin elevation detection and warning banner
- [x] Apply button wired end-to-end with loading state
- [x] DNS cache flush after every apply
- [ ] Custom sites full integration with Apply flow
- [ ] Settings page — restore hosts file from backup

### Phase 2: Advanced Features
- [ ] Password Setup
- [ ] Usage Statistics
- [ ] Wildcard Blocking
- [ ] Focus Timer
- [ ] Resources Page

### Phase 3: Security Hardening
- [ ] Password hashing with libsodium / Argon2id
- [ ] JSON hardening and bounds checks
- [ ] Strict domain normalization unit tests

---

## 🌦️ Contact & Support

**Developer**: Matthew Bonnichsen
**Email**: mbonnic81@gmail.com
**LinkedIn**: [matthew-bonnichsen](https://www.linkedin.com/in/matthew-bonnichsen)
**Portfolio**: [github.com/mbonnic-psx](https://github.com/mbonnic-psx)

---

<div align="center">

> "If you feel like you are losing everything remember, trees lose their leaves every year, yet they stand tall and wait for better days to come — Anonymous"

[⬆ Back to Top](#nimbus-dns-blocker)

</div>
