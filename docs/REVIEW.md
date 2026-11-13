# XrmToolBox — Engineering, Security, UX & Business Review

**Date:** 2026-11-13 · **Scope:** this repository only (the external
`McTools.Xrm.Connection` credential library is out of scope and noted where relevant).
**Method:** static review of ~44k LOC. Findings cite `file:line`. Each has a
stable **ID** so `TODO.md`, PRs, and agents can reference it without re-analysis.

Severity: **Critical / High / Medium / Low**. "Confirmed" = read at the cited
line; "Suspected" = strong signal, full lifecycle not traced.

---

## A. Security — pentester / white-hat perspective

The entire download → extract → load pipeline (plugins **and** self-update) runs
with **TLS as the only integrity control**. There is no hash, NuGet-signature, or
Authenticode verification anywhere before code is written into the app's trust
boundary and executed in-process with the user's live Dataverse admin context.

### Critical

- **SEC-C1 — Auto-update overwrites the running app from an unverified zip.**
  `XrmToolBox.AutoUpdater/AutoUpdater.cs:70-77,138-156`. Downloads `packageUrl`,
  `ZipFile.ExtractToDirectory`, then copies every file over the live install dir.
  No hash/signature/Authenticode check. URL source: `XrmToolBox/New/NewForm.cs:1646-1678`
  takes `release.DownloadUrl` verbatim from releases JSON and passes it through
  `New/NewVersionForm.cs:54 Process.Start(...)`; the only gate is `.EndsWith(".zip")`.
  **Impact:** anyone able to influence that JSON (compromised/MITM'd portal, DNS,
  proxy) or the download host achieves arbitrary code execution + full host
  takeover. `DownloadUrl` is not even constrained to the xrmtoolbox.com host.

- **SEC-C2 — Plugin NuGet packages installed with no signature verification.**
  `XrmToolBox.PluginsStore/StoreFromPortal.cs:375-496`. `PackageArchiveReader` is
  used read-only; `.nupkg` signature validation (`SignedPackageVerifier` /
  `GetTrustResultAsync`) is never called. Extracted DLLs are copied to
  `Paths.PluginsPath` and loaded on next scan. **Impact:** a malicious package on
  the feed, or a tampered/MITM NuGet mirror, runs in-process. No author pinning.

