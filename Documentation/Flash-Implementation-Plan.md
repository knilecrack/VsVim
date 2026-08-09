# Flash-Style Jump Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement flash.nvim-style labeled jump navigation in VsVim, entered via new `:Flash` Ex commands, with inline glyph labels.

**Architecture:** New `ModeKind.Flash` + `FlashMode` in the F# core (shaped like `SubstituteConfirmMode`) owns keystroke capture, match finding over visible lines, label assignment and jumping. A `FlashTaggerSource` (F#, `Tagger.fs`) publishes labels as `FlashLabelTag : IGlyphTag`; a glyph factory in VimWpf renders them. Entry is via a new `LineCommand.Flash` Ex command so users bind keys with `:nmap s :Flash<CR>`.

**Tech Stack:** F# (VimCore), C# (VimWpf, tests), xUnit, .NET Framework 4.7.2.

**Spec:** `Documentation/Flash-Design.md` (validated design, decision log, edge cases).

## Errata (applied during execution)

- Task 2's test expectations as originally written were mutually unsatisfiable with the default caret at position 0 (the caret match is excluded for `FlashKind.Search`). Resolved in the committed tests: caret moved off the position-0 match in `TypeChar_FindsMatchesInVisibleText`, `SmartCase_*`, `IgnoreCase_*`; the first `IgnoreCase` assert is 2 (not 1). Tasks 3-4 must keep caret positions deliberate in their tests.
- Test idiom: `IFlashMode.Matches` is an F# list — use `.Length` (not `.Count`) from C#.
- The test `Create` helper needs `_textView.SetVisibleLineCount(lines.Length)` (IWpfTextView) in addition to `DisplayTextLineContainingBufferPosition`, or only line 0 is visible.
- On this machine the xUnit console runner lives under the redirected NuGet cache `J:\packages\mcmausknile_nuget\.nuget\packages\xunit.runner.console\2.4.1\tools\net472\xunit.console.x86.exe`, not `%UserProfile%\.nuget\packages`.
- The PowerShell build command needs single quotes in Git Bash so `$msbuild` is not eaten by bash.
- Task 3 review found a plan defect: `AssignLabels` seeded `usedLabels` empty, allowing duplicate labels once the caret moves. Fixed in Task 4 by pre-seeding `usedLabels` with `_labelMap` values.
- Task 4 found a second plan defect: verbatim label precedence made any label char unusable as a search-continuation char (e.g. could never type 'a' to narrow when 'a' labeled a match). Fixed flash.nvim-style in `AssignLabels`: a fresh label is not taken from the character immediately following that match's span (the char the user would type to narrow). `KeyInputUtil.EnterKey` exists; there is no `BackKey` — use `KeyNotationUtil.StringToKeyInput("<BS>")`.

## Global Constraints

- House rules from `AGENTS.md` apply: F# spacing conventions, no LINQ in hot paths, imperative loops, options over nulls, ASCII-only source.
- Tests go in the shared `Test/VimCoreTest/` project (`.projitems`), NOT in per-version projects, so they run against VS2019/2022/2026.
- No default key bindings; built-in `f`/`s` behavior must be untouched.
- Label alphabet: `asdfghjklqwertyuiopzxcvbnm`. Case follows `ignorecase`/`smartcase`.
- New files must be added to the owning project file (`VimCore.fsproj`, `VimWpf.projitems`, `VimCoreTest.projitems`) — nothing compiles otherwise.
- Build a single test project with:
  ```powershell
  $msbuild = & Tools\vswhere.exe -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
  & $msbuild /nologo /v:m /m /p:Configuration=Debug Test\VimCoreTest2022\VimCoreTest2022.csproj
  ```
- Run tests with the xUnit console from the NuGet cache:
  ```cmd
  %UserProfile%\.nuget\packages\xunit.runner.console\2.4.1\tools\net472\xunit.console.x86.exe Binaries\Debug\VimCoreTest2022\net472\Vim.Core.2022.UnitTest.dll -class Vim.UnitTest.FlashModeTest
  ```
- Commit after every task with a plain imperative message (repo style: no `feat:` prefix).

---

### Task 1: Core plumbing + FlashMode skeleton (enter / Esc)

**Files:**
- Modify: `Src/VimCore/CoreInterfaces.fs` (ModeKind at line 1262; before ModeArgument at line 2687; ModeArgument at 2687-2742; after ISubstituteConfirmMode at line 6102; IVimBuffer after line 5835)
- Modify: `Src/VimCore/VimBuffer.fs` (near line 293)
- Modify: `Src/VimCore/Vim.fs` (modeList, line 295-305)
- Create: `Src/VimCore/Modes_Flash_FlashMode.fs`
- Create: `Src/VimCore/Modes_Flash_FlashMode.fsi`
- Modify: `Src/VimCore/VimCore.fsproj` (after line 77)
- Modify: `Src/VimEditorHost/VimUtil.cs` (near CreateSubstituteArgument, line 224)
- Create: `Test/VimCoreTest/FlashModeTest.cs`
- Modify: `Test/VimCoreTest/VimCoreTest.projitems` (near line 110)

**Interfaces:**
- Produces: `ModeKind.Flash`; `FlashKind` (`Search` | `FindCharForward` | `FindCharBackward` | `TillCharForward` | `TillCharBackward`); `ModeArgument.Flash of FlashKind`; `FlashMatch` record (`Span: SnapshotSpan`, `Label: string`); `IFlashMode` (`SearchText`, `Matches`, `MatchesChanged`, inherits `IMode`); `IVimBuffer.FlashMode`; `VimUtil.CreateFlashArgument(FlashKind)`.
- Consumes: nothing from other tasks.

- [ ] **Step 1: Write the failing test** — `Test/VimCoreTest/FlashModeTest.cs` (modeled on `SubstituteConfirmModeTest.cs`):

