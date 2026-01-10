# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VsVim is a free Vim emulator for Visual Studio 2019 and 2022. The core vim implementation is written in F#, with Visual Studio integration written in C#.

## Build Commands

```bash
# Build the solution
Build.cmd

# Build with specific configuration
Build.cmd -config Release

# Run unit tests
powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts\Build.ps1 -test"

# Run extra verification (VSIX contents, version consistency)
powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts\Build.ps1 -testExtra"
```

## Development Requirements

- Visual Studio 2022 with:
  - .NET Desktop Development workload
  - F# Language
  - Visual Studio Extension Development workload

## Testing

Tests use xUnit. Test projects are versioned by VS version (2019/2022):
- `Test/VimCoreTest2019/` and `Test/VimCoreTest2022/` - Core vim engine tests
- `Test/VsVimTest2019/` and `Test/VsVimTest2022/` - VS integration tests

Unit tests can target different VS editor versions via `%VsVimTargetVersion%` environment variable (14.0, 15.0, 16.0).

**VimApp** (`Src/VimApp/`) is a lightweight WPF editor host for rapid testing without launching full Visual Studio.

## Architecture

### Core Layers

**VimCore** (`Src/VimCore/`) - F# project containing the entire vim engine, editor-agnostic:
- `CoreInterfaces.fs` - Primary interfaces: `IVimBuffer`, `IVim`, `IVimTextBuffer`
- `CoreTypes.fs` - Core discriminated unions and types (motions, commands, registers)
- `Modes_Normal_NormalMode.fs` - Normal mode command definitions
- `Modes_Visual_VisualMode.fs` - Visual mode implementation
- `Modes_Insert_InsertMode.fs` - Insert mode implementation
- `CommandRunner.fs` - Executes commands, handles counts and registers
- `MotionCapture.fs` - Parses motion commands
- `MotionUtil.fs` - Motion calculations (word, line, paragraph movements)
- `CommandUtil.fs` - Command execution logic
- `Interpreter_*.fs` - Ex command parser and interpreter
- `VimSettings.fs` - Vim settings implementation

**VsVimShared** (`Src/VsVimShared/`) - Shared C# code for VS integration:
- `VsVimHost.cs` - IVimHost implementation bridging vim to VS
- `VsCommandTarget.cs` - Command routing from VS to VsVim
- `VsVimPackage.cs` - VS package entry point

**VimWpf** (`Src/VimWpf/`) - WPF-specific rendering (command margin, block caret)

**VimEditorHost** (`Src/VimEditorHost/`) - Shared source for hosting IVimBuffer in tests and VimApp

### Key Patterns

- Commands are defined as discriminated unions (`NormalCommand`, `VisualCommand`, etc.)
- `ICommandRunner` processes key input through command bindings
- `IMotionCapture` handles motion argument collection for commands like `d{motion}`
- Shared projects (`.shproj`) enable code sharing across VS version-specific projects

## Coding Guidelines

### F# Style
- Prefix private fields with underscore (`_fieldName`)
- Add spaces between: values and operators, names and values in records, names and explicit types, keywords and parens
- Do not use semicolons for multi-line object initializers

### Terminology
- **Last** is inclusive, **End** is exclusive
- **Position** - Editor's SnapshotPoint, may be mid-line feed
- **Column** - Vim unit (tab = 1 column, astral code point = 1 column)
- **Spaces** - Vim measure for column width (tab = tabstop value, wide char = 2)

### C# Style
Follow [DotNet coding style](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md)
