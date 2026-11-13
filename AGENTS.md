# AGENTS.md — Working guide for AI agents (Codex, Claude Code, Copilot)

> Purpose: let an agent act correctly in this repo **without re-discovering the layout every time**.
> Read this first. It is the canonical orientation file; `CLAUDE.md` and
> `.github/copilot-instructions.md` point here. Keep it short and current.

## What this is

XrmToolBox — a **Windows desktop (.NET Framework, WinForms)** admin tool for
Microsoft Dataverse / Dynamics 365 / model-driven Power Apps. It hosts
third-party "tools" (plugins) as tabs via **MEF**. See `ARCHITECTURE.md` for the
full map and `docs/REVIEW.md` for the standing issue list.

- Languages/stack: C# (~44k LOC, 279 `.cs`), WinForms, MEF
  (`System.ComponentModel.Composition`), `Microsoft.CrmSdk.*`, WebView2.
- Target frameworks: **`net48`** (main app, ToolLibrary, PluginsStore),
  **`net462`** (Extensibility SDK + AutoUpdater). Do not change TFMs casually —
  the SDK TFM is a compatibility contract for external plugin authors.

## Build / run / test — read before trying

- **This repo does NOT build on Linux/macOS or in this cloud container.** It is
  .NET *Framework* + WinForms and needs **Windows + MSBuild/Visual Studio 2022**
  (or `msbuild XrmToolBox.sln /restore`). There is no `dotnet build` path.
- Packages are **`packages.config` / NuGet restore** (classic), not PackageReference.
- **There is no automated test project** in the solution. Do not claim tests
  pass — there are none to run. Verification is manual on Windows.
- CI historically runs on **AppVeyor** (badge in `README.md`); no workflow file
  is committed to the repo, so you cannot inspect or run it here.
- Agents on non-Windows hosts: you can **read, edit, and reason**, but you
  **cannot compile or run**. Say so; never report a build/run as done.

## Repository map (where things live)

| Path | What it is | Touch with care |
|------|-----------|-----------------|
| `XrmToolBox/` | Main app. Entry `Program.cs`; main window `New/NewForm.cs`; store UI `New/PluginsForm2.cs`; options `Options.cs`, `Forms/OptionsDialog.cs`. | `New/` is the live code. `Forms/` has **duplicates/legacy** (e.g. two `NewVersionForm.cs`). |
| `XrmToolBox.Extensibility/` | **Public plugin SDK** external authors reference. `PluginControlBase`, `Interfaces/I*.cs`, `Paths.cs`, `LogManager.cs`. | Breaking changes break the ecosystem. Treat as public API. |
| `XrmToolBox.PluginsStore/` | Legacy NuGet-based store. `StoreFromPortal.cs`. Contains dead copy `StoreForm - Copier.cs`. | Being superseded by ToolLibrary. |
| `XrmToolBox.ToolLibrary/` | Newer tool catalog/installer. `ToolLibrary.cs`, `AppCode/ImageCache.cs`, `AppCode/WebRequestHelper.cs`. | Active. |
| `XrmToolBox.AutoUpdater/` | Standalone self-update `.exe` (`AutoUpdater.cs`). | Security-critical (see below). |
| `Plugins/` | In-tree sample/first-party plugins (SampleTool, SecurityRoleViewer, DataDictionaryBuilder). | Good reference for plugin conventions. |
| `docs/` | Design docs (`superpowers/`), this review (`REVIEW.md`). | — |
| `MigrationBackup/` | **Stale build-migration artifacts — should not be in the repo.** | Do not read for signal; candidate for deletion (see TODO). |

Runtime data lives under `%AppData%\MscrmTools\XrmToolBox\` → `Plugins\`,
`Logs\`, `Settings\`, `Connections\` (see `XrmToolBox.Extensibility/Paths.cs`).

## Conventions

- WinForms: each form/control is `X.cs` + `X.Designer.cs`. Edit designer files
  only when changing the visual layout; prefer hand-written partial code in `X.cs`.
- Plugins are discovered by MEF `[Export(typeof(IXrmToolBoxPlugin))]` +
  metadata attributes (see `Plugins/SecurityRoleViewer/Plugin.cs`).
- Background work goes through `WorkAsync`/`BackgroundWorker`, not raw threads.
  (Existing raw-thread/`Thread.Abort` usage in `PluginsForm2.cs` is a known bug,
  not a pattern to copy.)
- Attribution/commit rules for this session: branch `claude/zen-hawking-mn7oaq`;
  never push to `master` without explicit user approval.

## High-risk zones — do not "clean up" blindly

1. **Plugin loading & trust** (`PluginManagerExtended.cs`, `Manifest/ManifestLoader.cs`):
   MEF loads every DLL from a user-writable folder, full-trust, in-process. The
   whole security model lives here — changes have supply-chain impact.
2. **Auto-update** (`AutoUpdater.cs`, `New/NewVersionForm.cs`): downloads a zip
   and overwrites the running app. No integrity check today. Any edit here is
   security-sensitive; coordinate with the TODO P0 items.
3. **Extensibility public surface** (`Interfaces/`, `PluginControlBase`): changing
   signatures breaks hundreds of external plugins. Additive only.
4. **Connection/credential flow**: the real credential model is the **external**
   `McTools.Xrm.Connection` NuGet package, not in this repo. Do not assume you
   can see or change how Dataverse secrets are encrypted.

## Token-saving shortcuts (avoid re-exploration)

- "Where is the main window?" → `XrmToolBox/New/NewForm.cs` (large; grep, don't read whole).
- "Where is the store/tool list UI?" → `XrmToolBox/New/PluginsForm2.cs`.
- "Where are secrets/paths stored?" → `XrmToolBox.Extensibility/Paths.cs`.
- "Plugin SDK contract?" → `XrmToolBox.Extensibility/Interfaces/`.
- Prefer `grep`/`Glob` over reading `NewForm.cs` (2k+ lines) or `ToolLibrary.cs` end to end.
- Ignore `MigrationBackup/` and `*- Copier.*` files when searching for current behavior.
- Known issues are catalogued in `docs/REVIEW.md` and `TODO.md` — check there
  before re-analyzing memory/security/UX; cite the existing finding ID instead.
