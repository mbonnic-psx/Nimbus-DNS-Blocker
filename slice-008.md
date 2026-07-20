# Slice 008 — Phase 2: Drop the Identity packages (PBKDF2 via Rfc2898DeriveBytes)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the checklist at the bottom.

---

## 1. Goal

`PasswordService` is the *only* reason two NuGet packages ship — `Microsoft.AspNetCore.Identity`
(2.3.9, EOL) and `Microsoft.AspNet.Identity.Core` (2.2.4, legacy, otherwise unused) — and it
uses exactly one type from them: `PasswordHasher<string>`. That's Tech Debt #6.

This slice replaces `PasswordHasher<T>` with the framework-built-in
`System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2`, extracts the hashing into a pure,
MAUI-free `Utilities/PasswordHash.cs` (so it can be unit-tested the same way `HostValidation`
and `HostsSection` are), rewires `PasswordService`'s four call sites, deletes both packages,
and covers the new helper with tests.

**Decisions already made (do not deviate):**

1. **New helper is pure and MAUI-free.** `Utilities/PasswordHash.cs` — a `static` class with
   `Hash(string)` and `Verify(string attempt, string storedHash)`. No `Preferences`, no DI,
   no MAUI types. This is what makes it testable from the plain `net9.0` test project, exactly
   like the two utilities already there (slices 005/006).

2. **Self-describing hash string format.** One `string` stored in Preferences, carrying every
   parameter needed to verify it, so the format can evolve later without a second field:

   ```
   v1.{iterations}.{base64(salt)}.{base64(subkey)}
   ```

   `v1` is a literal version tag (four dot-separated parts total). Parameters:
   `PBKDF2-HMAC-SHA256`, **600000** iterations, **16**-byte random salt, **32**-byte subkey.
   Salt from `RandomNumberGenerator` (CSPRNG). This replaces the opaque single Base64 blob
   `PasswordHasher` produced.

3. **Constant-time comparison.** `Verify` compares the derived subkey with
   `CryptographicOperations.FixedTimeEquals` — never `==`/`SequenceEqual`.

4. **Migration = require re-setup; do NOT try to verify old ASP.NET hashes.** Any stored hash
   that is not in the `v1.` format above makes `Verify` return **false** (never throw). The app
   is pre-release (Phase 3 hasn't happened), so there are no real production hashes to preserve,
   and keeping the dead Identity code path just to read legacy blobs would defeat the point of
   the slice. Guardian users who set up before this build must remove + re-add Guardian mode —
   this goes in the Release Notes, next to the existing guardian-recovery note.

5. **`Verify` is total and never throws.** Null/empty/malformed stored hash → `false`. Bad
   Base64, wrong part count, unparseable iterations → `false`. Callers already treat a `false`
   verify as "wrong / not set"; nothing downstream should see an exception.

6. **No behavioural change to `PasswordService`'s public surface.** Same `IPasswordService`
   methods, same return shapes, same Preference keys (`nimbus_password_hash`,
   `nimbus_guardian_recovery_hash`). Only the hashing mechanism changes. `GenerateGuardianHash`,
   the validation rules in `SetPasswordAsync`, and all the mode/flag logic are untouched.

**Scope discipline:** don't touch the Razor flows (`UnlockModal`, `GuardianFlow`,
`AccountabilityFlow`), `Settings.razor`, the hosts/browser/config services, or the DI
registrations. This slice is the hashing swap and its tests only. Do **not** do the other
Phase 2 items here (rename, TFM trim, service dedup/interfaces, junk removal, daily quote) —
each is its own later slice.

---

## 2. Before you start

Read in full: `Services/PasswordService.cs`, `Services/IPasswordService.cs`,
`Utilities/HostValidation.cs` (for the file style — namespace, XML doc, static class),
`Nimbus.Tests/Nimbus.Tests.csproj`, and `Nimbus.Tests/HostValidationTests.cs` (for the test
style). Note that `PasswordHasher<string>` appears at four call sites in `PasswordService.cs`:
lines ~122 (`HashPassword`, password), ~135 (`HashPassword`, recovery code), ~158
(`VerifyHashedPassword`, password), ~176 (`VerifyHashedPassword`, recovery code).

