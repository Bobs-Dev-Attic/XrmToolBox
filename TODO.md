# TODO — Prioritized Remediation Plan

Derived from [`docs/REVIEW.md`](docs/REVIEW.md). IDs in brackets link to the full
finding (with `file:line` and rationale). Ordered by **risk × leverage**, then by
"unblocks other work". Checkboxes are for tracking.

> Verification note: this is a Windows-only .NET Framework / WinForms app with **no
> test project**. Changes must be built with MSBuild/VS 2022 and verified manually on
> Windows. Do not report any fix as "tested" from a non-Windows host.

Legend: 🔴 Critical · 🟠 High · 🟡 Medium · ⚪ Low · 💡 Strategic

---

## P0 — Do first (security-critical, protects the brand)

- [ ] 🔴 **Verify update integrity before overwriting the app.** [SEC-C1]
  Code-sign releases; in `AutoUpdater.cs` verify Authenticode (and/or a published
  SHA-256) on the downloaded binaries *before* `CopyDirectoryContent`. Constrain the
  release `DownloadUrl` host to an allowlist (`*.xrmtoolbox.com`) in `New/NewForm.cs`.
- [ ] 🔴 **Verify plugin package signatures / pin trusted authors.** [SEC-C2]
  Use `SignedPackageVerifier`/`GetTrustResultAsync` in `StoreFromPortal.cs` before
  extracting; reject unsigned/untrusted packages (allow override only with an explicit
  user consent dialog that names the author).
- [ ] 🔴 **Pin TLS and stop accepting `http`/`file` for remote installs.** [SEC-H3, SEC-H1]
  Set `ServicePointManager.SecurityProtocol = Tls12 | Tls13` early in `Program.cs`;
  reject non-https `DownloadUrl`/repo schemes in `ToolLibrary.cs:225-249,447-520`
  (keep `file` only behind an explicit "local/offline feed" opt-in).
- [ ] 🟠 **Add explicit Zip-Slip containment checks.** [SEC-M1]
  After `Path.Combine`, assert the full path starts with the intended root in all four
  extractors (`StoreFromPortal.cs`, `ToolLibrary.cs`, `StoreForm - Copier.cs`, `AutoUpdater.cs`).

## P1 — High value, bounded effort

- [ ] 🟠 **Encrypt proxy secrets at rest; mark plain settings `[XmlIgnore]`.** [SEC-H2, SEC-M5, SEC-L1]
  DPAPI (`ProtectedData`, `CurrentUser`) or Windows Credential Manager for the proxy
  password; remove the plaintext path in `Options.cs`/`OptionsDialog.cs`; drop the
  hardcoded `c:\temp` in `ProxySettingsHelper.cs`.
- [ ] 🟠 **Scrub exception text before logging/clipboard.** [SEC-M2]
  Redact token/secret patterns in `ErrorDetail.cs` + `LogManager` before writing
  `Logs\*.log` or `Clipboard.SetText`.
- [ ] 🟠 **Fix the two biggest leaks.** [PERF-H1, PERF-H2]
  `ImageCache`: dispose images on `Clear()/Refresh()`, bound the cache, fix the inverted
  `Clean()` guard. `NewForm.pluginControlStatuses`: remove entries on tab close.
- [ ] 🟠 **Detach plugin events on tab close.** [PERF-H3]
  Add matching `-=` for every `+=` in `NewForm` within `FormClosed`.
- [ ] 🟠 **Stop `Thread.Abort()` on search; remove the 200 ms render sleep.** [CQ-4, UX-2]
  Replace with a debounced `CancellationToken` async search in `PluginsForm2.cs`.
- [ ] 🟠 **Friendly, actionable connection/tool errors.** [UX-1, UX-3]
  Normalize SDK fault messages in `NewForm.cs:964,1434`; make `ErrorDetail` show the real
  message by default and size via layout, not pixel math. Fix "occured" typo.

## P2 — Medium (quality, perf, UX polish)

- [ ] 🟡 **Dispose per-paint GDI objects / cache static pens & brushes.** [PERF-H4, PERF-L]
  All `*Model.cs`, `NotificationControl.cs`, `PluginsForm2.cs` draw handlers, etc.
- [ ] 🟡 **Single shared `HttpClient`; remove per-call clients.** [PERF-M1]
- [ ] 🟡 **Move startup IO off the UI thread; stop eager full plugin load where possible.** [PERF-H5]
- [ ] 🟡 **Replace sync-over-async (`.Wait()`/`.GetAwaiter().GetResult()`) with real async.** [PERF-H6]
- [ ] 🟡 **Dispose `FileSystemWatcher`; audit static-event subscribers.** [PERF-M2, PERF-M3]
- [ ] 🟡 **Migrate ADAL → MSAL.** [SEC-M4] (Coordinate: touches auth; larger change.)
- [ ] 🟡 **Log swallowed exceptions at Verbose instead of empty `catch {}`.** [CQ-3]
- [ ] 🟡 **Onboarding/progress:** determinate startup progress + Cancel; non-blocking category nav with retry. [UX-6, UX-4]

## P3 — Low / housekeeping (also helps agents & token cost)

- [ ] ⚪ **Delete `MigrationBackup/` and `.gitignore` it.** [CQ-2]
- [ ] ⚪ **Remove dead `StoreForm - Copier.*`; consolidate `Forms/` vs `New/` duplicates.** [CQ-1]
- [ ] ⚪ **Accessibility:** contrast validation on tool-card colors, keyboard access + larger hit targets, layout that survives high DPI/large fonts. [UX-7]
- [ ] ⚪ **Unify "plugin" vs "tool" terminology in user-facing strings.** [UX-5]
- [ ] ⚪ **Replace pixel-math dialog sizing / manual wheel-scroll loop / modal copy toast.** [UX-3, UX-9]
- [x] ⚪ **Replace the generic Azure `.github/copilot-instructions.md`.** [CQ-6] — done (points to `AGENTS.md`).

## 💡 Strategic (plan, don't rush)

- [ ] 💡 **Introduce a test project** (xUnit/NUnit) for non-UI logic: `ToolLibrary`,
  `StoreFromPortal`, `ImageCache`, version parsing. Biggest long-term quality lever. [CQ-5]
- [ ] 💡 **Signed-plugin allowlist + managed/offline enterprise feed** — unlocks enterprise
  adoption and closes the supply-chain gap end-to-end. [PRIV-4, PRIV-5]
- [ ] 💡 **Consider plugin isolation** (out-of-process / AppContainer) so a bad plugin
  can't trivially read live admin credentials. [SEC-C3]
- [ ] 💡 **Explicit, documented telemetry consent + retention; rotating IDs.** [PRIV-1, SEC-L3]
- [ ] 💡 **Long-term runtime plan: .NET 8/9** (WinForms supported) for modern security/perf
  defaults. [Business §F]

---

### Sequencing rationale

P0 removes the arbitrary-code-execution paths that put live admin credentials at risk —
highest risk, and the enterprise-trust unlock. P1 are high-value, mostly local fixes
(secrets at rest, the two dominant leaks, the search-box bug, error UX). P2 is broad but
mechanical cleanup. P3 is housekeeping that also reduces confusion and token cost for
future human/AI work. Strategic items need design before code.
