# AGENTS

Guidance for agentic coding assistants working in the VsVim repository.

## Scope
- Audience: code agents (Claude Code, ChatGPT, etc.) collaborating on VsVim.
- Goals: build/run quickly, follow house styles, avoid breaking the VSIX build.
- Repo languages: F# (vim core), C# (VS integration, WPF), a little PowerShell for build/test.

## Repository Landmarks
- Root solution: `VsVim.sln` (includes all VS2019/VS2022 projects).
- Additional solution: `Src/CleanVsix/CleanVsix.sln` (utility used during packaging).
- Scripts: `Scripts/Build.ps1`, `Build.cmd` wrappers for build/test/pack/verify.
- App host: `Src/VimApp/` lightweight WPF editor host for quick manual checks.
- Tests: `Test/VimCoreTest2019/`, `Test/VimCoreTest2022/`, `Test/VsVimTest2019/`, `Test/VsVimTest2022/` (all xUnit, .NET Framework 4.7.2 output under `Binaries/<Config>/...`).
- Shared integration: `Src/VsVimShared/`, core engine: `Src/VimCore/`, WPF bits: `Src/VimWpf/`.
- No Cursor rules present (`.cursor/`, `.cursorrules` not found); no Copilot instructions file found (`.github/copilot-instructions.md` absent).

## Environments & Prereqs
- Primary IDE: Visual Studio 2022.
- Required workloads: .NET Desktop Development, F# Language, Visual Studio Extension Development.
- Target frameworks: tests run on .NET Framework 4.7.2 outputs.
- Supported VS editor targets: 2019, 2022; build scripts also list 2026 for future.
- Environment variable `VsVimTargetVersion` (14.0/15.0/16.0) influences which VS editor binaries tests bind to.

## Build Commands
- Default build (Debug): `Build.cmd` (wraps PowerShell script).
- Release build: `Build.cmd -config Release`.
- CI-style build without deploying VSIX: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -build -ci"`.
- Build solution directly with MSBuild (after restore): `msbuild /nologo /restore /v:m /m /p:Configuration=Debug VsVim.sln`.
- Clean VSIX artifacts are produced into `Binaries/Deploy/<Config>/<VsVersion>/` during build.

## Test Commands (Batch)
- Full test run: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -test"` (runs all VS2019/VS2022 xUnit suites).
- Full build + tests: `Build.cmd -test` or `powershell ... Scripts/Build.ps1 -build -test`.
- Extra verification (VSIX contents + version consistency): `powershell ... Scripts/Build.ps1 -testExtra` (runs `Test-VsixContents` + `Test-Version`).

## Test Commands (Single Assembly / Single Test)
- After a build, assemblies land under `Binaries/<Config>/VimCoreTest2019/net472/...` etc.
- Run one assembly (example 2022 core):
  - `Binaries/Debug/VimCoreTest2022/net472/xunit.console.x86.exe Binaries/Debug/VimCoreTest2022/net472/Vim.Core.2022.UnitTest.dll`
- Run one test method via xUnit console `-method` switch (adjust paths as needed):
  - `Binaries/Debug/VimCoreTest2022/net472/xunit.console.x86.exe Binaries/Debug/VimCoreTest2022/net472/Vim.Core.2022.UnitTest.dll -method Namespace.ClassName.TestMethod`
- Run one test class: use `-class Namespace.ClassName`.
- If `VsVimTargetVersion` must change, set it before invoking the runner.

## Quick Local Smoke (Manual)
- Open `Src/VimApp/` in VS, set startup project to VimApp, F5 to sanity-check key behavior without full VS.
- Packaging sanity: ensure `Binaries/Deploy/<Config>/<VsVersion>/VsVim.vsix` exists after build; `Test-VsixContents` verifies expected files.

## Source Control Hygiene
- Respect existing user changes; do not revert unrelated edits.
- No destructive git commands (`reset --hard`, force push) unless explicitly requested.
- Default branch style: follow recent commit messages visible via `git log --oneline -n 10` when crafting commits.

## Coding Style (General)
- Default to DotNet coding style for C#: https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md
- F# style: see below; follow project conventions, not generic F# style.
- Prefer clarity over cleverness; mirror existing patterns in the same folder.
- Keep ASCII-only unless existing file already contains non-ASCII.

## Imports / Usings
- C#: place `using System;`-style namespaces first, then others, sorted and grouped per DotNet guidelines; avoid unused usings.
- F#: keep `open` lists minimal and localized; avoid blanket opens that widen scope unnecessarily.
- Do not introduce wildcards; keep namespaces specific to needed APIs.

## Formatting
- C#: brace and spacing per DotNet style (K&R-ish, spaces after keywords, braces on new lines for types/members).
- C#: one statement per line; avoid trailing whitespace; prefer expression-bodied members only when it improves clarity.
- F#: add spaces between values and operators, between names and values in record initializers, between names and explicit types, and between keywords and opening parens (`if (`, `with get (`).
- F#: do not use semicolons for multi-line object initializers.
- Align multiline argument lists and pipeline steps for readability; keep indentation consistent with surrounding code.