---

## 3. Edits

### Edit A — new file `Utilities/PasswordHash.cs`

```csharp
using System.Security.Cryptography;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// PBKDF2 password hashing using the framework's own primitives — the single
/// source of truth for both the Guardian password and the recovery code.
/// Replaces the ASP.NET Identity <c>PasswordHasher&lt;T&gt;</c> so the two
/// EOL/legacy Identity packages can be dropped (Tech Debt #6).
///
/// Stored format (one self-describing string; no separate salt field needed):
///     v1.{iterations}.{base64(salt)}.{base64(subkey)}
/// </summary>
public static class PasswordHash
{
    private const string Version = "v1";
    private const int    Iterations = 600_000;   // PBKDF2-HMAC-SHA256, OWASP-range
    private const int    SaltBytes  = 16;
    private const int    KeyBytes   = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Hashes a password with a fresh random salt and returns the self-describing
    /// <c>v1.iterations.salt.subkey</c> string to persist.
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeyBytes);

        return string.Join('.',
            Version,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subkey));
    }

    /// <summary>
    /// Verifies an attempt against a stored hash. Total and constant-time:
    /// returns false for any null/empty/malformed stored hash (including the old
    /// ASP.NET Identity format — those users must re-set their password) and
    /// never throws.
    /// </summary>
    public static bool Verify(string? attempt, string? storedHash)
    {
        if (string.IsNullOrEmpty(attempt) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Version) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt, expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expectedSubkey.Length == 0) return false;

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            attempt, salt, iterations, Algorithm, expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
```

### Edit B — `Services/PasswordService.cs`: rewire the four call sites

**B1.** Replace the top-of-file import:

```csharp
using Microsoft.AspNetCore.Identity;   // ← delete
```
with
```csharp
using Nimbus_Internet_Blocker.Utilities;
```
(Keep `using System.Security.Cryptography;` — `GenerateGuardianHash` still needs
`RandomNumberGenerator`.)

**B2.** `SetPasswordAsync` — password hash (~line 122):

```csharp
var hash = new PasswordHasher<string>().HashPassword("nimbus", password);   // before
var hash = PasswordHash.Hash(password);                                     // after
```

**B3.** `SetPasswordAsync` — recovery-code hash (~line 135):

```csharp
var recoveryHash = new PasswordHasher<string>().HashPassword("nimbus", recoveryCode);  // before
var recoveryHash = PasswordHash.Hash(recoveryCode);                                    // after
```

**B4.** `VerifyPassword` (~lines 156–162): the whole re-hash/`PasswordVerificationResult`
block collapses to a direct bool:

```csharp
var storedHash = Preferences.Get(PREF_HASH, string.Empty);
return PasswordHash.Verify(attempt, storedHash);
```

**B5.** `VerifyRecoveryCode` (~lines 173–180): keep the early `IsNullOrEmpty(storedHash)`
guard and the `.Trim()` (trimming is significant, case/dashes preserved), then:

```csharp
return PasswordHash.Verify(attempt.Trim(), storedHash);
```

**B6.** Update the now-stale doc comments in `PasswordService.cs` **and** `IPasswordService.cs`
that name `PasswordHasher<string>` / `PasswordVerificationResult` / `SuccessRehashNeeded`
(the block comments above `SetPasswordAsync`, `VerifyPassword`, `VerifyRecoveryCode`, and the
`// ── Hashing ──` inline note). State the new mechanism plainly — "PBKDF2 via
`Utilities/PasswordHash`, a self-describing `v1.iterations.salt.subkey` string" — and delete
the `SuccessRehashNeeded` wording, which no longer applies. Do not add tutorial narration
(CLAUDE.md style rule); comments state invariants and why.

### Edit C — `Nimbus-Internet-Blocker.csproj`: delete both packages

Remove these two lines (currently lines 64–65):

