# VsVim Project Overview

VsVim is a free Vim emulator for Visual Studio 2019 through 2026. It provides a high-fidelity Vim experience within the Visual Studio editor.

## Architecture

The project is split into several key layers:

- **VimCore (`Src/VimCore/`)**: Written in **F#**, this is the core vim engine. it is entirely editor-agnostic and contains the logic for motions, commands, modes (Normal, Visual, Insert), and Ex commands.
- **VsVimShared (`Src/VsVimShared/`)**: Written in **C#**, this contains the shared logic for integrating with Visual Studio's editor APIs.
- **VimWpf (`Src/VimWpf/`)**: WPF-specific rendering logic for elements like the command margin and block caret.
- **VimApp (`Src/VimApp/`)**: A lightweight WPF host for the editor, allowing for rapid testing of core Vim features without launching a full instance of Visual Studio.
- **VimEditorHost (`Src/VimEditorHost/`)**: Shared source for hosting `IVimBuffer` in tests and `VimApp`.

## Building and Running

The project uses PowerShell scripts for build and test automation, with batch file wrappers in the root.

### Common Commands

- **Build the solution**: `Build.cmd`
- **Build with specific configuration**: `Build.cmd -config Release`
- **Run unit tests**: `Test.cmd`
- **Run extra verification (VSIX contents, version consistency)**: `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts\Build.ps1 -testExtra"`

### Multi-Version Support

VsVim targets multiple versions of Visual Studio (2019, 2022, 2026). You can configure the target version for testing:
- Set the environment variable `%VsVimTargetVersion%` to `14.0`, `15.0`, or `16.0`.
- Use the `-testConfig` flag with `Build.cmd`.

The project manages Visual Studio SDK dependencies via the `References/` directory to ensure build reliability across different environments.

## Development Conventions

### General Terminology
- **Last**: Inclusive range.
- **End**: Exclusive range.
- **Position**: Editor's `SnapshotPoint` (may be mid-line feed).
- **Column**: Vim unit (tab = 1 column).
- **Spaces**: Vim measure for column width (tab = `tabstop` value).

### Coding Styles
- **C#**: Follows the [DotNet coding style](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md).
- **F#**: 
    - Prefix private fields with an underscore (`_fieldName`).
    - Use spaces between values and operators, record names and values, names and explicit types, and keywords and parentheses.
    - Avoid semicolons for multi-line object initializers.

### Testing
- Tests use **xUnit**.
- Test projects are organized by Visual Studio version (e.g., `Test/VimCoreTest2022/`, `Test/VsVimTest2022/`).
- Use **VimApp** for rapid manual verification of UI and editing behavior.

## Key Files
- `VsVim.sln`: Main solution file.
- `Directory.Build.props`: Shared build properties and reference paths.
- `Scripts/Build.ps1`: Primary build and automation script.
- `Src/VimCore/CoreInterfaces.fs`: Primary Vim engine interfaces (`IVimBuffer`, `IVim`, `IVimTextBuffer`).
- `Src/VimCore/CoreTypes.fs`: Core types, motions, and command definitions.
