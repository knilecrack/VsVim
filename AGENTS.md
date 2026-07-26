# AGENTS

Guidance for agentic coding assistants working in the VsVim repository.

## Project Overview

VsVim is a free Vim emulator for Visual Studio 2019, 2022 and 2026, licensed under
Apache 2.0 (see `License.txt`). It implements a high-fidelity Vim engine (motions,
modes, Ex commands, registers, macros, etc.) on top of the Visual Studio WPF editor.

- Languages: F# (the vim core engine), C# (Visual Studio integration, WPF, tests),
  a little PowerShell for build/test automation.
- There is also `Src/VimMac/`, a Visual Studio for Mac extension (net7.0) built only
  on macOS CI.

## Repository Layout

- Root solution: `VsVim.sln` — contains all projects including the VS2019/2022/2026
  flavors.
- `Src/VimCore/` — F# core vim engine. Editor-agnostic: modes (Normal, Visual,
  Insert, Command, SubstituteConfirm...), motions, Ex command interpreter
  (`Interpreter_*.fs`), key notation, registers, marks, undo. Version number lives
  in `Src/VimCore/Constants.fs`.
- `Src/VsVimShared/` — C# integration with the Visual Studio editor APIs shared by
  all VS versions (command routing, `VsVimHost`, `VsVimPackage`, key bindings).
- `Src/VimWpf/` — WPF rendering pieces (block caret, command margin). A shared
  project (`.shproj`/`.projitems`), compiled into consumers rather than shipped
  alone.
- `Src/VimEditorHost/` — shared project for hosting `IVimBuffer` in tests and in
  VimApp; defines `EditorVersion` (Vs2019 / Vs2022 / Vs2026).
- `Src/VimApp/` — lightweight WPF host of the VS editor; starts fast, good for
  manually testing vim behavior without launching full Visual Studio.
- `Src/VimTestUtils/` — shared test utilities (`WpfFactAttribute`, mock factories).
- `Src/VsVim2019/`, `Src/VsVim2022/`, `Src/VsVim2026/` — thin per-VS-version VSIX
  projects (each has its own `source.extension.vsixmanifest`).
- `Src/CleanVsix/` — small utility (`CleanVsix.exe`) run during packaging to strip
  unwanted files out of the produced VSIX.
- `Test/VimCoreTest/` — shared test project (`.shproj`) whose sources are compiled
  into `Test/VimCoreTest2019/`, `Test/VimCoreTest2022/`, `Test/VimCoreTest2026/`.
  Add new core tests here, not in the per-version projects.
- `Test/VsVimSharedTest/`, `Test/VsVimTest2019/2022/2026/`, `Test/VimWpfTest/` —
  the remaining xUnit suites.
- `References/Common|Vs2019|Vs2022|Vs2026/` — VS editor reference assemblies.
  `Directory.Build.props` restricts `AssemblySearchPaths` to these (no GAC), so
  builds are machine-independent.
- `Scripts/` — `Build.ps1` (main build/test/pack script), `build.sh` (macOS VimMac
  build), `target.cmd`, `Test-ProjectFiles.ps1`. `Tools/` holds `vswhere.exe` and
  7-Zip used by the scripts.

## Technology Stack

- Target framework: .NET Framework 4.7.2 for the VSIX and all Windows test
  assemblies (VimMac targets net7.0).
- Tests: xUnit 2.4.1, executed with `xunit.console.x86.exe` from the NuGet package
  cache.
- Editor integration via MEF and the VS SDK (`Microsoft.VSSDK.BuildTools`).
- Primary IDE: Visual Studio 2022 with the .NET Desktop Development, F# Language
  and Visual Studio Extension Development workloads.

## Multi-Version Targeting

Each VS version is a separate project pair (`VsVim2022` + `VsVimTest2022`, etc.)
that compiles the shared sources with a per-project MSBuild property
`VsVimVisualStudioTargetVersion` (`16.0` = VS2019, `17.0` = VS2022, `18.0` =
VS2026), set in each `.csproj`. `Directory.Build.targets` imports the matching
`References/Vs20xx/Vs20xx.Build.targets`. Version-specific behavior is handled
with `#if` directives. The legacy `Scripts/target.cmd` writes a
`VsVimTargetVersion` property into `Binaries/User.props` (imported by
`Directory.Build.props`); `Documentation/Developing.md` describes this but is
partly stale (it predates VS2022/2026 and GitHub Actions).

## Build and Test Commands

All builds go through `Scripts/Build.ps1`; `Build.cmd` / `Test.cmd` are thin
wrappers.

- Build (Debug): `Build.cmd` or
  `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -build"`
- Release build: `Build.cmd -config Release`
- CI build (no VSIX deploy step): `Scripts/Build.ps1 -build -ci`
- Direct MSBuild: `msbuild /nologo /restore /v:m /m /p:Configuration=Debug VsVim.sln`
  (MSBuild path is located via `Tools/vswhere.exe`; do not hardcode VS paths.)
- Full test run: `Scripts/Build.ps1 -test` — runs, for each of 2019/2022/2026,
  `Vim.Core.<ver>.UnitTest.dll` and `Vim.VisualStudio.Shared.<ver>.UnitTest.dll`
  from `Binaries/<Config>/.../net472/` with the xUnit console runner from the
  NuGet cache, writing XML results to `Binaries/xunitResults/`.
- Extra verification: `Scripts/Build.ps1 -testExtra` — runs `Test-VsixContents`
  (unpacks each VSIX with 7-Zip and checks the exact expected file set) and
  `Test-Version` (version in `Src/VimCore/Constants.fs` must equal every
  `source.extension.vsixmanifest` version, and `VsVimPackage.cs` must reference
  `VimConstants.VersionNumber`).
