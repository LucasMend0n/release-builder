# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ReleaseBuilder is a .NET 10 console app that automates `git checkout` + `dotnet rebuild` across an ordered list of local repositories for a given release branch (`release/{version}`). It ships as a Windows-only self-contained single-file `.exe` — end users do not need .NET installed. There is no test suite (yet); `Program.Main` is the whole entry point.

## Commands

```bash
# Debug build
dotnet build

# Release single-file self-contained exe (the same command CD runs)
dotnet publish release-builder.csproj -c Release -r win-x64

# Lint (the same command CI runs — must pass before merge)
dotnet format release-builder.csproj --verify-no-changes

# Run from source (uses %APPDATA% config by default)
dotnet run -- -v 1.5.0
dotnet run -- --config-path
dotnet run -- -v 1.5.0 -c path/to/alt.json
```

Exit codes: `0` success, `1` runtime/build failure, `2` config missing (template was just created — user should edit and re-run).

## Architecture

Orchestration lives in `Program.cs`. For each `RepositoryEntry` in config order it runs, sequentially:

1. `GitService.CheckForDirtyWorkingTreeAsync` — `git status --porcelain`; if dirty, `StashWorkTree` is called to push a stash. Note: the dirty-tree path returns `Success=false` with a message — that's the trigger to stash, not an error (`GitService.cs:41`).
2. `FetchAsync` → `CheckoutBranchAsync` → `PullAsync`. Checkout tries the local branch first, falls back to `checkout -b release/{v} origin/release/{v}`.
3. `BuildService.RebuildAsync` — `dotnet restore` → `dotnet clean` → `dotnet build --no-incremental --no-restore` against the configured `.sln` (or `.csproj`).

Each step short-circuits the repo's loop on failure. If `StopOnError` is true the whole run aborts; otherwise the next repo starts and failures aggregate in the final report.

`GitService` and `BuildService` both shell out via `Process.Start` and return `(bool Success, string Output)` tuples. No logging framework — `ConsoleLogger` (`Services/Logger.cs`) is a static class writing colored `[INFO]/[OK]/[WARN]/[FAIL]` lines to `Console`.

### Config loading

`Program.LoadConfig` (`Program.cs:222`) resolution order:
1. Path passed via `-c` / `--config <path>` (explicit → missing file is a hard error).
2. `%APPDATA%\release-builder\appsettings.json` (default → on first run, creates the directory + writes a usable template + exits with code 2 and a "edit and re-run" message).

`Program.cs:185` (`GetDefaultConfigPath`) computes the default via `Environment.SpecialFolder.ApplicationData` (Windows-only project, but the API call is what it is). The template content lives inline in `CreateTemplateConfig` and matches `examples/appsettings.template.json` — keep them in sync if you change one.

`BuildConfig` shape: `rootPath`, `stopOnError`, `repositories[]` with `name` (folder relative to `rootPath`, may contain subpath segments like `LogBackoffice.Web\src\services\Service.LogBackoffice.Api`) and `solutionFile` (`.sln` or `.csproj`). Order matters — dependencies before consumers.

### Namespaces — watch out

The folder is `Model/` (singular) but holds two namespaces:
- `Model/BuildResult.cs` → `release_builder.Model`
- `Model/BuildConfig.cs` → `release_builder.Models` (plural)

Both are used and `Program.cs` `using`s both. Don't "fix" without auditing call sites.

## Distribution

End-user install flow (target audience): GitHub Release → download `release-builder-vX.Y.Z-win-x64.zip` → extract → run `install.bat`. The bat copies the exe to `%LOCALAPPDATA%\Programs\release-builder\`, adds it to user PATH via a PowerShell one-liner (not `setx` — `setx` truncates PATH at 1024 chars), and seeds `%APPDATA%\release-builder\appsettings.json` from the template if missing.

`installer/install.bat` and `installer/uninstall.bat` are bundled into the release ZIP by the CD workflow alongside `release-builder.exe` and the `examples/` folder.

## CI / CD

- `.github/workflows/ci.yml` — runs on `pull_request` to `main`. Lint (`dotnet format --verify-no-changes`) + Release build with `TreatWarningsAsErrors=true` (in csproj). Branch protection on `main` should require this before merge.
- `.github/workflows/release.yml` — runs on `push` of tag `v*`. Publishes single-file exe with `-p:Version=<tag-without-v>`, stages the ZIP (exe + install/uninstall bats + examples), creates a GitHub Release with auto-generated notes.

Cutting a release is `git tag v1.2.3 && git push --tags` — CD does the rest. No manual `gh release create`.

## Style conventions

- File-scoped namespaces (`namespace X;`) everywhere except `Model/BuildResult.cs` (block-scoped — legacy, fine to leave alone unless touching the file).
- `TreatWarningsAsErrors=true` in `.csproj` — any warning fails CI. Don't suppress with pragmas; fix the warning.
- The `examples/` folder holds reference configs (`appsettings.consiglog.json`, `appsettings.development.json`, `appsettings.template.json`). They are not loaded by the app — they exist for users to copy/inspect.
