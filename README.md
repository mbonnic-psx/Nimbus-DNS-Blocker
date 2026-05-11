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
- **Custom Site Blocking**: Add and remove your own domains/websites to block
- **Toggle Categories**: Enable/disable entire categories with one click
- **Persistent Configuration**: Settings maintain on application restart
- **Hosts File Management**: Safely modifies Windows hosts file with automatic backup and restore
- **Admin Elevation Guard**: Detects whether the app is running as Administrator and surfaces a clear warning if not
- **Apply Blocking Rules**: Single button press writes all enabled rules to the hosts file and flushes DNS

### Password Protection
- **Optional Password Lock**: Prevent unauthorized changes to blocking rules once applied
- **PBKDF2 Hashing**: Passwords are hashed via `PasswordHasher<string>` — the raw password is never stored
- **Two Recovery Modes**:
  - **Accountability Mode** — Walk through 5 grounding questions including today's inspirational quote
  - **Guardian Mode** — Transcribe a cryptographically random one-time code (never stored, never reused)
- **Accountability Flow**: Enforces a 3-second mandatory pause per question so the user reads and reflects before advancing

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
- **Guardian Flow UI**: UI component for transcribing the one-time Guardian recovery code
- **Unlock Modal**: Compose both recovery flows behind a single password-first modal on the Apply button
- **Settings Page**: Full password setup, change, and removal with recovery mode picker
- **Usage Statistics**: Track number of domains blocked
- **Wildcard Blocking**: Block entire domains with `*.example.com`
- **Focus Timer**: Time-based blocking for studying and work
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
- **Storage**: JSON (System.Text.Json) + MAUI Preferences
- **Platform**: Windows Desktop

### Key Libraries & APIs
- `Microsoft.Maui.Controls` - Cross-platform UI framework
- `Microsoft.Extensions.Identity.Core` - PBKDF2 password hashing via `PasswordHasher<string>`
- `System.Text.Json` - High-performance JSON serialization
- `System.IO` - File system operations
- `System.Security.Principal` - Windows Administrator elevation detection
- `System.Diagnostics.Process` - Background DNS flush via `ipconfig /flushdns`
- Windows Hosts File (`C:\Windows\System32\drivers\etc\hosts`)

---

## 🌂 Security

Nimbus is a **local-first** desktop application — all configuration stays on your machine.

### Password Protection

Nimbus DNS Blocker includes an optional **Password Lock** so that once blocking rules are applied, you must enter a password to apply any further changes.

> If a password is not set, blocking rules apply without a prompt. Once a password is set, it is required before every Apply — protecting against casual urges to disable blocks.

### Password Hashing

Nimbus **never stores your password**. Instead it stores a hash string generated by `PasswordHasher<string>` (PBKDF2 + random salt), saved in MAUI Preferences (AppData). To unlock, Nimbus verifies the entered password against the stored hash.

High-level flow:
1. User sets a password → app generates a hash string
2. Hash string is saved in local app settings
3. To unlock, Nimbus verifies the entered password against the stored hash

### Recovery Modes

If you forget your password, Nimbus offers two recovery paths set at password-creation time:

**Accountability Mode** — Walk through 5 grounding questions one at a time. Each question enforces a 3-second mandatory pause before the Next button enables, so the user actually reads and reflects. The final question requires typing today's inspirational quote exactly. Accepted answers are shown as greyed hints for questions 1–4; no hint is given for the quote.

**Guardian Mode** — A cryptographically random one-time code is generated at recovery time, displayed once in a monospace box with a clear "this will not be shown again" warning. The user must transcribe it manually (paste is disabled). The code is never stored.

### Security Limitations

Because Nimbus uses **hosts-file based blocking**, it cannot stop a user with **Administrator** access from editing the hosts file directly. The lock is designed to prevent *unauthorized changes inside the Nimbus app*, not to provide tamper-proof enforcement against an admin user.

---

## ☔ Project Structure

```
Nimbus-Internet-Blocker/
├── Components/
│   ├── Pages/
│   │   ├── Home.razor               # Dashboard with daily quote
│   │   ├── Blocking.razor           # Category & custom site management + Apply wiring
│   │   └── Settings.razor           # App configuration (in progress)
│   └── Shared/
│       ├── AccountabilityFlow.razor # 5-question grounding recovery flow (DONE)
│       ├── GuardianFlow.razor       # One-time code recovery flow (TO BUILD)
│       └── UnlockModal.razor        # Password-first modal composing both flows (TO BUILD)
├── Layout/
│   ├── MainLayout.razor             # App shell with sidebar navigation
│   └── NavMenu.razor                # Sidebar navigation component
├── Models/
│   ├── PresetsRoot.cs               # Category blocklist data models
│   └── CustomsRoot.cs               # Custom sites data models
├── Services/
│   ├── IHostsFileService.cs         # Hosts file service interface
│   ├── HostsFileService.cs          # Hosts file read/write/backup/DNS flush
│   ├── IPasswordService.cs          # Password service interface
│   ├── PasswordService.cs           # PBKDF2 hashing, recovery mode, Guardian hash
│   ├── PresetService.cs             # Category blocklist management
│   ├── CustomSitesService.cs        # Custom sites management (add/toggle/remove)
│   ├── QuoteService.cs              # Inspirational quote provider
│   ├── ISnackbarService.cs          # Snackbar service interface
│   └── SnackbarService.cs           # Toast notification system
├── wwwroot/
│   └── css/
│       └── app.css                  # Neumorphic UI styles + accountability flow classes
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
6. If a password is set, you will be prompted to enter it before changes are written
7. Blocking takes effect immediately

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

**PasswordService.cs** — Manages optional password lock and recovery
```csharp
public class PasswordService : IPasswordService
{
    public bool IsPasswordEnabled()
    public RecoveryMode GetRecoveryMode()
    public Task SetPasswordAsync(string password, string confirmPassword, RecoveryMode mode)
    public bool VerifyPassword(string attempt)
    public Task ClearPasswordAsync()

    // Generates a cryptographically random 4-segment code — never stored
    public string GenerateGuardianHash()
}
```

**Key Design Decisions:**
- **One-time backup**: Creates `hosts.nimbus.bak` on first write, preserving the original pre-Nimbus state forever
- **Marker-based splicing**: Only the `# --- Nimbus-managed section ---` block is ever touched — lines outside it are never modified
- **Concurrent loading**: Presets and custom sites are loaded simultaneously via `Task.WhenAll`
- **UTF-8 No BOM**: Hosts file is written without a BOM, which is the Windows standard
- **Never stored secrets**: Guardian hash is generated fresh at recovery time and discarded immediately after verification

**PresetService.cs** — Manages category-based blocklists
```csharp
public class PresetService
{
    public async Task<PresetsRoot> LoadAsync()
    public async Task SaveAsync(PresetsRoot root)
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

public enum RecoveryMode
{
    Accountability,
    Guardian
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
- [x] Custom site removal
- [ ] Wildcard Blocking

### Phase 2: Advanced Features
- [x] Password service (PBKDF2 hashing, recovery mode, Guardian hash generation)
- [x] Accountability Flow — 5-question grounding recovery component
- [ ] Guardian Flow — one-time code recovery component
- [ ] Unlock Modal — password-first modal wiring Apply button
- [ ] Settings page — password setup, change, removal, recovery mode picker
- [ ] Usage Statistics
- [ ] Focus Timer
- [ ] Resources Page

### Phase 3: Security Hardening
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