## Naming & Terminology
- Follow existing domain terms: **Last** is inclusive; **End** is exclusive.
- Columns/positions: **Column** = Vim column (tab=1), **Position** = editor `SnapshotPoint`, **Spaces** = Vim visual width (tabstop, wide chars count 2).
- Util classes typically expose `Create` helpers.
- APIs taking counts should guard against oversized user input (return option or validate); APIs taking line numbers should consider returning option when out-of-range.

## Types & Nullability
- Prefer explicit types where they clarify intent (especially public APIs); rely on `var`/F# inference in obvious local contexts only.
- Avoid nulls in new code; prefer options (`Option` in F#, nullable references or `Optional` patterns in C#) and clear defaulting.
- Keep discriminated unions and enums exhaustive; handle `default` cases consciously to avoid silent behavior changes.

## Error Handling & Logging
- Fail fast in scripts when environment variables (e.g., `GITHUB_RUN_NUMBER`) are missing and required.
- In C#: throw argument exceptions for invalid inputs; return clear error results when part of command-processing flow; avoid swallowing exceptions.
- In F#: propagate errors via result/option where part of control flow; avoid exceptions for expected states.
- Build scripts: preserve `Set-StrictMode -version 2.0` and `$ErrorActionPreference="Stop"`; surface failures with clear messages (see `Write-TaskError`).

## Testing Patterns
- Tests are xUnit; keep fixtures small and deterministic.
- Target-specific behavior may differ between VS2019 and VS2022; parameterize where needed rather than duplicating logic.
- When adding tests, ensure they work with `VsVimTargetVersion` variations (14.0/15.0/16.0) if they touch editor APIs.

## Performance & Allocation
- Be mindful of allocations in hot paths (motion capture, command execution); reuse spans/buffers where existing code does so.
- Avoid LINQ in tight loops inside the core engine; prefer imperative loops as seen in nearby code.

## Threading & Async
- Visual Studio integration often runs on UI thread; follow existing marshaling patterns in `VsVimShared` and avoid deadlocks.
- Use async/await only when the surrounding layer already uses it; many core operations are synchronous by design.

## UI / WPF Notes
- Preserve existing WPF styling in `VimWpf`; avoid introducing new resources without checking shared dictionaries.
- Keep command margin and caret rendering behavior consistent across VS versions; test in VimApp when possible.

## VSIX Packaging
- Packaging uses `CleanVsix.exe` to strip unwanted files; do not bypass this in new scripts.
- If touching manifests (`source.extension.vsixmanifest`), ensure version consistency across `Src/VsVim*/` and `Src/VimCore/Constants.fs`; `Test-Version` enforces this.

## Build Script Conventions
- `Scripts/Build.ps1` supports switches: `-build`, `-test`, `-testExtra`, `-updateVsixVersion`, `-uploadVsix`, `-config`, `-ci`.
- Keep new script parameters aligned with existing pattern (Switch parameters for actions; string for config).
- MSBuild path resolved via `vswhere.exe`; avoid hardcoding VS install paths.

## Dependencies & Restore
- NuGet cache resolved via `NUGET_PACKAGES` or `%UserProfile%\.nuget\packages`; do not commit restored packages.
- Package restore occurs during `msbuild /restore`; `Build.cmd` handles it automatically.

## File / Repo Conventions
- Ignore patterns in `.gitignore` should be respected; do not commit binaries (`Binaries/`, `Deploy/`, `Debug/`, `Release/`).
- Keep ASCII-only when editing text; repository primarily uses UTF-8 without BOM.

## What Does Not Exist
- No repository-wide `.editorconfig` present; follow documented guidelines instead.
- No Cursor rules or Copilot instruction files to inherit; this document plus `CLAUDE.md` and `Documentation/CodingGuidelines.md` are authoritative.

## When Adding Docs
- Keep new docs concise and place them under `Documentation/` unless otherwise requested.
- Mirror this file’s tone for agent-facing instructions.

## Good Agent Habits
- Before commits: run at least targeted tests relevant to touched area; for packaging changes run `-testExtra`.
- Summarize changes with rationale; avoid long diffs without explanation.
- Never force-push unless user explicitly directs.
- When unsure about VS version-specific behavior, check both 2019 and 2022 test projects.

## Quick Command Reference
- Build Debug: `Build.cmd`
- Build Release: `Build.cmd -config Release`
- Build + Test: `Build.cmd -test`
- Tests only: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -test"`
- Extra verification: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -testExtra"`
- Single test: `Binaries/Debug/VimCoreTest2022/net472/xunit.console.x86.exe Binaries/Debug/VimCoreTest2022/net472/Vim.Core.2022.UnitTest.dll -method Namespace.Class.Test`

## Final Reminders
- Match surrounding style in the file you edit (spacing, naming, patterns).
- Keep changes minimal and scoped; avoid drive-by refactors unless asked.
- Document non-obvious logic with brief comments only when necessary.
- Verify paths and VS target versions before invoking scripts to avoid long rebuilds.