```csharp
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Modes.Flash;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class FlashModeTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private Mock<ICommonOperations> _operations;
        private FlashMode _modeRaw;
        private IFlashMode _mode;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _operations = new Mock<ICommonOperations>(MockBehavior.Loose);
            var vimBufferData = CreateVimBufferData(_textView);
            _modeRaw = new FlashMode(vimBufferData, _operations.Object);
            _mode = _modeRaw;
        }

        [WpfFact]
        public void ModeKind_IsFlash()
        {
            Create("cat", "dog");
            Assert.Equal(ModeKind.Flash, _mode.ModeKind);
        }

        [WpfFact]
        public void Escape_SwitchesToNormal()
        {
            Create("cat", "dog");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            Assert.True(_mode.Process(KeyInputUtil.EscapeKey).IsSwitchMode(ModeKind.Normal));
        }
    }
}
```

(`Process` has an overload taking a single `KeyInput` via extension; check `SubstituteConfirmModeTest.cs` line 105 for the exact call shape.)

- [ ] **Step 2: Run test to verify it fails** — build per Global Constraints; expect compile failure (`FlashMode` does not exist).

- [ ] **Step 3: Core type additions** — in `Src/VimCore/CoreInterfaces.fs`:

In `ModeKind` (after `| ExternalEdit = 12`):
```fsharp
    | Flash = 13
```

Immediately before `type ModeArgument =` (line 2687):
```fsharp
/// The kind of session to run when Flash mode is entered
[<RequireQualifiedAccess>]
type FlashKind =

    /// Incrementally typed string search over the visible text
    | Search

    /// Single char search in the visible text after the caret ('f')
    | FindCharForward

    /// Single char search in the visible text before the caret ('F')
    | FindCharBackward

    /// Like FindCharForward but the jump lands before the match ('t')
    | TillCharForward

    /// Like FindCharBackward but the jump lands after the match ('T')
    | TillCharBackward
```

In `ModeArgument`, after the `CancelOperation` case:
```fsharp
    /// Enter flash mode with the given session kind
    | Flash of FlashKind: FlashKind
```
and in its `LinkedUndoTransaction` member add:
```fsharp
        | ModeArgument.Flash _ -> Option.None
```

Immediately after `and ISubstituteConfirmMode = ... inherit IMode` (line ~6102):
```fsharp
/// A single labeled match in a flash session
and FlashMatch = {

    /// The span of the matched text
    Span: SnapshotSpan

    /// The label which jumps to this match
    Label: string
}

and IFlashMode =

    /// The current search text (FlashKind.Search) or target char (find kinds)
    abstract SearchText: string

    /// The current set of labeled matches
    abstract Matches: FlashMatch list

    /// Raised when SearchText or Matches change, and when the session ends
    [<CLIEvent>]
    abstract MatchesChanged: IDelegateEvent<System.EventHandler>

    inherit IMode
```

In `IVimBuffer` after `abstract SubstituteConfirmMode: ISubstituteConfirmMode` (line 5835):
```fsharp
    /// IFlashMode instance for flash mode
    abstract FlashMode: IFlashMode
```

In `Src/VimCore/VimBuffer.fs` next to `member x.SubstituteConfirmMode` (~line 293), inside the same `interface IVimBuffer with` implementation:
```fsharp
        member x.FlashMode = _modeMap.GetMode ModeKind.Flash :?> IFlashMode
```

In `Src/VimCore/Vim.fs` modeList, after the SubstituteConfirmMode line:
```fsharp
        ((Modes.Flash.FlashMode(vimBufferData, commonOperations) :> IMode))
```

- [ ] **Step 4: Create the mode skeleton** — `Src/VimCore/Modes_Flash_FlashMode.fsi`:

```fsharp
#light

namespace Vim.Modes.Flash
open Vim
open Vim.Modes

type internal FlashMode =
    new: IVimBufferData * ICommonOperations -> FlashMode

    interface IFlashMode
```

`Src/VimCore/Modes_Flash_FlashMode.fs`:

```fsharp
#light

namespace Vim.Modes.Flash
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor

type internal FlashMode
    (
        _vimBufferData: IVimBufferData,
        _operations: ICommonOperations
    ) as this =

    let _vimTextBuffer = _vimBufferData.VimTextBuffer
    let _textView = _vimBufferData.TextView
    let _globalSettings = _vimTextBuffer.GlobalSettings
    let _matchesChanged = StandardEvent()
    let _backKey = KeyNotationUtil.StringToKeyInput "<BS>"
    let _enterKey = KeyNotationUtil.StringToKeyInput "<CR>"

    /// The label alphabet, in assignment order.  Home row first like flash.nvim
    static let LabelChars = "asdfghjklqwertyuiopzxcvbnm"

    let mutable _kind = FlashKind.Search
    let mutable _searchText = ""
    let mutable _matches: FlashMatch list = []

    /// Stability map: match start position -> assigned label.  A match keeps
    /// its label as the search text is narrowed
    let mutable _labelMap: Map<int, char> = Map.empty

    member x.EndSession () =
        _searchText <- ""
        _matches <- []
        _labelMap <- Map.empty
        _matchesChanged.Trigger this

    member x.CanProcess (keyInput: KeyInput) =
        KeyInputUtil.IsCore keyInput && not keyInput.IsMouseKey

    member x.Process (keyInputData: KeyInputData) =
        let keyInput = keyInputData.KeyInput
        if keyInput = KeyInputUtil.EscapeKey then
            x.EndSession()
            ProcessResult.Handled (ModeSwitch.SwitchMode ModeKind.Normal)
        else
            ProcessResult.Handled ModeSwitch.NoSwitch

    member x.OnEnter (arg: ModeArgument) =
        arg.CompleteAnyTransaction()
        _kind <-
            match arg with
            | ModeArgument.Flash kind -> kind
            | _ -> FlashKind.Search
        _searchText <- ""
        _matches <- []
        _labelMap <- Map.empty

    interface IFlashMode with
        member x.VimTextBuffer = _vimTextBuffer
        member x.ModeKind = ModeKind.Flash
        member x.CommandNames = LabelChars |> Seq.map (fun c -> KeyInputSet(KeyInputUtil.CharToKeyInput c))
        member x.CanProcess keyInput = x.CanProcess keyInput
        member x.Process keyInputData = x.Process keyInputData
        member x.SearchText = _searchText
        member x.Matches = _matches
        member x.OnEnter arg = x.OnEnter arg
        member x.OnLeave () = x.EndSession()
        member x.OnClose () = ()
        [<CLIEvent>]
        member x.MatchesChanged = _matchesChanged.Publish
```