- Build + tests: `Build.cmd -test`

### Running a single test

The xUnit console runner is not copied to the output directory; use the one from
the NuGet cache:

```
%UserProfile%\.nuget\packages\xunit.runner.console\2.4.1\tools\net472\xunit.console.x86.exe ^
  Binaries\Debug\VimCoreTest2022\net472\Vim.Core.2022.UnitTest.dll ^
  -method Namespace.ClassName.TestMethod
```

Use `-class Namespace.ClassName` for a whole class. Test assemblies are named
`Vim.Core.<2019|2022|2026>.UnitTest.dll` and
`Vim.VisualStudio.Shared.<2019|2022|2026>.UnitTest.dll`.

### Manual smoke testing

Open the solution, set `VimApp` as startup project and F5 — this hosts the real
vim engine in a lightweight WPF shell and is much faster than launching an
experimental VS instance.

## CI / Deployment

CI is GitHub Actions (`.github/workflows/main.yml`), on `windows-2022`:

1. `Scripts\Build.ps1 -ci -config Debug -build`
2. `Scripts\Build.ps1 -ci -config Debug -test`
3. `Scripts\Build.ps1 -ci -config Debug -testExtra`
4. A Release publish job builds with `-updateVsixVersion` (stamps
   `GITHUB_RUN_NUMBER` into the VSIX manifests) and uploads the VSIX artifacts.
5. A macOS job builds `Src/VimMac/` via `Scripts/build.sh` and publishes the
   `.mpack`.

Built VSIX files are cleaned by `CleanVsix.exe` and land in
`Binaries/Deploy/<Config>/<VsVersion>/VsVim.vsix`. Do not bypass `CleanVsix` in
new scripts. Publishing to the Open VSIX Gallery is done by
`Scripts/Build.ps1 -uploadVsix` (CI only; requires `GITHUB_RUN_NUMBER`).

## Code Style

Authoritative docs: `Documentation/CodingGuidelines.md` plus this file. C#
follows the dotnet/corefx coding style.

- C#: braces on new lines for types/members, spaces after keywords, one statement
  per line, `using System.*` first then sorted groups, no unused usings, no
  trailing whitespace.
- F#: prefix private fields with `_`; add spaces around operators/comparisons,
  in record initializers, before explicit type annotations (`(text: string)`),
  and between keywords and open parens (`if (`, `with get (`); do not use `;`
  in multi-line object initializers.
- Keep `open` lists minimal and localized; no wildcard usings.
- ASCII-only in source unless the file already contains non-ASCII; UTF-8 without
  BOM.
- Match the surrounding file's existing patterns; keep changes minimal and
  scoped — no drive-by refactors.

## Naming / Terminology Conventions

- **Last** is inclusive; **End** is exclusive.
- **Column** = Vim column (tabs count as 1), **Position** = editor
  `SnapshotPoint`, **Spaces** = Vim visual width (tabstop, double-wide chars).
- Util classes expose `Create` helper methods.
- APIs taking a count must return an option or guard against oversized user
  input (users control counts). APIs taking a line number should consider
  returning an option.
- Prefer options over nulls; keep discriminated-union/enum matches exhaustive
  and handle `default` cases consciously.

## Testing Conventions

- xUnit; fixtures small and deterministic. WPF-dependent tests use
  `WpfFactAttribute` from `VimTestUtils`.
- Core engine tests go in the shared `Test/VimCoreTest/` project so they run
  against all three VS editor versions.
- Behavior can differ subtly between editor versions; when unsure, check the
  2019, 2022 and 2026 test flavors rather than assuming.
- Integration-style tests (`*IntegrationTest.cs`) drive a real `IVimBuffer`
  through `VimEditorHost`.

## Performance / Threading Notes

- The core engine is synchronous by design; avoid LINQ and extra allocations in
  hot paths (motion capture, command execution) — mirror the imperative style of
  nearby code.
- VS integration code often runs on the UI thread; follow existing marshaling
  patterns in `VsVimShared`.

## Housekeeping

- Do not commit anything under `Binaries/` (build outputs, VSIX artifacts,
  `xunitResults`, `Logs`).
- NuGet restore happens via `msbuild /restore` / `Build.cmd`; cache is
  `NUGET_PACKAGES` or `%UserProfile%\.nuget\packages`.
- When bumping the version, update `Src/VimCore/Constants.fs` and all three
  `Src/VsVim20*/source.extension.vsixmanifest` files together — `Test-Version`
  enforces this.
- Auxiliary root docs `TEXT_OBJECTS_README.md`, `TEXT_OBJECTS_FEATURE_STATUS.md`
  and `TEXT_OBJECTS_QUICK_REFERENCE.md` describe recently added text-object
  motion support (`vi(`, `va{`, ...); `test_vi_command.cs` is a scratch buffer
  for manually exercising those motions. Treat them as working notes, not
  authoritative docs. New permanent documentation belongs under `Documentation/`.

## Quick Command Reference

- Build Debug: `Build.cmd`
- Build Release: `Build.cmd -config Release`
- Build + Test: `Build.cmd -test`
- Tests only: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -test"`
- Extra verification: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -testExtra"`
- Single test: see "Running a single test" above.

## Good Agent Habits

- Before committing: run at least the targeted tests for the touched area; for
  packaging/manifest changes also run `-testExtra`.
- Respect existing uncommitted user changes; never run destructive git commands
  (`reset --hard`, force push) unless explicitly asked.
- Verify paths and VS target versions before invoking scripts to avoid long
  rebuilds.
