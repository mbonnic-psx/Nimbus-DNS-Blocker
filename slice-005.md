# Slice 005 — Unit tests for SpliceSection + NormalizeHost (Phase 0 item 5)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the manual test checklist at the bottom.

---

## 1. Goal

Phase 0's last safety item: unit tests for the two pure functions everything else leans on —
`SpliceSection` (hosts-file block replacement) and `NormalizeHost` (domain cleanup). These
tests are the net under slice 006 (Restore reuses the splice path) and Phase 2's refactors.

**The structural problem this slice must solve first:** both functions are `private` inside
classes that can't be compiled outside MAUI (`HostsFileService` constructor-depends on the
MAUI-bound config services; `NormalizeHost` is duplicated privately in `PresetService` and
`CustomSitesService`, which use `FileSystem`/`Preferences`). A plain `net9.0` xUnit project
can't reference any of that. So this slice **extracts the two pure functions into
`Utilities/`** and has the test project compile those two source files directly.

This deliberately pulls forward one narrow piece of Phase 2 ("deduplicate `NormalizeHost`
into `Utilities/HostValidation.cs`") — CLAUDE.md already names `HostValidation.cs` as the
intended home, and testing it is impossible otherwise. **Scope discipline:** extract ONLY
`NormalizeHost` and the splice logic. Do NOT dedupe the seed plumbing, do NOT add service
interfaces, do NOT touch `Blocking.razor`, `Settings.razor`, or any flow component.

---

## 2. Before you start

Read in full: `Utilities/HostValidation.cs` (currently an empty placeholder),
`Services/HostsFileService.cs`, `Services/PresetService.cs`,
`Services/CustomSitesService.cs`, `Nimbus-Internet-Blocker.csproj`.

Grep before editing: `NormalizeHost`, `SpliceSection`, `SectionBegin`, `SectionEnd` — the
expected usage sites are only the three service files. If you find others, stop and report.

---

## 3. Edits — production code (extraction, no behaviour change)

### Edit A — `Utilities/HostValidation.cs` (replace the empty placeholder)

```csharp
using System.Text.RegularExpressions;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Shared host/domain normalization — the single source of truth used by both
/// config services (previously duplicated privately in each).
/// </summary>
public static class HostValidation
{
    /// <summary>
    /// Normalizes user input to a bare lowercase host: strips scheme, path,
    /// port, surrounding whitespace, and trailing dots. Returns "" for
    /// null/blank input.
    /// </summary>
    public static string NormalizeHost(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var s = input.Trim();

        s = Regex.Replace(s, @"^\s*https?://", "", RegexOptions.IgnoreCase);

        var slash = s.IndexOf('/');
        if (slash >= 0) s = s[..slash];

        var colon = s.IndexOf(':');
        if (colon >= 0) s = s[..colon];

        return s.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
```

(The body is the existing implementation moved verbatim — do not "improve" it; the tests
pin current behaviour.)

### Edit B — new file `Utilities/HostsSection.cs`

Move the marker constants and `SpliceSection` out of `HostsFileService` unchanged:

```csharp
using System.Text;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Pure logic for the Nimbus-managed block inside the Windows hosts file.
/// Lines outside the delimited section are never modified.
/// </summary>
public static class HostsSection
{
    public const string BeginMarker = "# --- Nimbus-managed section BEGIN ---";
    public const string EndMarker   = "# --- Nimbus-managed section END ---";

    /// <summary>
    /// Replaces the existing Nimbus-managed section in <paramref name="hostsContent"/>
    /// with <paramref name="newSection"/>. When no complete section is found
    /// (first run, or damaged markers), the new section is appended after the
    /// current content instead — never destructively.
    /// </summary>
    public static string Splice(string hostsContent, string newSection)
    {
        // ← move the ENTIRE body of HostsFileService.SpliceSection here verbatim,
        //    substituting SectionBegin/SectionEnd with BeginMarker/EndMarker.
    }
}
```

### Edit C — `Services/HostsFileService.cs`

- Add `using Nimbus_Internet_Blocker.Utilities;`.
- Delete the `SectionBegin`/`SectionEnd` constants and the `SpliceSection` method.
- `BuildSection` uses `HostsSection.BeginMarker` / `HostsSection.EndMarker`.
- The apply path calls `HostsSection.Splice(hostsContent, section)`.
- No behavioural change anywhere.

### Edit D — `Services/PresetService.cs` and `Services/CustomSitesService.cs`

In each: add `using Nimbus_Internet_Blocker.Utilities;`, delete the private `NormalizeHost`
method, and change call sites to `HostValidation.NormalizeHost(...)`. Remove the now-unused
`using System.Text.RegularExpressions;`. No behavioural change.

### Edit E — `Nimbus-Internet-Blocker.csproj` (critical)

The MAUI project globs `**/*.cs`, so the new test folder would otherwise be compiled into
the app and break the build. Add:

```xml
<ItemGroup>
    <!-- Test project is built separately; keep it out of the app's compile glob -->
    <Compile Remove="Nimbus.Tests\**\*.cs" />
    <None Remove="Nimbus.Tests\**" />
</ItemGroup>
```

---

## 4. Edits — test project

### Edit F — new file `Nimbus.Tests/Nimbus.Tests.csproj`

Plain `net9.0` (NOT a MAUI TFM) so tests run on both Windows and Linux. It compiles the two
utility source files directly instead of referencing the MAUI project:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
        <PackageReference Include="xunit" Version="2.9.2" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    </ItemGroup>

    <ItemGroup>
        <Compile Include="..\Utilities\HostValidation.cs" Link="src\HostValidation.cs" />
        <Compile Include="..\Utilities\HostsSection.cs"   Link="src\HostsSection.cs" />
    </ItemGroup>

</Project>
```

(If restore rejects a package version, use the nearest available — versions are not
load-bearing. Do not add the test project to the `.sln`; it runs by path.)

### Edit G — `Nimbus.Tests/HostValidationTests.cs`

`[Theory]`/`[InlineData]` table covering, at minimum:

| Input | Expected |
|---|---|
| `null`, `""`, `"   "` | `""` |
| `"  Example.COM  "` | `"example.com"` |
| `"https://example.com"` | `"example.com"` |
| `"HTTP://EXAMPLE.COM"` | `"example.com"` |
| `"https://www.example.com/path?q=1#frag"` | `"www.example.com"` |
| `"example.com/deep/path"` | `"example.com"` |
| `"example.com:443"` | `"example.com"` |
| `"https://example.com:8080/x"` | `"example.com"` |
| `"example.com."` | `"example.com"` |
| `"sub.domain.example.com"` | `"sub.domain.example.com"` |
| `"ftp://example.com"` | `"ftp"` *(colon truncation — documents current behaviour; comment it as such)* |

### Edit H — `Nimbus.Tests/HostsSectionTests.cs`

**Assertion style (important):** `Splice` joins with `Environment.NewLine`, so raw string
equality is platform-dependent. Add a private helper and assert on **lines**:

```csharp
private static string[] Lines(string s) =>
    s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
```

Cover these behaviours (all verified against the current implementation — the tests pin
what the code does today):

1. **No markers → append.** Given content without markers, output starts with the original
   content (trimmed of trailing blank lines), then a blank separator line, then the new
   section; every original line is still present, in order.
2. **Both markers → replace.** Content with a stale Nimbus block between other lines: lines
   before BEGIN and after END are untouched and in order; stale block lines are gone; the
   new section's lines appear exactly once.
3. **CRLF input** and **lone-`\r` input** both splice identically to LF input (compare via
   `Lines`).
4. **Indented/padded markers** (`"   # --- Nimbus-managed section BEGIN ---  "`) are still
   recognized (markers are matched after `Trim()`).
5. **Duplicate BEGIN markers before END:** the span from the *first* BEGIN through the END
   is replaced — the inner duplicate marker is removed with it.
6. **Reversed markers (END appears before BEGIN):** falls back to the append path — no
   original line is lost (assert every original line still present). Comment: quirky but
   deliberately non-destructive; the invariant that matters is "never delete non-Nimbus
   lines".
7. **Empty content `""` → append** works and the output contains the full new section.
8. **Section at end-of-file without trailing newline** is still replaced correctly.
9. **Idempotence:** splicing the same section twice equals splicing it once
   (`Lines(once)` sequence-equals `Lines(twice)`).

Build each test's fake hosts content from small string arrays joined with the ending under
test (don't hand-write giant literals), and use realistic lines
(`"0.0.0.0         facebook.com"`, comments, a `127.0.0.1 localhost` line outside the block).

---

## 5. Docs to update

- **`CLAUDE.md`**
  - Tech Debt "No tests" item: mark **RESOLVED** for `Splice`/`NormalizeHost`; note the
    test project `Nimbus.Tests/` (plain net9.0, compiles the two utility files directly).
  - Tech Debt dedup item (#5): note `NormalizeHost` is now shared via
    `Utilities/HostValidation.cs`; remaining dedup is the seed plumbing only.
  - Project structure: `Utilities/HostValidation.cs` no longer a placeholder; add
    `Utilities/HostsSection.cs` and `Nimbus.Tests/`.
  - "What's Done": add unit tests for splice + normalization.
- **`PLAN.md`** → Phase 0: check the **"Unit tests"** box (`- [x]`). In Phase 2, annotate
  the dedup bullet that the `NormalizeHost` half is already done (slice 005).

---

## 6. Verify

- Run `dotnet test Nimbus.Tests/Nimbus.Tests.csproj` **if** the `dotnet` CLI exists in this
  environment (`which dotnet` first). It may not be installed in this WSL — if so, state
  that plainly in your report; do NOT claim tests pass without running them. The plain
  net9.0 test project itself is Linux-compatible, so if dotnet IS available, tests must
  pass before you finish.
- Re-read every edited file: no remaining private `NormalizeHost` in either service, no
  `SpliceSection`/`SectionBegin`/`SectionEnd` left in `HostsFileService`, csproj glob
  exclusion present, using-directives correct.
- **Do NOT** run git, commit, or push.

### Manual checklist for the human (include in your report)

On Windows:

1. `dotnet test .\Nimbus.Tests\Nimbus.Tests.csproj` → all tests pass.
2. `dotnet build -f net9.0-windows10.0.19041.0` → app still builds (proves the csproj glob
   exclusion works and the extraction compiles).
3. Quick smoke: run the app → add/normalize a custom site (e.g. paste
   `https://Example.com:443/x`) → still normalizes to `example.com`; Apply still writes the
   hosts block correctly (extraction changed no behaviour).

Optional (recommended): install the .NET 9 SDK inside WSL (`sudo apt install dotnet-sdk-9.0`
or Microsoft's install script) so future slices can run these tests without the Windows
round-trip.

---

## 7. When finished

Summarize for the human: files changed/created (one line each), whether tests were actually
run and their results, anything that didn't match this spec and how you adapted, and the
manual checklist above.