```xml
<PackageReference Include="Microsoft.AspNet.Identity.Core" Version="2.2.4" NoWarn="NU1701" />
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.9" />
```

Leave every other `PackageReference` intact. After this, grep the whole repo (excluding
`obj/`/`bin/`) for `AspNetCore.Identity`, `AspNet.Identity`, and `PasswordHasher` — the only
remaining hits should be the unrelated `Microsoft.AspNetCore.Components.*` usings in
`Components/_Imports.razor`. If anything else references the Identity packages, stop and report
it rather than deleting.

### Edit D — `Nimbus.Tests/Nimbus.Tests.csproj`: compile the new util + add its test

Add a third `<Compile Include>` alongside the existing two:

```xml
<Compile Include="..\Utilities\HostValidation.cs" Link="src\HostValidation.cs" />
<Compile Include="..\Utilities\HostsSection.cs"   Link="src\HostsSection.cs" />
<Compile Include="..\Utilities\PasswordHash.cs"   Link="src\PasswordHash.cs" />
```

### Edit E — new file `Nimbus.Tests/PasswordHashTests.cs`

```csharp
using Nimbus_Internet_Blocker.Utilities;
using Xunit;

namespace Nimbus.Tests;

public class PasswordHashTests
{
    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hash = PasswordHash.Hash("Correct-horse-9");
        Assert.True(PasswordHash.Verify("Correct-horse-9", hash));
    }

    [Fact]
    public void Verify_WrongPassword_Fails()
    {
        var hash = PasswordHash.Hash("Correct-horse-9");
        Assert.False(PasswordHash.Verify("correct-horse-9", hash));   // case-sensitive
        Assert.False(PasswordHash.Verify("Wrong-horse-9", hash));
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentHashes()
    {
        // Random per-hash salt ⇒ two hashes of the same input never collide,
        // yet both verify.
        var a = PasswordHash.Hash("Correct-horse-9");
        var b = PasswordHash.Hash("Correct-horse-9");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHash.Verify("Correct-horse-9", a));
        Assert.True(PasswordHash.Verify("Correct-horse-9", b));
    }

    [Fact]
    public void Hash_ProducesExpectedShape()
    {
        var parts = PasswordHash.Hash("Correct-horse-9").Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.Equal("600000", parts[1]);
        Assert.NotEmpty(parts[2]);   // base64 salt
        Assert.NotEmpty(parts[3]);   // base64 subkey
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("v1.600000.onlythreeparts")]
    [InlineData("v2.600000.c2FsdA==.c3Via2V5")]          // unknown version tag
    [InlineData("v1.notanumber.c2FsdA==.c3Via2V5")]      // unparseable iterations
    [InlineData("v1.600000.!!!notbase64!!!.c3Via2V5")]   // bad base64 salt
    // A value shaped like the old ASP.NET Identity blob — must be rejected, not throw.
    [InlineData("AQAAAAEAACcQAAAAEID0lengthyBase64Blob==")]
    public void Verify_MalformedOrLegacyStoredHash_ReturnsFalse(string? storedHash)
    {
        Assert.False(PasswordHash.Verify("anything", storedHash));
    }

    [Fact]
    public void Verify_EmptyAttempt_ReturnsFalse()
    {
        var hash = PasswordHash.Hash("Correct-horse-9");
        Assert.False(PasswordHash.Verify("", hash));
        Assert.False(PasswordHash.Verify(null, hash));
    }

    [Fact]
    public void Verify_TamperedSubkey_Fails()
    {
        var parts = PasswordHash.Hash("Correct-horse-9").Split('.');
        var subkey = System.Convert.FromBase64String(parts[3]);
        subkey[0] ^= 0xFF;                                       // flip one byte
        parts[3] = System.Convert.ToBase64String(subkey);

        Assert.False(PasswordHash.Verify("Correct-horse-9", string.Join('.', parts)));
    }

    [Fact]
    public void Verify_HandlesUnicodeAndDashedRecoveryCode()
    {
        // Recovery codes are dashed (xxxxxx-xxxxxx-…); confirm dashes/unicode survive.
        const string code = "a1B!x9-Zç9$mn-Qw3#er-Ty7&ui";
        var hash = PasswordHash.Hash(code);
        Assert.True(PasswordHash.Verify(code, hash));
        Assert.False(PasswordHash.Verify(code.Replace('-', '_'), hash));
    }
}
```

