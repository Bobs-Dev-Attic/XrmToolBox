# Copilot / AI agent instructions

The canonical guide for AI agents in this repository is **[`/AGENTS.md`](../AGENTS.md)**.
Read it before making changes. Supporting docs:

- Architecture: [`/ARCHITECTURE.md`](../ARCHITECTURE.md)
- Standing review & stable issue IDs: [`/docs/REVIEW.md`](../docs/REVIEW.md)
- Prioritized work: [`/TODO.md`](../TODO.md)

## Essentials (see AGENTS.md for the rest)

- **Stack:** C# / .NET **Framework** (net48 app, net462 SDK) / **WinForms** / **MEF** plugins.
  Builds on **Windows + MSBuild/VS 2022 only** — there is no `dotnet build` and **no test project**.
- **Do not change** the `XrmToolBox.Extensibility` public interfaces or `PluginControlBase`
  signatures without an ecosystem-compatibility plan — external plugins depend on them.
- **Security-critical** code (auto-update, plugin loading, package install) is the supply-chain
  trust boundary; align any change with `TODO.md` P0 items.
- Ignore `MigrationBackup/` and `*- Copier.*` files — stale/dead code.
