# Nimbus — Product & Business Plan

Two tracks. **Track 1 is a free Windows blocker released on GitHub** — the trust-builder
and funnel. **Track 2 is a paid Cold Turkey-style blocker** — only built if Track 1
produces real demand (waitlist signups / users asking to pay).

Guiding positioning: *"the blocker for people fighting compulsion, not a productivity
toy."* Compete on the human side (compassionate friction, guardian accountability,
privacy/local-first) — **not** on tamper-proof toughness, where Cold Turkey wins.

Rules of the road:
- Don't start a phase until the previous one's checkboxes are done.
- Don't build Track 2 until the Track 1 validation gate passes.
- Update CLAUDE.md's "What's Done / Tech Debt" sections as items land.

---

## Track 1 — Free App (v1.0 release)

### Phase 0 — Make the core trustworthy (~1–2 weeks)

**Phase 0 complete.** (2026-07-17)

Fix the verified bugs before anything new. All are listed with detail in CLAUDE.md
"Known Bugs / Tech Debt".

- [x] **Guardian recovery fix** — at setup, hash the recovery code (PBKDF2, same
      machinery as the password) and store the hash in Preferences; `GuardianFlow`
      verifies the *typed* code against that stored hash instead of generating a fresh
      one on screen. (~1 day; the single worst flaw in the app)
- [x] **Truthful apply feedback** — `IHostsFileService.ApplyAsync` returns `bool`;
      `Blocking.razor` branches on it; delete the dead `_preApplySnapshot` logic. (~½ day)
- [x] **Safe persistence** — atomic saves (temp file + `File.Replace`) in both config
      services; failed `LoadAsync` must not return an empty root that a later save
      persists; save failures surface via snackbar. (~1 day)
- [x] **Restore / Unblock All** — Settings button that empties the Nimbus section (or
      restores `hosts.nimbus.bak`) and flushes DNS. Replaces "Coming soon". (~1 day)
- [x] **Unit tests** for `SpliceSection` (missing/reversed/duplicate markers, CRLF/LF)
      and `NormalizeHost`. Nothing else needs tests for v1. (~1 day)

### Phase 1 — Close the DoH bypass (~2–3 days)

The feature that makes the app actually work in modern browsers, and the headline for
the release: "blocks sites even with Secure DNS enabled."

- [x] New `BrowserPolicyService` (behind an interface) that writes registry policies on
      apply and removes them on Restore/Unblock All:
      - Chrome: `HKLM\SOFTWARE\Policies\Google\Chrome` → `DnsOverHttpsMode = "off"`
      - Edge:   `HKLM\SOFTWARE\Policies\Microsoft\Edge` → `DnsOverHttpsMode = "off"`
      - Firefox: `HKLM\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS` → `Enabled = 0`
- [ ] Test against real installed browsers (verify blocked site fails with policy on,
      resolves with it off).
