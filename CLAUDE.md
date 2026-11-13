# CLAUDE.md

Orientation for Claude Code in this repo. **Read [`AGENTS.md`](AGENTS.md) first** —
it is the canonical guide (stack, build reality, repo map, conventions, high-risk
zones, token-saving shortcuts). This file adds only Claude-/session-specific notes.

## Before you start

- Project map: [`ARCHITECTURE.md`](ARCHITECTURE.md). Standing issues (with stable IDs
  like `SEC-C1`, `PERF-H2`, `UX-3`): [`docs/REVIEW.md`](docs/REVIEW.md). Prioritized
  work: [`TODO.md`](TODO.md). **Cite an existing ID instead of re-analyzing.**

## Hard rules

- **Cannot build or run here.** .NET *Framework* 4.8 + WinForms needs Windows +
  MSBuild/VS 2022. There is no `dotnet build` and **no test project**. Never report a
  build, run, or test as passing from this container — reason about the code and say
  verification must happen on Windows.
- **Git:** develop on `claude/zen-hawking-mn7oaq`. **Never push to `master`** without
  explicit user approval. Create a PR only if explicitly asked.
- **Public API:** `XrmToolBox.Extensibility` (interfaces + `PluginControlBase`) is the
  contract external plugins compile against. Additive changes only; don't rename or
  change signatures without an ecosystem plan. Don't raise the `net462` SDK TFM casually.
- **Security-sensitive files** (`AutoUpdater.cs`, `PluginManagerExtended.cs`,
  `StoreFromPortal.cs`, `ToolLibrary.cs`): coordinate edits with the `TODO.md` P0 items;
  these are the supply-chain trust boundary.

## Efficiency

- Don't read `New/NewForm.cs` or `ToolLibrary.cs` end-to-end (very large) — grep/Glob to
  the symbol. Ignore `MigrationBackup/` and `*- Copier.*` (stale/dead) when searching.
- WinForms files come in `X.cs` + `X.Designer.cs` pairs; edit the designer only for layout.