- **SEC-C3 — Plugins load full-trust, in-process, auto-instantiated by reflection.**
  `XrmToolBox.Extensibility/Manifest/ManifestLoader.cs:101-113`,
  `XrmToolBox/PluginManagerExtended.cs:95-98,246-254`. MEF `DirectoryCatalog` over a
  user-writable folder + `Activator.CreateInstance` + `Assembly.LoadFrom`/
  `ReflectionOnlyLoadFrom`. No AppDomain isolation, CAS, strong-name, or evidence.
  **Impact:** any DLL dropped in `Plugins\` = arbitrary code with the user's admin
  credentials. Combined with SEC-C2 this is the core supply-chain risk.

### High

- **SEC-H1 — Custom repos accept `http` and `file` schemes; `DownloadUrl` honored verbatim.**
  `XrmToolBox.ToolLibrary/ToolLibrary.cs:66-74,225-249,447-520`. `LoadTools` and
  `DownloadPackage` permit `http` (cleartext, MITM-able) and `file`/UNC sources for
  attacker-controlled metadata. A shared/socially-engineered custom repo delivers a
  backdoored package over http or a UNC share → code execution.

- **SEC-H2 — Proxy password persisted in plaintext XML.**
  `XrmToolBox/Options.cs:669-670,814-823` via `XmlSerializerHelper.cs:56-71`. The
  `Password` property has **no `[XmlIgnore]`** and no encryption; written cleartext
  to `Settings\XrmToolBox.Settings.xml`. Often a corporate `DOMAIN\user` credential
  (`WebProxyHelper.cs:25-32`). A second cleartext path exists via
  `Forms/OptionsDialog.cs:83,102` into the connections file.

- **SEC-H3 — No TLS/`ServicePointManager` hardening.** `XrmToolBox/Program.cs:349-355`
  sets only pool/Nagle options; `SecurityProtocol` is never pinned, no cert pinning.
  Because TLS is the *only* integrity control (SEC-C1/C2 have no second line of
  defense), any downgrade/interception (corporate MITM proxy, user root cert, the
  net462 updater) turns disclosure into **code execution**.

### Medium

- **SEC-M1 — Zip Slip / path traversal in four extractors.** Destinations built from
  archive-controlled entry names with no post-`Path.Combine` containment check:
  `StoreFromPortal.cs:434-443`, `ToolLibrary.cs:264-266`, `StoreForm - Copier.cs:404,413`,
  and `AutoUpdater.cs:144` (`ZipFile.ExtractToDirectory` on net462 predates the
  framework traversal guard). Widens write primitives (e.g. Startup folder, sibling DLLs).

- **SEC-M2 — Exception text (may contain secrets) written to plaintext log + clipboard.**
  `XrmToolBox.Extensibility/Forms/ErrorDetail.cs:27,126-147`; sink `LogManager.cs:139-143`.
  CRM/ADAL faults frequently embed connection strings/token material in
  `Exception.Message`; these are logged to `Logs\*.log` and copied to clipboard
  unfiltered. (`LogManager` itself only logs `ConnectionName` intentionally.)

- **SEC-M3 — No Mark-of-the-Web / zone / publisher check before MEF load.**
  `PluginManagerExtended.cs:90-98`. Any DLL in `Plugins\` is composed regardless of
  origin; the MOTW hint at `Forms/WelcomeDialog.cs:149` is only advisory for the app zip.

- **SEC-M4 — ADAL (end-of-life auth library) in use.**
  `Microsoft.IdentityModel.Clients.ActiveDirectory 5.3.0` (`packages.config`). ADAL
  was retired in June 2023; no security fixes. Migrate to **MSAL**
  (`Microsoft.Identity.Client`). Affects token acquisition robustness and compliance.

- **SEC-M5 — Proxy creds round-tripped through NuGet config with a hardcoded `c:\temp` path.**
  `XrmToolBox.PluginsStore/ProxySettingsHelper.cs:11,21,58`
  (`Settings.LoadDefaultSettings("c:\\temp")`). Fragile location + lightly-protected
  `http_proxy.password` storage.

### Low

- **SEC-L1 — Proxy password held indefinitely as an immutable `String` in a static singleton** (`Options.cs:670`, `Controls/ProxyControl.cs:36,49`) — never zeroed; recoverable from a memory/crash dump. No `SecureString`.
- **SEC-L2 — Updater builds a child process command line by string-concatenating `downloadUrl` + args** (`AutoUpdater.cs:80-122`, `New/NewVersionForm.cs:52-54`) — fragile quoting around an attacker-influenced value.
- **SEC-L3 — Telemetry sends a persistent pseudonymous `InstallationId`** enabling cross-session device correlation (`AppCode/AppInsights/AppInsights.cs`). Opt-out exists; see Privacy (§E).

**Recommended controls (defense in depth):** sign releases + verify Authenticode
on downloaded binaries before overwrite; verify `.nupkg` signatures and pin trusted
authors; constrain `DownloadUrl`/update host to an allowlist; reject `http`/`file`
for remote installs; pin `SecurityProtocol = Tls12|Tls13`; add explicit Zip-Slip
containment checks; encrypt proxy secrets with **DPAPI** (`ProtectedData`,
`CurrentUser` scope) and mark them `[XmlIgnore]` in the plain settings file;
scrub exception messages before logging/clipboard.

---

## B. Memory & performance — senior-engineer perspective

### High

- **PERF-H1 — `ImageCache` leaks every image on refresh; unbounded cache.**
  `XrmToolBox.ToolLibrary/AppCode/ImageCache.cs:120-132` (`Cache.Clear()` disposes
  nothing), `:31-59` (`Image.FromStream` never disposed), `:85-118` (`ImageConverter`
  not disposed). Unbounded `ConcurrentDictionary<string,Image>` (`:16`) for process
  lifetime. Inverted guard `Clean()` `:63 if (Directory.Exists(_cachePath)) return;`
  means the on-disk cache is never cleaned. Steady GDI-handle + heap growth.

- **PERF-H2 — Plugin controls retained forever.** `XrmToolBox/New/NewForm.cs:38,1584` —
  entries are added to `pluginControlStatuses` but **never removed** (no remove
  anywhere). The main form lives for the whole session, so every tool ever opened
  stays rooted and cannot be GC'd after its tab closes.

- **PERF-H3 — Plugin event subscriptions never detached on tab close.**
  `NewForm.cs:760,789,790-806` (`+=` for message bus, `OnRequestConnection`,
  `OnWorkAsync`, `DuplicateRequested` closures capturing `NewForm`); `FormClosed`
  (`:857-866`) disposes the form but has **zero `-=`**. Combined with PERF-H2 the
  whole plugin object graph stays alive.

- **PERF-H4 — Per-paint GDI allocation never disposed (fires on every repaint).**
  `Extensibility/UserControls/LargePluginModel.cs:82-94`, `SmallPluginModel.cs:105-117`,
  `NotificationControl.cs:191-202` (leaks pen **and** brush), `New/PluginsForm2.cs:667-686`
  (+ a new `Bitmap` per draw), `Controls/SwitchControl.cs:49`, `Controls/NavLeftItem.cs:98-101`,
  `Controls/PathsControl.cs:92`, `PluginsStore/StoreFormFromPortal.cs:391`,
  `Forms/NewPluginVersion.cs:67,85`, `Forms/NewConnectionVersion.cs:50,68`. Highest-ROI
  leaks because these handlers repaint constantly.

- **PERF-H5 — Heavy synchronous IO + eager assembly loading on the UI thread at startup.**
  `Program.cs:171-274` (`InitializePluginsFolder` stream copies, `ZipFile.ExtractToDirectory`,
  per-DLL `FileVersionInfo`), `:107-154,358-412` (`Thread.Sleep(200/500)` retry loops),
  `PluginManagerExtended.cs:90-93` (`Parallel.ForEach File.ReadAllBytes` every DLL),
  `:256-276` (`ReflectionOnlyLoadFrom` every DLL), `:95-98` (full MEF compose).
  Explains slow cold start.

- **PERF-H6 — Sync-over-async on HTTP (UI-thread deadlock + thread-pool starvation).**
  `ImageCache.cs:38` (`.GetAwaiter().GetResult()` inside `Parallel.ForEach`),
  `XtbPluginVersion.cs:83,93`, `ToolPackageProperties.cs:99,102`, `ToolLibrary.cs:240,364`
  (`.Wait()`), `StoreFromPortal.cs:710,741`, `StoreFormFromPortal.cs:101`.

### Medium / Low (abridged — see citations)

- **PERF-M1 — Per-call `HttpClient`/`WebClient`** (socket exhaustion):
  `Announcement/AnnouncementManager.cs:27`, `XtbPluginVersion.cs:82`,
  `ToolPackageProperties.cs:98`, both `AppInsights.cs` (`:222`/`:240`),
  `Controls/ProxyControl.cs:73`, `WebRequestHelper.cs:14`. Reuse a single
  `HttpClient` (or `IHttpClientFactory` pattern).
- **PERF-M2 — `FileSystemWatcher` never disposed/detached** (`PluginManagerExtended.cs:79-86`).
- **PERF-M3 — Static singletons hold static events/UI refs** (`PluginManagerExtended.cs:24-39`, `NewForm.cs:156`).
- **PERF-M4/M5 — Font/`Image.FromFile` fields not disposed** (`ToolLibraryForm.Drawing.cs:35-41`, `New/PluginsForm2.cs:155-169`, `StoreFormFromPortal.cs:213,385`).
- **PERF-L — Double-alloc `new Pen(new SolidBrush(...))`, redundant `MemoryStream` write, `DefaultConnectionLimit=65000` amplifying PERF-M1.**

**Recommended:** dispose GDI objects via `using` or cache static `Pen`/`Brush`/`Font`;
remove `pluginControlStatuses` entries and `-=` events on tab close; bound + dispose
the image cache and fix the `Clean()` guard; move startup IO off the UI thread with
real async; single shared `HttpClient`.

---

## C. Code quality & best practices — maintainability perspective

- **CQ-1 — Duplicated/legacy code trees.** Parallel `XrmToolBox/Forms/` and
  `XrmToolBox/New/` with duplicated forms (two `NewVersionForm.cs`); dead copy
  `XrmToolBox.PluginsStore/StoreForm - Copier.cs(.Designer.cs)`. Confusing for humans
  and agents; raises token cost and risk of editing the wrong file.
- **CQ-2 — Committed artifacts.** `MigrationBackup/` (VS upgrade backup) is in source
  control. Should be deleted and `.gitignore`d.
- **CQ-3 — Swallowed exceptions.** ~8 files with empty `catch {}`
  (`Program.cs`, `New/NewForm.cs`, `PluginManagerExtended.cs`, `ImageCache.cs`,
  `HiddenPluginsDialog.cs`, `DropdownSettingsControl.cs`, two `InstallationInfo.cs`).
  Hides real failures; at minimum log at Verbose.
- **CQ-4 — Deprecated/dangerous APIs.** `Thread.Abort()` per keystroke
  (`New/PluginsForm2.cs:1004-1006`, `New/PluginsForm.cs:782`) — throws on modern
  runtimes, can corrupt state. Raw `Thread` where `WorkAsync`/`Task` belongs.
- **CQ-5 — No automated tests.** No test project in the solution; all verification is
  manual on Windows. High regression risk for a tool with an external plugin contract.
- **CQ-6 — Useless AI guidance file.** `.github/copilot-instructions.md` was generic
  Azure boilerplate unrelated to the project (now replaced to point at `AGENTS.md`).
- **CQ-7 — Hardcoded paths/magic numbers.** `c:\temp` (`ProxySettingsHelper.cs:21`),
  `Thread.Sleep(1000)` "wait for file" (`PluginsForm2.cs:1011-1014`), pixel-math dialog
  sizing (`ErrorDetail.cs:40,177`).

---

## D. UX & accessibility — designer perspective

### High

- **UX-1 — Raw SDK exception strings shown to end users.** `New/NewForm.cs:1434`
  (`MessageBox.Show(this, e.FailureReason, ...)`, echoed to status bar `:1439`) and
  `:964` ("An error occured…{error.Message}" — also a typo). Dataverse faults are
  multi-line developer-speak with no remediation guidance.
- **UX-2 — Search box aborts a thread on every keystroke + fixed 200 ms render delay.**
  `New/PluginsForm2.cs:1004-1006` (`Thread.Abort`), `:414` (`Thread.Sleep(200)` before
  every render), `:550` (`Items.Clear()` → flicker). Jank in the primary discovery control.
- **UX-3 — Error dialog hides the real message behind fragile string-slicing, collapsed
  by default, DPI-unaware.** `Extensibility/Forms/ErrorDetail.cs:80-134` (substring
  heuristics for the header), `:151` (details hidden until clicked), `:40,177` (raw pixel
  sizing ignores font/DPI scaling).

### Medium / Low

- **UX-4 — Synchronous HTTP on the category-nav constructor path**; silent failure hides
  the whole panel with no retry (`PluginsForm2.cs:82,161-169,202-205`).
- **UX-5 — Terminology drift "plugin" vs "tool"** across code and UI strings
  (`PluginsForm2.cs:218,235`).
- **UX-6 — Splash has no determinate progress / no Cancel**; long first runs look frozen
  (`WelcomeDialog.cs:38-44,75-80`). Connection flow is async but also offers no Cancel.
- **UX-7 — Accessibility:** owner-drawn tool cards with hardcoded colors and no contrast
  validation / high-contrast awareness (`PluginsForm2.cs:644-748`); tiny mouse-only
  PayPal/"New" hit targets, keyboard only supports Enter-to-open (`:708-744,785,799`);
  pixel-positioned cards clip under large fonts/high DPI (`:691-698`). `AutoScaleMode.Font`
  is used (good) but custom paint undoes much of its benefit.
- **UX-8 — Blunt destructive confirmations** ("close N tabs") with no "don't ask again"
  and no per-tab unsaved-work awareness (`NewForm.cs:2086`, `PluginControlBase.cs:132`).
- **UX-9 — Modal popup to confirm a clipboard copy** (`ErrorDetail.cs:148`); manual
  10px mouse-wheel scroll loop for the nav (`PluginsForm2.cs:896-929`).

**Positives:** connection + plugin-list flows are genuinely async (no hard UI block);
first-run `StartPage` with doc links and "don't show again"; destructive actions are at
least confirmed; consistent `AutoScaleMode.Font`.

---

## E. Privacy, legal & compliance perspective

- **PRIV-1 — Telemetry & GDPR/consent.** Application Insights collects a persistent
  pseudonymous `InstallationId` + tool usage. Opt-out exists
  (`OptinForApplicationInsights`, `Options.cs:623`), but confirm it is **opt-in or
  clearly disclosed at first run** for EU users, with a visible privacy notice and a
  documented retention period. The stable device GUID is personal data under GDPR.
- **PRIV-2 — Secrets at rest.** Plaintext proxy credentials (SEC-H2) and credential-bearing
  logs (SEC-M2) may violate customer security policies for an admin tool that holds
  production Dataverse access. Encrypt with DPAPI; scrub logs.
- **PRIV-3 — Licensing.** Verify `LICENSE.txt` (and bundled components: DockPanelSuite,
  ScintillaNET, WebView2, CrmSdk) are attributed and compatible; the embedded
  `Microsoft.Extensions.AI` dependency implies new data-flow/licensing questions if any
  AI feature sends org data to a model endpoint — document what, if anything, leaves the
  machine.
- **PRIV-4 — Supply-chain liability.** Shipping third-party plugins with no signature
  verification (SEC-C2/C3) is a material risk for enterprise adopters subject to SOC 2 /
  ISO 27001 / executive-order software-supply-chain requirements. A signing + allowlist
  story is increasingly a procurement gate.
- **PRIV-5 — Data residency.** Default endpoints are the xrmtoolbox.com portal + nuget.org;
  document these so customers in regulated/sovereign-cloud environments can allowlist or
  air-gap. Consider an offline/enterprise-feed mode.

---

## F. Product & business perspective

**Founder / executive lens**

- **Strength:** XrmToolBox is a category-defining, trusted tool with a huge community and
  plugin ecosystem — a genuine moat. Preserve the SDK contract above all.
- **Top risk to the brand:** a single supply-chain incident (SEC-C1/C2/C3) would damage
  trust disproportionately because the tool runs with admin credentials against production.
  Code-signing + package verification is the highest-leverage investment — it protects the
  brand and unlocks enterprise adoption simultaneously.
- **Enterprise readiness gap:** no signed-plugin allowlist, no centrally managed/air-gapped
  feed, no SSO-aligned MSAL auth (still ADAL). These are exactly the checkboxes that gate
  paid enterprise/consulting deals. Closing them is a monetization path for a free tool.
- **Platform trajectory:** .NET Framework 4.x + WinForms is mature but terminal. A long-term
  plan (even if multi-year) toward .NET 8/9 (WinForms still supported) de-risks the runtime
  and unlocks modern tooling, performance, and security defaults (TLS, crypto).

**Marketer lens**

- The `Microsoft.Extensions.AI` dependency hints at AI features — a strong differentiator
  **if** paired with a crisp privacy story ("your data never leaves your machine" or a clear
  disclosure). Lead with trust.
- "Security-hardened, signed plugins" is a messaging upgrade that turns the biggest technical
  risk into a selling point for enterprise and MVP-community credibility.
- Reduce first-run friction (UX-6 onboarding, UX-1 friendlier errors) to improve activation
  for the long tail of new Dataverse admins.

---

## G. Suggested better options / services

| Area | Current | Better option |
|------|---------|---------------|
| Auth | ADAL 5.3 (EOL) | **MSAL** (`Microsoft.Identity.Client`) + WAM broker |
| Update integrity | unverified zip | Code-sign releases; verify Authenticode before overwrite; or adopt **Squirrel/Velopack/ClickOnce** with signed packages |
| Plugin integrity | none | Signed `.nupkg` verification + trusted-author allowlist; optional AppContainer/out-of-proc isolation |
| HTTP | per-call `HttpClient`/`WebClient` | single shared `HttpClient`; `SocketsHttpHandler`; pinned `Tls12\|Tls13` |
| Secrets at rest | plaintext XML | **DPAPI** (`ProtectedData`, CurrentUser) or Windows Credential Manager |
| Telemetry | raw App Insights, stable GUID | OpenTelemetry + explicit consent + rotating/session IDs + documented retention |
| Tests | none | xUnit/NUnit for non-UI logic (ToolLibrary, StoreFromPortal, ImageCache); FlaUI for smoke |
| Runtime (long term) | .NET Framework 4.8 | .NET 8/9 (WinForms supported) for modern security/perf defaults |

---

## H. How to use this document

- Each ID (e.g. `SEC-C1`, `PERF-H2`, `UX-3`) is stable — reference it in commits/PRs
  and in agent prompts instead of re-describing the issue.
- Prioritized, sequenced remediation is in [`TODO.md`](../TODO.md).
- Before re-investigating any area, check for an existing ID here first (token saving).