In `Src/VimCore/VimCore.fsproj` after line 77:
```xml
    <Compile Include="Modes_Flash_FlashMode.fsi" />
    <Compile Include="Modes_Flash_FlashMode.fs" />
```

In `Src/VimEditorHost/VimUtil.cs` next to `CreateSubstituteArgument` (line 224):
```csharp
        internal static ModeArgument CreateFlashArgument(FlashKind kind)
        {
            return ModeArgument.NewFlash(kind);
        }
```

In `Test/VimCoreTest/VimCoreTest.projitems` next to the SubstituteConfirmModeTest entry (line 110):
```xml
    <Compile Include="$(MSBuildThisFileDirectory)FlashModeTest.cs" />
```

- [ ] **Step 5: Run test to verify it passes** — build + run `Vim.UnitTest.FlashModeTest`; expect 2 passed.

- [ ] **Step 6: Commit**
```bash
git add Src/VimCore/CoreInterfaces.fs Src/VimCore/VimBuffer.fs Src/VimCore/Vim.fs Src/VimCore/VimCore.fsproj Src/VimCore/Modes_Flash_FlashMode.fs Src/VimCore/Modes_Flash_FlashMode.fsi Src/VimEditorHost/VimUtil.cs Test/VimCoreTest/FlashModeTest.cs Test/VimCoreTest/VimCoreTest.projitems
git commit -m "Add Flash mode skeleton with ModeKind.Flash and IFlashMode"
```

---

### Task 2: Match computation for FlashKind.Search

**Files:**
- Modify: `Src/VimCore/Modes_Flash_FlashMode.fs`
- Modify: `Test/VimCoreTest/FlashModeTest.cs`

**Interfaces:**
- Consumes: Task 1 skeleton.
- Produces: `FlashMode.Recompute()`, `IFlashMode.Matches`/`SearchText` live data; later tasks rely on `Recompute` being the single update path.

- [ ] **Step 1: Write failing tests** — add to `FlashModeTest.cs`. NOTE: visible lines require a real layout; force it like `NormalModeIntegrationTest.cs:6703` does with `DisplayTextLineContainingBufferPosition`. Update `Create` to end with:

```csharp
            _textView.DisplayTextLineContainingBufferPosition(
                _textBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
```

Tests:

```csharp
        [WpfFact]
        public void TypeChar_FindsMatchesInVisibleText()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            Assert.Equal("c", _mode.SearchText);
            Assert.Equal(2, _mode.Matches.Count);
        }

        [WpfFact]
        public void TypeChar_ExcludesMatchAtCaret()
        {
            Create("cat cat");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(0));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            Assert.Single(_mode.Matches);
        }

        [WpfFact]
        public void MatchesChanged_RaisedOnType()
        {
            Create("cat", "dog");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            var count = 0;
            _mode.MatchesChanged += (sender, args) => count++;
            _mode.Process('c');
            Assert.Equal(1, count);
        }

        [WpfFact]
        public void IgnoreCase_MatchesUpperAndLower()
        {
            Create("Cat cat");
            Vim.GlobalSettings.IgnoreCase = true;
            Vim.GlobalSettings.SmartCase = false;
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            Assert.Equal(1, _mode.Matches.Count);
            _mode.Process('a');
            Assert.Equal(2, _mode.Matches.Count);
        }

        [WpfFact]
        public void SmartCase_UppercasePatternIsCaseSensitive()
        {
            Create("Cat cat");
            Vim.GlobalSettings.IgnoreCase = true;
            Vim.GlobalSettings.SmartCase = true;
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('C');
            Assert.Single(_mode.Matches);
        }
```

(Adjust global settings reset: `VimTestBase` resets settings between tests; if not, save/restore in the test. `Vim` property is available on `VimTestBase` — verify against `IncrementalSearchTest.cs`.)

- [ ] **Step 2: Run to verify failure** — matches are always empty in the skeleton.

- [ ] **Step 3: Implement** — add to `FlashMode` in `Modes_Flash_FlashMode.fs` (add `open System` at the top):

```fsharp
    member x.CaretPoint = TextViewUtil.GetCaretPoint _textView

    /// Is the search for the given text case sensitive, honoring
    /// the 'ignorecase' and 'smartcase' options
    member x.IsCaseSensitive (text: string) =
        if not _globalSettings.IgnoreCase then true
        elif _globalSettings.SmartCase && (text |> Seq.exists Char.IsUpper) then true
        else false

    /// Find all occurrences of text in the visible extent
    member x.FindMatches (text: string): SnapshotSpan list =
        if StringUtil.IsNullOrEmpty text then []
        else
            match TextViewUtil.GetVisibleSnapshotLineRange _textView with
            | None -> []
            | Some lineRange ->
                let extent = lineRange.ExtentIncludingLineBreak
                let haystack = extent.GetText()
                let comparison =
                    if x.IsCaseSensitive text then StringComparison.Ordinal
                    else StringComparison.OrdinalIgnoreCase
                let startPosition = extent.Start.Position
                let snapshot = extent.Snapshot
                let result = ResizeArray<SnapshotSpan>()
                let mutable index = haystack.IndexOf(text, comparison)
                while index >= 0 do
                    let start = SnapshotPoint(snapshot, startPosition + index)
                    result.Add(SnapshotSpan(start, text.Length))
                    let nextIndex = index + max 1 text.Length
                    if nextIndex >= haystack.Length then
                        index <- -1
                    else
                        index <- haystack.IndexOf(text, nextIndex, comparison)
                List.ofSeq result

    /// Order the matches by jump priority for the current kind
    member x.OrderMatches (matches: SnapshotSpan list) =
        let caretPosition = x.CaretPoint.Position
        match _kind with
        | FlashKind.Search ->
            matches
            |> List.filter (fun span -> span.Start.Position <> caretPosition)
            |> List.sortBy (fun span -> abs (span.Start.Position - caretPosition))
        | _ -> matches

    /// Recompute matches and labels for the current search text.  This is
    /// the single update path; it always raises MatchesChanged
    member x.Recompute () =
        let ordered = x.FindMatches _searchText |> x.OrderMatches
        let currentPositions = ordered |> List.map (fun span -> span.Start.Position) |> Set.ofList
        _labelMap <- _labelMap |> Map.filter (fun position _ -> Set.contains position currentPositions)
        _matches <- ordered |> List.map (fun span -> { Span = span; Label = "" })
        _matchesChanged.Trigger this
```