- [x] Settings toggle + honest explanation text ("Nimbus disables browser Secure DNS so
      system blocking works; removed when you unblock everything").

### Phase 2 — Cleanup & rename (~1 week)

- [ ] **Pick the real name** (current name describes the mechanism, not the benefit).
      Quick trademark + domain search *before* release. Update `ApplicationTitle`,
      `ApplicationId` (`com.companyname.*` must go), window title, README.
- [x] Trim `TargetFrameworks` to Windows only; delete unused `Platforms/` folders;
      remove the CA1416 pragma. *(There was never a CA1416 pragma — the
      `[SupportedOSPlatform("windows")]` attributes are correct and were left in place.)*
- [x] Drop `Microsoft.AspNetCore.Identity` + `Microsoft.AspNet.Identity.Core`; replace
      `PasswordHasher<T>` with `Rfc2898DeriveBytes.Pbkdf2` (keep verifying old-format
      hashes or just require re-setup — one-time decision, note it in release notes).
- [x] Deduplicate `PresetService`/`CustomSitesService` shared logic into
      `Utilities/HostValidation.cs`; fix the wrong fallback seed shape and wrong-service
      log messages; add interfaces for both services.
      *(`NormalizeHost` half + seed-shape/log-message fixes landed in slice 005. Slice 010
      closed the rest: seed plumbing — `GetLivePath`, `EnsureLiveFileExistsAsync`,
      `ReadSeedTextAsync` — moved into abstract `Services/SeedBackedConfigService.cs`;
      both services now implement `IPresetService`/`ICustomSitesService`.)*
- [ ] Junk removal: ~~stray `"exclude"` block in `presets.seed.json`, empty `JsonLoad.cs`,
      move `rain.js` out of `css/`~~ done (slice 009); delete tutorial comments and
      stale personal paths remains open.
- [x] Quote of the day = seeded by date (so it's actually daily); fix typos/inconsistent
      quoting in the quote list; AccountabilityFlow Q5 validates quote text only (not
      author line).
      *(Slice 011: `QuoteService` picks deterministically via
      `Utilities/QuoteText.IndexForDate` (tested); quotes restructured to
      `Quote(Text, Author)` records, fixing the fused/escaped-string typos and
      inconsistent shape; Q5 shows and validates `CurrentQuoteText` only.)*

### Phase 2.5 — Launch elevation & tamper-friction (Track 1 hardening)

Lighter, UI-side precursors to the Track 2 service (Phase 5). They raise the bypass
bar past *casual* tampering without a background service — still "friction, not
enforcement": the same Administrator who runs the app can always undo them, so this
does not contradict the positioning (we don't claim tamper-proof toughness).

- [x] **Force admin at launch** — `Platforms/Windows/app.manifest` now requests
      `requireAdministrator`, so Windows shows the UAC consent prompt *before* the
      process starts: decline → the app never opens; accept → it runs elevated (which
      the hosts write and DoH policies already require). Only valid because the app is
      unpackaged (`WindowsPackageType=None`) — MSIX-packaged apps can't force
      elevation this way. Replaces the previous behavior (ran un-elevated, only
      `HostsFileService.IsElevated`-checked at Apply time). Dev note: debugging now
      needs Visual Studio / `dotnet run` launched elevated.
- [ ] **ACL-lock the hosts file** — on Apply, tighten the hosts-file DACL so
      interactive Users/Administrators lose *write* (a Notepad save → Access Denied);
      the elevated app unlocks → writes (wrapping the existing `AtomicFile` temp-write
      + `File.Replace`) → re-locks. **Critical gotcha:** preserve *read* for `SYSTEM`
      and `NT AUTHORITY\NetworkService`, or the DNS Client service (Dnscache) can't
      read hosts and the blocking itself breaks. `RestoreAsync` / uninstall **must**
      restore a sane default DACL (otherwise the user is left needing `takeown` to
      edit their own hosts file). Gate behind a Settings toggle, same pattern as the
      DoH policy. Honest limit: an admin can `takeown` / Safe-Mode around it — real
      enforcement is Phase 5's LocalSystem service + `FileSystemWatcher` re-write.

### Phase 3 — Release (~1 week)

- [ ] Release build + installer or zip; test on a clean Windows VM (no dev tools).
- [ ] README rewrite: what it does, honest bypass model ("friction, not enforcement"),
      DoH handling, screenshots.
- [ ] Tag **v1.0** on GitHub with binaries.
- [ ] Decide and document the license (open-core is the default assumption: free app
      stays open, paid tamper-resistance is closed).

## Release Notes

- Guardian recovery verification changed: guardian users who set up before this build
  must remove and re-add Guardian mode to get a verifiable recovery code; old recovery
  codes were never stored and cannot be verified.
- Password hashing moved to built-in PBKDF2. Hashes from earlier builds are not
  compatible; Guardian users (and anyone with a password set) must re-set their
  password and recovery code.

### Phase 4 — Funnel (non-code, ongoing)

- [ ] One-page site: free download + waitlist email box for the paid version
      ("locked sessions, schedules, URL-level blocking, verified guardian mode").
- [ ] Post in 2–3 target communities (r/nosurf, digital-minimalism, recovery spaces) —
      honestly, as the builder; ask what people tried and abandoned.
- [ ] Write 1–2 evergreen SEO posts ("how to actually block X on Windows in 2026").
- [ ] Talk to every user who emails. Churned Cold Turkey/Freedom users are the spec
      for Track 2.

### ✅ Validation gate for Track 2

Proceed to Track 2 only if something like this is true within ~2–3 months of release:
meaningful waitlist (>100), or repeated unprompted "I'd pay for X" requests, or steady
download growth. Otherwise: keep improving Track 1, or stop — that's a valid outcome.

---

## Track 2 — Paid Blocker (build only after the gate)

Order within Track 2 is driven by what free users actually ask to pay for; the default
guess below puts scheduling/URL blocking before deep tamper-proofing.

### Phase 5 — Windows service architecture (4–6 wks full-time / 2–3 mo part-time)

The pivotal step: enforcement moves out of the UI into a service the user doesn't
casually control. UI stops needing admin.

- [ ] .NET Worker Service running as LocalSystem; owns hosts writes, DoH policies,
      block-session state.
- [ ] Named-pipe IPC between MAUI UI and service.
- [ ] Config moves to `%ProgramData%` with ACLs (only SYSTEM writes).
- [ ] `FileSystemWatcher` on hosts file — service rewrites its section if edited out.
- [ ] Service auto-restart recovery options + deny-stop DACL.
- [ ] Real installer (Inno Setup or WiX) — services can't be xcopy-deployed.
- [ ] **Security review of the IPC and any update path before shipping** — code running
      as SYSTEM is a machine-compromise vector if wrong.

### Phase 6 — Timed & locked sessions (2–4 wks / 1–2 mo)

- [ ] Block sessions with end times enforced by the service; recurring weekly schedules.
- [ ] "Locked" settings while a session is active (Accountability/Guardian flows gate
      early exit).
- [ ] Clock-change resistance: track elapsed monotonic time, not wall-clock deadlines.

### Phase 7 — Browser extension / URL-level blocking (6–8 wks / 2–3 mo)

- [ ] Chrome/Edge MV3 extension (`declarativeNetRequest`) + Firefox port.
- [ ] Native messaging host connected to the service (rules come from the service).
- [ ] Block page with the quote/reflection UX instead of a connection error.
- [ ] Force-install + prevent-removal via `ExtensionInstallForcelist` policies.
- [ ] Store publication (Chrome Web Store, AMO) — calendar time, start early.

### Phase 8 — App blocking + stats (3–4 wks / 1–2 mo)

- [ ] Process watch (WMI event subscription) closes blocklisted executables during
      sessions; match by hash/signature, not just filename.
- [ ] Streaks / usage stats on the dashboard (people pay for visible progress).

### Phase 9 — Tamper resistance (ongoing backlog, never "done")

- [ ] Prevent uninstall during active block; re-assert registry policies if deleted;
      optional Task Manager/regedit blocking during sessions.
- [ ] Goal is "bypass takes 20+ minutes," not "unbreakable." Market it that way.

---

## Business Setup (do alongside Track 2, not before the gate)

- [ ] **Pricing:** one-time purchase $20–40 with paid major upgrades (matches the
      local-first/no-server story; a subscription would require sync/servers).
- [ ] **Merchant of record:** Paddle or Lemon Squeezy (they handle global VAT/sales tax).
- [ ] **Entity + EULA:** LLC (or local equivalent) before charging; EULA explicitly
      covers "we modify your hosts file / you may lock yourself out."
- [ ] **Code signing cert** (~$100–400/yr) — mandatory for the paid installer; unsigned
      service installers get killed by SmartScreen/AV. (Free GitHub release can live
      with the warning.)
- [ ] **Crash reporting + auto-update** (opt-in, privacy-respecting) from paid v1.
- [ ] **Support policy for "I'm locked out and need in NOW"** — decide it as a product
      rule (e.g., guardian-verified unlock only), not case-by-case support.
- [ ] **Marketing honesty rule:** never claim unbypassable. Oversold friction tools +
      compulsive audience = angry refunds.

## Expectations (read when discouraged)

Lifestyle business, not a startup: Cold Turkey is ~one person over a decade. A good
outcome after year one is a few thousand dollars/month. The failure mode to avoid is
six silent months building tamper-resistance for a product nobody has paid for — which
is exactly what the validation gate exists to prevent. The moat is trust + positioning
(signed, honest, privacy-first, visibly maintained), not technical toughness.