---

## 4. Docs to update

- **`CLAUDE.md`**
  - **Tech Debt #6** ("Wrong packages"): mark **RESOLVED** — both Identity packages removed;
    `PasswordHasher<T>` replaced by `Utilities/PasswordHash` (PBKDF2-HMAC-SHA256, 600k
    iterations, self-describing `v1` hash string); note the migration (old hashes don't
    verify → re-setup, per Release Notes).
  - **Tech Stack table**, "Password hashing" row: change to
    `PBKDF2 via Rfc2898DeriveBytes.Pbkdf2 (Utilities/PasswordHash.cs)` and drop the
    "ASP.NET Identity packages — slated for removal" parenthetical.
  - **Project Structure**: add `Utilities/PasswordHash.cs` (mark DONE) and the new
    `Nimbus.Tests/PasswordHashTests.cs`; update the `Nimbus.Tests/` note to say it now
    compiles three utility files.
  - **What's Done**: add a bullet — Identity packages dropped, PBKDF2 hashing moved to a
    tested pure utility.
  - **Tech Debt #10** ("No tests" → pure functions): extend the list of covered pure
    functions to include `PasswordHash.Hash`/`Verify`.
- **`PLAN.md`**
  - Phase 2: check the box **"Drop `Microsoft.AspNetCore.Identity` + `Microsoft.AspNet.Identity.Core`;
    replace `PasswordHasher<T>` with `Rfc2898DeriveBytes.Pbkdf2`…"**.
  - **Release Notes** section: add a line — *"Password hashing moved to built-in PBKDF2. Hashes
    from earlier builds are not compatible; Guardian users (and anyone with a password set)
    must re-set their password and recovery code."*

---

## 5. Verify

- This is pure `System.Security.Cryptography` — it builds and tests on Linux/WSL, no MAUI
  workload needed. Run:

  ```
  dotnet test Nimbus.Tests/Nimbus.Tests.csproj
  ```

  All existing tests plus the new `PasswordHashTests` must pass.
- If the MAUI Windows build is available (`dotnet build -f net9.0-windows10.0.19041.0`), run it
  to confirm `PasswordService` compiles with the Identity `using` gone. In a Linux/WSL
  environment the MAUI Windows target likely can't build — if so, say that plainly and instead
  re-read `PasswordService.cs` end-to-end: no `PasswordHasher`/`PasswordVerificationResult`
  references remain, the `using Nimbus_Internet_Blocker.Utilities;` is present, `System.Security.Cryptography`
  is still imported for `GenerateGuardianHash`, and both `Verify*` methods return the helper's
  bool directly.
- Grep the repo (excluding `obj/`/`bin/`) for `PasswordHasher`, `AspNetCore.Identity`,
  `AspNet.Identity` — only the unrelated `Components.*` usings in `_Imports.razor` should
  remain.
- **Do NOT** run git, commit, or push.

### Manual sanity check for the human (Windows, include in your report)

Because the format change invalidates old hashes, verify the re-setup path on Windows:

1. If you already had Guardian mode set up on an old build: after this build, open Settings →
   removing Guardian may need the unlock flow → remove + re-add Guardian mode → a fresh
   recovery code shows.
2. Set a Guardian password → close and reopen the app → Apply blocking → the unlock modal
   accepts the correct password and rejects a wrong one.
3. "Forgot password" → type the recovery code shown at setup → it verifies; a wrong code is
   rejected.
4. Switch to Accountability mode and back to confirm mode flags still behave.

---

## 6. When finished

Summarize for the human: files created/changed (one line each), the `dotnet test` result,
anything that didn't match this spec and how you adapted, and the manual re-setup check above.
Flag the headline consequence in one sentence: **existing password/recovery hashes stop
verifying — affected users must re-set them** (Release Notes updated to say so).
