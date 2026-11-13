# XrmToolBox — Architecture Documentation

A technical overview of how XrmToolBox is structured, how it runs, and how its
pieces fit together. For the prioritized issue list see [`TODO.md`](TODO.md);
for the detailed multi-perspective review see [`docs/REVIEW.md`](docs/REVIEW.md);
for agent working rules see [`AGENTS.md`](AGENTS.md).

---

## 1. What it is

XrmToolBox is a **Windows desktop application** (.NET Framework, WinForms) that
connects to **Microsoft Dataverse** (Dynamics 365 CE / model-driven Power Apps)
and hosts a large ecosystem of **tools ("plugins")** that ease customization,
configuration, and administration tasks. It ships with first-party tools and
loads third-party tools contributed by the community.

- ~44,000 lines of C# across 279 files, 5 core projects + in-tree plugins.
- Plugin model: **MEF** (`System.ComponentModel.Composition`).
- UI: **WinForms** + DockPanelSuite (tabbed/dockable tool tabs) + WebView2
  (embedded web content, e.g. the "what's new" page).

## 2. Solution layout

| Project | TFM | Responsibility |
|---------|-----|----------------|
| **XrmToolBox** | net48 | Main executable: process entry (`Program.cs`), main window (`New/NewForm.cs`), connection UI, options, plugin/store UI, update orchestration. |
| **XrmToolBox.Extensibility** | net462 | The **public SDK** external plugin authors reference: `PluginControlBase`, `IXrmToolBoxPlugin*` interfaces, `Paths`, `LogManager`, shared controls/dialogs. |
| **XrmToolBox.PluginsStore** | net48 | Legacy plugin store backed by NuGet + the xrmtoolbox.com portal (`StoreFromPortal.cs`). Being superseded by ToolLibrary. |
| **XrmToolBox.ToolLibrary** | net48 | Newer tool catalog + installer: fetches the tool list, images (`ImageCache`), and installs/updates packages. |
| **XrmToolBox.AutoUpdater** | net462 | Standalone updater `.exe` that closes the app, downloads a release zip, backs up, and overwrites the installation. |
| **Plugins/** | net48 | In-tree first-party tools (SampleTool, SecurityRoleViewer, DataDictionaryBuilder) — also the canonical examples of plugin conventions. |

The net462 TFM on the SDK is a **deliberate compatibility floor** so older
external plugins keep loading; don't raise it without an ecosystem plan.

## 3. Runtime data & file locations

Defined in `XrmToolBox.Extensibility/Paths.cs`. Root:
`%AppData%\MscrmTools\XrmToolBox\` (roaming user profile), overridable via
`Paths.OverrideRootPath`.

| Folder | Contents |
|--------|----------|
| `Plugins\` | Installed tool DLLs (user-writable; MEF scans this). |
| `Logs\`    | Per-plugin plaintext `*.log` files (`LogManager`). |
| `Settings\`| `XrmToolBox.Settings.xml` (app options, incl. proxy settings). |
| `Connections\` | `MscrmTools.ConnectionsList.xml` — connection definitions, serialized by the **external** `McTools.Xrm.Connection` package. |

> The core credential model (`ConnectionDetail`, `CrmConnections`) is **not in
> this repository** — it comes from the `McTools.Xrm.Connection` NuGet package.
> At-rest protection of Dataverse secrets is governed there, not here.

## 4. Startup sequence (`XrmToolBox/Program.cs` → `Main`)

1. `[STAThread] Main(args)`: assembly-resolve redirects (`RedirectAssembly`),
   global exception handlers, `ServicePointManager` tuning
   (`DefaultConnectionLimit = 65000`, Nagle off, Expect100Continue off).
2. **Plugins folder bootstrap** (synchronous, UI thread): `InitializePluginsFolder`
   migrates/copies plugin files, extracts `Plugins.zip` on first run, version-checks
   every DLL; `CopyUpdatedPlugins` / `RemovePlugins` reconcile pending updates
   (with `Thread.Sleep` retry loops).
3. `WelcomeDialog.ShowSplashScreen()` (indeterminate status text only).
4. `OptimizeConnectionSettings()`.
5. `Application.Run(new NewForm(args))` — hands off to the main window.

Recent upstream work ("boot speedup") moved some web-service and tool-library
loading off the critical path, but plugin assembly discovery is still largely
eager (see §5 and `docs/REVIEW.md` perf findings).

## 5. Plugin system (the core design)

```
Plugins\*.dll  ──►  PluginManagerExtended
                     ├─ Parallel.ForEach File.ReadAllBytes (warm OS cache)
                     ├─ DirectoryCatalog(PluginPath)  ──► MEF AggregateCatalog
                     ├─ CompositionContainer.ComposeParts  (loads all parts)
                     ├─ FileSystemWatcher(PluginPath)      (hot-add new tools)
                     └─ AppDomain.AssemblyResolve ─► Assembly.LoadFrom(path)
                                   │
ManifestLoader.CreateManifest ─────┘
   └─ Activator.CreateInstance(type) as IXrmToolBoxPlugin  (full trust, in-process)
                                   │
NewForm  ──► creates PluginControlBase UserControl, hosts it as a dock tab,
             wires OnRequestConnection / OnWorkAsync / message bus events.
```

Key interfaces (`XrmToolBox.Extensibility/Interfaces/`): `IXrmToolBoxPlugin`,
`IXrmToolBoxPluginControl`, plus capability markers (`INoConnectionRequired`,
`IGitHubPlugin`, `IPayPalPlugin`, `IHelpPlugin`, `IMessageBusHost`,
`IDuplicatableTool`, `ISettingsPlugin`, …). A tool is a MEF export of
`IXrmToolBoxPlugin` whose `GetControl()` returns a `PluginControlBase`.

**Trust model:** any DLL present in the user-writable `Plugins\` folder is
composed and instantiated with the full privileges of the XrmToolBox process —
which holds live Dataverse admin connections. There is no AppDomain isolation,
code-access-security, strong-name, or Authenticode gate. This is intentional for
extensibility but is the central security consideration (see `docs/REVIEW.md` §Security).

## 6. External services & network calls

| Purpose | Endpoint | Transport |
|---------|----------|-----------|
| Tool catalog (store) | `https://www.xrmtoolbox.com/_api/mctools_plugins`, `_api/mctools_categories` | HTTPS |
| NuGet packages | `https://api.nuget.org/v3/index.json` (or a plugin's own `DownloadUrl`) | HTTPS (also accepts `http`/`file` for custom repos) |
| Release/update check | `https://www.xrmtoolbox.com/_api/mctools_releases` | HTTPS |
| Update package | release JSON `DownloadUrl` (host-unconstrained) | follows URL |
| "What's new" page | `https://www.xrmtoolbox.com/new-version/?version=…` (WebView2) | HTTPS |
| Telemetry | Application Insights `https://dc.services.visualstudio.com/v2/track` | HTTPS |
| Images/logos | tool metadata URLs (`ImageCache`) | HTTP(S) |

Telemetry sends a pseudonymous persistent `InstallationId` GUID + tool
name/version/usage; opt-out exists (`OptinForApplicationInsights`). No
credentials/org URLs are placed in telemetry payloads (verified).

## 7. Auto-update flow

`NewForm` polls `_api/mctools_releases` → if newer, shows `NewVersionForm`
(renders the web "what's new" page) → on accept, copies `XrmToolBox.AutoUpdater.exe`
to `%ProgramData%\XrmToolBox\` and launches it with the release `DownloadUrl` as
an argument → the updater kills running instances, downloads the zip, backs up
the current install, and copies the new files over it, then relaunches.
**No hash/signature/Authenticode verification occurs** between download and
overwrite (see `docs/REVIEW.md` finding SEC-C1).

## 8. Key dependencies (selected)

- `Microsoft.CrmSdk.XrmTooling.CoreAssembly` 9.1.x, `Microsoft.CrmSdk.CoreAssemblies` 9.0.x — Dataverse SDK.
- `MscrmTools.Xrm.Connection` 1.2025.x — connection management (external credential model).
- `Microsoft.IdentityModel.Clients.ActiveDirectory` 5.3.0 — **ADAL, end-of-life**; should migrate to MSAL.
- `DockPanelSuite` 3.0.6 — docking UI. `Microsoft.Web.WebView2` — embedded browser.
- `Newtonsoft.Json` 13.0.3, `System.Text.Json` 10.x, `Microsoft.Extensions.AI` 9.7.x (AI features).

## 9. Known structural debt (summary)

- Parallel `Forms/` (legacy) and `New/` (current) trees with duplicated forms.
- `XrmToolBox.PluginsStore/StoreForm - Copier.*` — dead copy.
- `MigrationBackup/` committed to source control.
- `.github/copilot-instructions.md` was generic Azure boilerplate (replaced).
- No test project; Windows-only manual verification.

See [`TODO.md`](TODO.md) for the prioritized remediation plan.