And in `Process`, replace the final `else` branch with:

```fsharp
        elif keyInput = _backKey then
            if _searchText.Length > 0 then
                _searchText <- _searchText.Substring(0, _searchText.Length - 1)
                x.Recompute()
            ProcessResult.Handled ModeSwitch.NoSwitch
        else
            match keyInput.RawChar with
            | None -> ProcessResult.Handled ModeSwitch.NoSwitch
            | Some c ->
                _searchText <- _searchText + string c
                x.Recompute()
                ProcessResult.Handled ModeSwitch.NoSwitch
```

(If `keyInput.RawChar` does not compile, check the `KeyInput` API at `CoreInterfaces.fs:1339+` for the char-option property and use that instead.)

- [ ] **Step 4: Run tests to verify they pass** — `-class Vim.UnitTest.FlashModeTest`, expect all green.

- [ ] **Step 5: Commit**
```bash
git add Src/VimCore/Modes_Flash_FlashMode.fs Test/VimCoreTest/FlashModeTest.cs
git commit -m "Compute flash search matches over the visible text"
```

---

### Task 3: Label assignment with stability

**Files:**
- Modify: `Src/VimCore/Modes_Flash_FlashMode.fs`
- Modify: `Test/VimCoreTest/FlashModeTest.cs`

**Interfaces:**
- Consumes: `Recompute` from Task 2.
- Produces: stable `FlashMatch.Label` values; Task 4 jumps by label.

- [ ] **Step 1: Write failing tests**:

```csharp
        [WpfFact]
        public void Labels_AssignedByDistanceFromCaret()
        {
            Create("x a x", "x a x");
            _textView.Caret.MoveTo(_textBuffer.GetLine(0).Start);
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('x');
            // Caret is on the first 'x'; nearest remaining match gets "a".
            Assert.Equal("a", _mode.Matches[0].Label);
            Assert.Equal("s", _mode.Matches[1].Label);
        }

        [WpfFact]
        public void Labels_StableWhileNarrowing()
        {
            Create("ab ac ad");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('a');
            var firstLabels = _mode.Matches.Select(m => Tuple.Create(m.Span.Start.Position, m.Label)).ToList();
            _mode.Process('b');
            Assert.Single(_mode.Matches);
            Assert.Equal(firstLabels[0].Item2, _mode.Matches[0].Label);
        }

        [WpfFact]
        public void Labels_CappedByAlphabet()
        {
            // 30 matches, 26 labels: 4 matches get no label and are dropped
            // from the labeled list.
            var line = string.Join(" ", Enumerable.Repeat("q", 30));
            Create(line);
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('q');
            Assert.True(_mode.Matches.Count <= 26);
        }
```

(`Matches` only contains labeled matches; unlabeled overflow matches are dropped. Adjust implementation accordingly.)

- [ ] **Step 2: Run to verify failure** — labels are all `""`.

- [ ] **Step 3: Implement** — replace the `_matches <-` line in `Recompute` and add `AssignLabels` to `FlashMode`:

```fsharp
    /// Assign labels to the ordered matches, keeping previously assigned
    /// labels stable while the match is still present.  Matches beyond the
    /// label alphabet are dropped
    member x.AssignLabels (ordered: SnapshotSpan list): FlashMatch list =
        let usedLabels = System.Collections.Generic.HashSet<char>()
        let result = ResizeArray<FlashMatch>()
        let mutable labelIndex = 0
        for span in ordered do
            let position = span.Start.Position
            match Map.tryFind position _labelMap with
            | Some label ->
                usedLabels.Add label |> ignore
                result.Add { Span = span; Label = string label }
            | None ->
                let mutable label: char option = None
                while labelIndex < LabelChars.Length && label.IsNone do
                    let candidate = LabelChars.[labelIndex]
                    labelIndex <- labelIndex + 1
                    if not (usedLabels.Contains candidate) then
                        label <- Some candidate
                match label with
                | Some c ->
                    usedLabels.Add c |> ignore
                    _labelMap <- Map.add position c _labelMap
                    result.Add { Span = span; Label = string c }
                | None -> ()
        List.ofSeq result
```

In `Recompute`, replace `_matches <- ordered |> List.map ...` with:
```fsharp
        _matches <- x.AssignLabels ordered
```

- [ ] **Step 4: Run tests to verify they pass.**

- [ ] **Step 5: Commit**
```bash
git add Src/VimCore/Modes_Flash_FlashMode.fs Test/VimCoreTest/FlashModeTest.cs
git commit -m "Assign stable labels to flash matches"
```

---

### Task 4: Keystroke handling — jump, CR, backspace, label precedence

**Files:**
- Modify: `Src/VimCore/Modes_Flash_FlashMode.fs`
- Modify: `Test/VimCoreTest/FlashModeTest.cs`

**Interfaces:**
- Consumes: labeled matches from Task 3.
- Produces: `FlashMode.JumpTo (flashMatch: FlashMatch): ProcessResult` (used by Task 5).

- [ ] **Step 1: Write failing tests**:

```csharp
        [WpfFact]
        public void TypeLabel_JumpsAndSwitchesToNormal()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            var target = _mode.Matches[0];
            var result = _mode.Process(target.Label[0]);
            Assert.True(result.IsSwitchMode(ModeKind.Normal));
            _operations.Verify(x => x.MoveCaretToPoint(target.Span.Start, ViewFlags.Standard), Times.Once);
            Assert.Empty(_mode.Matches);
        }

        [WpfFact]
        public void Enter_JumpsToNearestMatch()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            var result = _mode.Process(KeyInputUtil.EnterKey);
            Assert.True(result.IsSwitchMode(ModeKind.Normal));
        }

        [WpfFact]
        public void Backspace_ShrinksSearch()
        {
            Create("cab", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            _mode.Process('a');
            Assert.Equal(2, _mode.Matches.Count);
            _mode.Process(KeyInputUtil.BackKey);
            Assert.Equal("c", _mode.SearchText);
        }
```

(If `KeyInputUtil.EnterKey`/`BackKey` don't exist, use `KeyNotationUtil.StringToKeyInput("<CR>")` / `"<BS>"` — check existing usage with grep first.)

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement** — add to `FlashMode`:

```fsharp
    /// Jump the caret to the given match and end the session
    member x.JumpTo (flashMatch: FlashMatch) =
        _operations.MoveCaretToPoint flashMatch.Span.Start ViewFlags.Standard
        x.EndSession()
        ProcessResult.Handled (ModeSwitch.SwitchMode ModeKind.Normal)
```

Rewrite `Process` as:

```fsharp
    member x.Process (keyInputData: KeyInputData) =
        let keyInput = keyInputData.KeyInput
        if keyInput = KeyInputUtil.EscapeKey then
            x.EndSession()
            ProcessResult.Handled (ModeSwitch.SwitchMode ModeKind.Normal)
        elif keyInput = _enterKey then
            match _matches with
            | [] -> ProcessResult.Handled ModeSwitch.NoSwitch
            | head :: _ -> x.JumpTo head
        elif keyInput = _backKey then
            if _searchText.Length > 0 then
                _searchText <- _searchText.Substring(0, _searchText.Length - 1)
                x.Recompute()
            ProcessResult.Handled ModeSwitch.NoSwitch
        else
            match keyInput.RawChar with
            | None -> ProcessResult.Handled ModeSwitch.NoSwitch
            | Some c ->
                // A typed label jumps once there is a search in progress;
                // otherwise the char extends the search text
                let labelMatch =
                    if StringUtil.IsNullOrEmpty _searchText then None
                    else _matches |> List.tryFind (fun m -> m.Label = string c)
                match labelMatch with
                | Some flashMatch -> x.JumpTo flashMatch
                | None ->
                    _searchText <- _searchText + string c
                    x.Recompute()
                    ProcessResult.Handled ModeSwitch.NoSwitch
```

- [ ] **Step 4: Run tests to verify they pass.**

- [ ] **Step 5: Commit**
```bash
git add Src/VimCore/Modes_Flash_FlashMode.fs Test/VimCoreTest/FlashModeTest.cs
git commit -m "Add flash jump, enter and backspace key handling"
```

---

### Task 5: FindChar / Till kinds

**Files:**
- Modify: `Src/VimCore/Modes_Flash_FlashMode.fs`
- Modify: `Test/VimCoreTest/FlashModeTest.cs`

**Interfaces:**
- Consumes: `JumpTo`, `Recompute`, `OrderMatches`.
- Produces: full `FlashKind` behavior consumed by the `:Flash` Ex command (Task 6).

- [ ] **Step 1: Write failing tests**:

```csharp
        [WpfFact]
        public void FindCharForward_OnlyForwardMatches()
        {
            Create("x mid x", "x end x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(2));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharForward));
            _mode.Process('x');
            Assert.All(_mode.Matches, m => Assert.True(m.Span.Start.Position > 2));
        }

        [WpfFact]
        public void FindCharBackward_OnlyBackwardMatches()
        {
            Create("x mid x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(6));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharBackward));
            _mode.Process('x');
            Assert.Single(_mode.Matches);
            Assert.Equal(0, _mode.Matches[0].Span.Start.Position);
        }

        [WpfFact]
        public void FindChar_ExtraCharsAfterTargetIgnored()
        {
            Create("x a x");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharForward));
            _mode.Process('x');
            var count = _mode.Matches.Count;
            _mode.Process('z'); // not a label, not the first char: ignored
            Assert.Equal(count, _mode.Matches.Count);
        }

        [WpfFact]
        public void TillCharForward_JumpsBeforeMatch()
        {
            Create("a x b x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(0));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.TillCharForward));
            _mode.Process('x');
            var target = _mode.Matches[0];
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), ViewFlags.Standard));
            _mode.Process(target.Label[0]);
            _operations.Verify(x => x.MoveCaretToPoint(
                It.Is<SnapshotPoint>(p => p.Position == target.Span.Start.Position - 1), ViewFlags.Standard), Times.Once);
        }
```

- [ ] **Step 2: Run to verify failure.**

- [ ] **Step 3: Implement** — replace `OrderMatches` and `JumpTo` and the append logic in `Process`:

```fsharp
    /// Order the matches by jump priority for the current kind
    member x.OrderMatches (matches: SnapshotSpan list) =
        let caretPosition = x.CaretPoint.Position
        match _kind with
        | FlashKind.Search ->
            matches
            |> List.filter (fun span -> span.Start.Position <> caretPosition)
            |> List.sortBy (fun span -> abs (span.Start.Position - caretPosition))
        | FlashKind.FindCharForward | FlashKind.TillCharForward ->
            matches
            |> List.filter (fun span -> span.Start.Position > caretPosition)
            |> List.sortBy (fun span -> span.Start.Position)
        | FlashKind.FindCharBackward | FlashKind.TillCharBackward ->
            matches
            |> List.filter (fun span -> span.Start.Position < caretPosition)
            |> List.sortByDescending (fun span -> span.Start.Position)
```

```fsharp
    /// Jump the caret to the given match and end the session.  Till kinds
    /// land one position before/after the match, clamped to the match line
    member x.JumpTo (flashMatch: FlashMatch) =
        let point = flashMatch.Span.Start
        let point =
            match _kind with
            | FlashKind.TillCharForward ->
                let line = SnapshotPointUtil.GetContainingLine point
                if point.Position > line.Start.Position then point.Subtract(1) else point
            | FlashKind.TillCharBackward ->
                let line = SnapshotPointUtil.GetContainingLine point
                if point.Position < line.End.Position then point.Add(1) else point
            | _ -> point
        _operations.MoveCaretToPoint point ViewFlags.Standard
        x.EndSession()
        ProcessResult.Handled (ModeSwitch.SwitchMode ModeKind.Normal)
```

In `Process`, in the `None` (no label match) arm:
```fsharp
                | None ->
                    match _kind with
                    | FlashKind.Search ->
                        _searchText <- _searchText + string c
                        x.Recompute()
                    | _ ->
                        // Find kinds take a single target char; further
                        // non-label chars are ignored
                        if StringUtil.IsNullOrEmpty _searchText then
                            _searchText <- string c
                            x.Recompute()
                    ProcessResult.Handled ModeSwitch.NoSwitch
```

- [ ] **Step 4: Run tests to verify they pass.**

- [ ] **Step 5: Commit**
```bash
git add Src/VimCore/Modes_Flash_FlashMode.fs Test/VimCoreTest/FlashModeTest.cs
git commit -m "Add flash find-char and till-char session kinds"
```

---

### Task 6: `:Flash` Ex command

**Files:**
- Modify: `Src/VimCore/Interpreter_Expression.fs` (after `HostCommand` case, line 603)
- Modify: `Src/VimCore/Interpreter_Parser.fs` (command alias list ~line 200; `noRangeCommand` handling line 937; dispatch ~line 2731; new `ParseFlash` near `ParseHostCommand` line 1990)
- Modify: `Src/VimCore/Interpreter_Interpreter.fs` (dispatch line 2450; new `RunFlash` near `RunHostCommand` line 2326)
- Modify: `Test/VimCoreTest/ParserTest.cs` (or the file containing HostCommand parse tests — locate with `grep -rn "vscmd" Test/VimCoreTest/`)

**Interfaces:**
- Consumes: `FlashKind`, `ModeArgument.Flash`.
- Produces: `LineCommand.Flash of FlashKind`; user-facing `:Flash [-f|-F|-t|-T]`.

- [ ] **Step 1: Write failing parser tests** (mirror an existing simple-command parse test in `ParserTest.cs`):

```csharp
        [Fact]
        public void Flash_NoFlag_IsSearch()
        {
            var lineCommand = ParseLineCommand("flash");
            var kind = Assert.IsType<LineCommand.Flash>(lineCommand).Item;
            Assert.Equal(FlashKind.Search, kind);
        }

        [Fact]
        public void Flash_DashF_IsFindCharForward()
        {
            var lineCommand = ParseLineCommand("flash -f");
            var kind = Assert.IsType<LineCommand.Flash>(lineCommand).Item;
            Assert.Equal(FlashKind.FindCharForward, kind);
        }
```

(Check the actual helper name in `ParserTest.cs` — e.g. `ParseLineCommand` — and copy the assertion style of a nearby single-case DU test. F# union case type name from C# is `LineCommand.Flash`.)

- [ ] **Step 2: Run to verify failure** (no `LineCommand.Flash`).

- [ ] **Step 3: Implement**

`Interpreter_Expression.fs`, after the `HostCommand` case (line 603):
```fsharp
    /// Enter flash mode with the given session kind
    | Flash of FlashKind: FlashKind
```

`Interpreter_Parser.fs` — in the command-name/alias tuple list (~line 200) add:
```fsharp
        ("flash", "flash")
```
In the range-handling match containing `| LineCommand.HostCommand _ -> noRangeCommand` (line 937) add:
```fsharp
            | LineCommand.Flash _ -> noRangeCommand
```
New parser member after `ParseHostCommand` (line 2008):
```fsharp
    /// Parse out a flash command.  Takes an optional flag: -f, -F, -t or -T.
    /// With no flag it starts a flash search
    member x.ParseFlash() =
        x.SkipBlanks()
        match x.ParseRestOfLine() with
        | "" -> LineCommand.Flash FlashKind.Search
        | "-f" -> LineCommand.Flash FlashKind.FindCharForward
        | "-F" -> LineCommand.Flash FlashKind.FindCharBackward
        | "-t" -> LineCommand.Flash FlashKind.TillCharForward
        | "-T" -> LineCommand.Flash FlashKind.TillCharBackward
        | _ -> x.ParseError Resources.Parser_Error
```
In the dispatch match (~line 2731) add:
```fsharp
                | "flash" -> x.ParseFlash()
```

`Interpreter_Interpreter.fs` — after `RunHostCommand` (line ~2335):
```fsharp
    /// Enter flash mode for the given session kind
    member x.RunFlash kind =
        _vimBuffer.SwitchMode ModeKind.Flash (ModeArgument.Flash kind)
```
In the dispatch (line 2450) add:
```fsharp
        | LineCommand.Flash kind -> x.RunFlash kind
```

- [ ] **Step 4: Run tests to verify they pass** (parser tests + full `FlashModeTest`).

- [ ] **Step 5: Commit**
```bash
git add Src/VimCore/Interpreter_Expression.fs Src/VimCore/Interpreter_Parser.fs Src/VimCore/Interpreter_Interpreter.fs Test/VimCoreTest/ParserTest.cs
git commit -m "Add :Flash ex command with -f/-F/-t/-T flags"
```

---

### Task 7: FlashLabelTag + FlashTaggerSource (core tagger)

**Files:**
- Modify: `Src/VimCore/Tagger.fs` (after `IncrementalSearchTaggerProvider`, line 124)
- Create: `Test/VimCoreTest/FlashTaggerSourceTest.cs` (modeled on `IncrementalSearchTaggerSourceTest.cs`)
- Modify: `Test/VimCoreTest/VimCoreTest.projitems`

**Interfaces:**
- Consumes: `IVimBuffer.FlashMode`, `IFlashMode.Matches`, `MatchesChanged`.
- Produces: `FlashLabelTag` (member `Chars: string`, implements `IGlyphTag`); `FlashTaggerSource` (`GetTags(SnapshotSpan)`); `FlashTaggerProvider` (MEF `IViewTaggerProvider`). Task 8's glyph factory consumes `FlashLabelTag.Chars`.

- [ ] **Step 1: Write the failing test** — `FlashTaggerSourceTest.cs`:

```csharp
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class FlashTaggerSourceTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private FlashTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<FlashLabelTag> _taggerSource;

        private void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textView.DisplayTextLineContainingBufferPosition(
                _textView.TextBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
            _taggerSourceRaw = new FlashTaggerSource(_vimBuffer);
            _taggerSource = _taggerSourceRaw;
        }

        private ITagSpan<FlashLabelTag>[] GetTags()
        {
            return _taggerSource.GetTags(_textView.TextSnapshot.GetExtent()).ToArray();
        }

        [WpfFact]
        public void NoSession_NoTags()
        {
            Create("cat", "dog");
            Assert.Empty(GetTags());
        }

        [WpfFact]
        public void Session_ProducesLabelTags()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            var tags = GetTags();
            Assert.Equal(2, tags.Length);
            Assert.Equal("a", tags[0].Tag.Chars);
        }

        [WpfFact]
        public void SessionEnd_ClearsTags()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            Assert.NotEmpty(GetTags());
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.Empty(GetTags());
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** (types don't exist).

- [ ] **Step 3: Implement** — in `Src/VimCore/Tagger.fs` after `IncrementalSearchTaggerProvider` (line 124):

```fsharp
/// Tag carrying the label text for a flash match
type FlashLabelTag (chars: string) =
    member x.Chars = chars
    interface IGlyphTag

/// Tagger for the labels of an active flash session
type FlashTaggerSource (_vimBuffer: IVimBuffer) as this =

    let _flashMode = _vimBuffer.FlashMode
    let _eventHandlers = DisposableBag()
    let _changed = StandardEvent()
    let mutable _matches: FlashMatch list = []

    static let EmptyTagList = ReadOnlyCollection<ITagSpan<FlashLabelTag>>([| |])

    do
        let raiseChanged () = _changed.Trigger this

        _flashMode.MatchesChanged
        |> Observable.subscribe (fun _ ->
            // Update before raising; the editor can call back synchronously
            _matches <- _flashMode.Matches
            raiseChanged())
        |> _eventHandlers.Add

    member x.GetTags (span: SnapshotSpan) =
        match _matches with
        | [] -> EmptyTagList
        | _ ->
            let snapshot = span.Snapshot
            let list = ResizeArray<ITagSpan<FlashLabelTag>>()
            for flashMatch in _matches do
                if flashMatch.Span.Snapshot = snapshot then
                    let startSpan = SnapshotSpan(flashMatch.Span.Start, 0)
                    if span.Contains(startSpan) then
                        let tag = FlashLabelTag(flashMatch.Label)
                        list.Add(TagSpan(startSpan, tag) :> ITagSpan<FlashLabelTag>)
            ReadOnlyCollection<ITagSpan<FlashLabelTag>>(list)

    interface IBasicTaggerSource<FlashLabelTag> with
        member x.GetTags span = x.GetTags span
        [<CLIEvent>]
        member x.Changed = _changed.Publish

    interface System.IDisposable with
        member x.Dispose() = _eventHandlers.DisposeAll()

[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(VimConstants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Editable)>]
[<TagType(typeof<FlashLabelTag>)>]
type internal FlashTaggerProvider
    [<ImportingConstructor>]
    (
        _vim: IVim
    ) =

    let _key = obj()

    interface IViewTaggerProvider with
        member x.CreateTagger<'T when 'T :> ITag> (textView: ITextView, textBuffer) =
            if textView.TextBuffer = textBuffer then
                match _vim.GetOrCreateVimBufferForHost textView with
                | None -> null
                | Some vimBuffer ->
                    let func () =
                        let taggerSource = new FlashTaggerSource(vimBuffer)
                        taggerSource :> IBasicTaggerSource<FlashLabelTag>
                    let tagger = TaggerUtil.CreateBasicTagger textView.Properties _key func
                    tagger :> obj :?> ITagger<'T>
            else
                null
```

(`Tagger.fsi` needs no change — mirror of how `IncrementalSearchTaggerSource` is handled.)

- [ ] **Step 4: Run tests to verify they pass** — `-class Vim.UnitTest.FlashTaggerSourceTest`.

- [ ] **Step 5: Commit**
```bash
git add Src/VimCore/Tagger.fs Test/VimCoreTest/FlashTaggerSourceTest.cs Test/VimCoreTest/VimCoreTest.projitems
git commit -m "Add flash label tagger source and provider"
```

---

### Task 8: VimWpf glyph rendering

**Files:**
- Create: `Src/VimWpf/Implementation/FlashGlyph/FlashGlyphFactory.cs`
- Create: `Src/VimWpf/Implementation/FlashGlyph/FlashGlyphFactoryProvider.cs`
- Modify: `Src/VimWpf/VimWpf.projitems` (after line 52)

**Interfaces:**
- Consumes: `FlashLabelTag.Chars` (Task 7).
- Produces: visible labels in the editor (manual verification via VimApp).

- [ ] **Step 1: Implement** — `FlashGlyphFactoryProvider.cs` (clone of `MarkGlyphFactoryProvider.cs`):

```csharp
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.FlashGlyph
{
    [Export(typeof(IGlyphFactoryProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TagType(typeof(FlashLabelTag))]
    [Name("FlashGlyph")]
    [Order(Before = "VsTextMarker")]
    internal sealed class FlashGlyphFactoryProvider : IGlyphFactoryProvider
    {
        private readonly IVim _vim;
        private readonly IClassificationFormatMapService _classificationFormatMapService;

        [ImportingConstructor]
        internal FlashGlyphFactoryProvider(IVim vim, IClassificationFormatMapService classificationFormatMapService)
        {
            _vim = vim;
            _classificationFormatMapService = classificationFormatMapService;
        }

        public IGlyphFactory GetGlyphFactory(IWpfTextView textView, IWpfTextViewMargin margin)
        {
            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(textView);
            return new FlashGlyphFactory(_vim, classificationFormatMap);
        }
    }
}
```

`FlashGlyphFactory.cs` (clone of `MarkGlyphFactory.cs`, with a highlight background so labels stand out):

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.Implementation.FlashGlyph
{
    internal class FlashGlyphFactory : IGlyphFactory
    {
        private readonly IVim _vim;
        private readonly IClassificationFormatMap _classificationFormatMap;

        internal FlashGlyphFactory(IVim vim, IClassificationFormatMap classificationFormatMap)
        {
            _vim = vim;
            _classificationFormatMap = classificationFormatMap;
        }

        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            if (tag is FlashLabelTag flashTag)
            {
                var textRunProperties = _classificationFormatMap.DefaultTextProperties;
                var typeface = textRunProperties.Typeface;
                var fontSize = textRunProperties.FontRenderingEmSize;

                var textBlock = new TextBlock
                {
                    Text = flashTag.Chars,
                    Foreground = SystemColors.HighlightTextBrush,
                    Background = SystemColors.HighlightBrush,
                    FontFamily = typeface.FontFamily,
                    FontStretch = typeface.Stretch,
                    FontWeight = FontWeights.Bold,
                    FontStyle = typeface.Style,
                    FontSize = fontSize,
                    ToolTip = "VsVim Flash",
                };

                return textBlock;
            }
            else
            {
                return null;
            }
        }
    }
}
```

In `Src/VimWpf/VimWpf.projitems` after line 52:
```xml
    <Compile Include="$(MSBuildThisFileDirectory)Implementation\FlashGlyph\FlashGlyphFactory.cs" />
    <Compile Include="$(MSBuildThisFileDirectory)Implementation\FlashGlyph\FlashGlyphFactoryProvider.cs" />
```

- [ ] **Step 2: Build the full solution** — `Build.cmd` (Debug) must succeed; VimWpf is compiled into VimApp and the VSIX hosts.

- [ ] **Step 3: Manual smoke test in VimApp** — set VimApp as startup, F5, create a buffer with repeated words, run `:Flash<CR>`, type a char, verify labels render at matches; type a label, verify the caret jumps; `<Esc>` cancels. (If running headless, note this as a manual step for the user.)

- [ ] **Step 4: Commit**
```bash
git add Src/VimWpf/Implementation/FlashGlyph Src/VimWpf/VimWpf.projitems
git commit -m "Render flash labels as inline glyphs"
```

---

### Task 9: Command margin banner + mode audit

**Files:**
- Modify: `Src/VimWpf/Implementation/CommandMargin/CommandMarginUtil.cs` (`GetStatusCommon`, ~line 115)

**Interfaces:**
- Consumes: `ModeKind.Flash`.
- Produces: "FLASH" shown in the command margin while the mode is active.

- [ ] **Step 1: Implement** — in the `GetStatusCommon` switch add:
```csharp
                case ModeKind.Flash:
                    return "FLASH";
```
Audit (no change expected, verify only):
- `BlockCaretController.CalcCaretKind` (line 89-139): unlisted modes default to `CaretDisplay.Block` — desired (flash keeps the normal block caret).
- `VimBuffer.fs` `KeyRemapMode` (line 297-307): falls into `KeyRemapMode.None` — desired (no remapping inside flash).
- `ImeCoordinator.cs` (line 328-353): verify the default branch treats Flash like Normal (IME off).

- [ ] **Step 2: Build + run full VimCoreTest2022 + VimWpfTest suites** per Global Constraints; all green.

- [ ] **Step 3: Commit**
```bash
git add Src/VimWpf/Implementation/CommandMargin/CommandMarginUtil.cs
git commit -m "Show FLASH in the command margin for flash mode"
```

---

### Task 10: Docs + full verification

**Files:**
- Modify: `Documentation/Supported Features.md` (add `:Flash` under supported commands)
- Modify: `Documentation/release-notes.md` (add entry under the in-progress version)
- Modify: `Documentation/Flash-Design.md` (mark status: implemented)

- [ ] **Step 1: Docs.** `Supported Features.md` — add to the command list:
```
* :Flash [-f|-F|-t|-T] - flash-style labeled jump navigation over the visible text (bind e.g. :nmap s :Flash<CR>)
```
`release-notes.md` — under the in-progress version's Features:
```
* Flash-style jump navigation: ':Flash' shows labels on all matches in the visible text as you type; type a label to jump. ':Flash -f/-F/-t/-T' provides labeled f/F/t/T motions across all visible lines. Bind via .vsvimrc, e.g. ':nmap s :Flash<CR>'.
```

- [ ] **Step 2: Full test run** — `powershell -ExecutionPolicy ByPass -NoProfile -command "& Scripts/Build.ps1 -test"`; all suites green (2019, 2022, 2026).

- [ ] **Step 3: Commit**
```bash
git add "Documentation/Supported Features.md" Documentation/release-notes.md Documentation/Flash-Design.md
git commit -m "Document :Flash command and release notes"
```

---

## Self-Review Notes

- Spec coverage: every design-doc component maps to a task (mode=1-5, Ex command=6, tagger=7, glyph=8, banner/audit=9, docs=10). FindChar range decision (all visible lines) is implemented in Task 5 via `OrderMatches` over the visible extent.
- Open risk to verify early (Task 2): `TextViewUtil.GetVisibleSnapshotLineRange` in unit tests requires a laid-out view — `Create` forces layout with `DisplayTextLineContainingBufferPosition`. If tests still see no `TextViewLines`, copy the setup approach from the scroll tests in `NormalModeIntegrationTest.cs`.
- Open API names flagged inline where uncertain: `keyInput.RawChar`, `KeyInputUtil.EnterKey`/`BackKey`, `ParseLineCommand` helper in `ParserTest.cs`, settings reset behavior in `VimTestBase`.
