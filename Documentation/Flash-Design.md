# Flash-Style Jump Navigation — Design

Status: implemented (2026-07-27). See `Documentation/Flash-Implementation-Plan.md` for the task-by-task implementation plan.

Implements flash.nvim-style jump navigation for VsVim: press a (user-mapped)
trigger, type 1-2 search chars, all matches in the visible window get
letter labels, type a label to jump the caret there. Also an enhanced
`f`/`F`/`t`/`T` variant that labels matches across all visible lines.

## Understanding Summary

- Feature: interactive flash search mode (dynamic labels) + enhanced
  find-char motions (`f`/`F`/`t`/`T` with labels).
- Opt-in: no default key bindings. VsVim has no `<Plug>` support and `:map`
  only replays literal keystrokes, so the feature is exposed as Ex commands
  (`:Flash`, `:Flash -f/-F/-t/-T`) that users map in `.vsvimrc`, e.g.
  `:nmap s :Flash<CR>`. Built-in `f`/`s` behavior is untouched.
- Rendering: inline glyph tagger (`IGlyphTag`), following the `MarkGlyph`
  precedent — not a WPF adornment layer.
- Scope is the active view's visible lines only; labels are stable per match
  while narrowing; matches ordered by distance from caret.
- Explicit non-goals (v1): operator integration (`d<label>`), remote
  operations, vim-style `:set` options, multi-window search.

## Assumptions

- Label alphabet hardcoded to `asdfghjklqwertyuiopzxcvbnm` (flash.nvim
  default). Add settings later only if requested.
- Case sensitivity follows the existing `ignorecase`/`smartcase` options.
- Search-jump matches in both directions from the caret; FindChar respects
  the f/F/t/T direction but spans all visible lines.
- Performance: synchronous match computation over visible lines only; must
  keep up with typing. No LINQ in the hot path, imperative loops per house
  rules. Label count is capped by the alphabet (26), so matches beyond that
  simply get no label.
- Maintenance: all logic lives in the F# core and is tested in the shared
  `Test/VimCoreTest/` project so it runs against VS2019/2022/2026.

## Decision Log

| Decision | Chosen | Alternatives rejected |
|---|---|---|
| Scope | Search-jump + f/F/t/T labels | Operators, remote ops (deferred) |
| Trigger | Ex commands `:Flash` / `:Flash -f/-F/-t/-T`, user maps via `:map` | Default `s` binding (breaks vim compat); `<Plug>` support (too big for v1) |
| Rendering | Inline glyph tagger (`MarkGlyph` pattern) | Overlay adornment; highlight-only |
| Interaction | flash-style dynamic labels, stable per match | Fixed 2-char then labels; single-char |
| Config | Hardcoded defaults; follow `ignorecase`/`smartcase` | Vim options up front |
| Architecture | Core mode + session events + tagger | Thin C# shim; reuse incsearch session |
| FindChar range | All visible lines | Caret line only |

## Final Design

### Components

F# core (`Src/VimCore/`):

- `CoreInterfaces.fs` — add `ModeKind.Flash` and `IFlashMode` (extends
  `IMode`), exposed as `IVimBuffer.FlashMode`. The mode exposes the current
  `SearchText`, a `FlashMatch list` (span + label string), and a single
  `MatchesChanged` event raised on every recompute and on session end.
- `Modes_Flash_FlashMode.fs` (new) — the `IFlashMode` implementation, shaped
  like `SubstituteConfirmMode` (`Modes_SubstituteConfirm_SubstituteConfirmMode.fs`).
  Registered in `Vim.fs` modeList (~line 302).
- `Tagger.fs` — `FlashLabelTag : IGlyphTag` (carries the label string),
  `FlashTaggerSource : IBasicTaggerSource<FlashLabelTag>` + MEF provider
  (`IViewTaggerProvider`), cloned from the `MarkGlyphTaggerSource` /
  `IncrementalSearchTaggerProvider` patterns. It subscribes to
  `IVimBuffer.FlashMode.MatchesChanged`.

Entry is Ex-command-only: `:Flash [-f|-F|-t|-T]` parses to
`LineCommand.Flash FlashKind` (`Interpreter_Parser.fs`), which the
interpreter turns into `ModeSwitch.SwitchModeWithArgument ModeKind.Flash`
with `ModeArgument.Flash`. There is no `NormalCommand` for flash; users bind
keys with `:nmap s :Flash<CR>`.

C# (`Src/VimWpf/`):

- `Implementation/FlashGlyph/` — `FlashGlyphFactoryProvider :
  IGlyphFactoryProvider` + `FlashGlyphFactory` rendering the label text with
  a colored background (clone of `MarkGlyph/MarkGlyphFactoryProvider.cs`).
  Label colors are hardcoded to `SystemColors.HighlightTextBrush` /
  `SystemColors.HighlightBrush` (no `EditorFormatDefinition`).

Tests:

- `Test/VimCoreTest/FlashModeTest.cs` — mode logic and label assignment.
- `FlashTaggerSourceTest.cs` modeled on `IncrementalSearchTaggerSourceTest.cs`.

### State machine

Entry: the user runs `:Flash [-f|-F|-t|-T]` (typically via a `:map` binding)
→ the interpreter switches to `ModeKind.Flash` via
`ModeSwitch.SwitchModeWithArgument` with a `ModeArgument.Flash` kind
distinguishing Search vs FindChar(forward/backward, till flag).

While active, `FlashMode.Process` per keystroke:

1. `<Esc>` — cancel: end the session, `SwitchMode ModeKind.Normal`.
2. Printable char — append to `SearchText` (FindChar: first key sets the
   target char), recompute, `Handled ModeSwitch.NoSwitch`.
3. `<BS>` — remove last char, recompute.
4. Char matching an assigned label — execute jump, end the session, switch
   to Normal. Label keys take precedence once the search text is non-empty.
5. `<CR>` — jump to the nearest match.

Recompute pipeline (synchronous, imperative):

- `TextViewUtil.GetVisibleSnapshotLineRange` → visible extent
  (`EditorUtil.fs:3069`).
- Scan for matches (case per `ignorecase`/`smartcase`).
- Order by distance from caret (both directions for Search; directional for
  FindChar).
- Assign labels greedily from the alphabet front; stability map keyed by
  position so a match keeps its label across narrows; matches beyond the
  alphabet get no label.
- Push via `IFlashMode.MatchesChanged`; `FlashTaggerSource` dirties affected
  spans → editor re-queries `GetTags` → glyph factory draws labels.

Jump: `ICommonOperations.MoveCaretToPoint` to match start (till variants
offset by one), then the session ends and labels disappear. If the buffer
changed mid-session (stale snapshot), the jump is abandoned: the session
ends and the mode switches back to Normal without moving the caret.

### Edge cases

- Zero matches → session stays active with no labels and no status message
  (v1 simplification); `<Esc>` or `<BS>` back to empty text continues the
  session.
- More matches than labels → unlabeled matches; narrowing reveals them.
- Buffer changes mid-session → jumps to stale-snapshot matches are abandoned
  (session ends without moving the caret).
- Caret already on a match → that match is excluded from labeling.
- `ModeKind` exhaustive matches to audit: `VimBuffer.fs` KeyRemapMode switch,
  `SelectionChangeTracker.fs`, command-margin/status display in
  VimWpf/VsVimShared. New kind shows "FLASH" status, `KeyRemapMode.None`.

### Known limitations

- Labels render in the editor's glyph margin (one per line) via the
  `IGlyphTag` mechanism, not inline over each match. Lines with multiple
  matches show all of that line's labels in the margin. An intra-text
  adornment layer that draws each label directly over its match is possible
  future work.

### Testing strategy

- `FlashModeTest.cs`: label ordering/stability, narrowing, jump, cancel,
  no-match, stale-snapshot jump guard, FindChar direction/till semantics,
  label-key precedence.
- `FlashTaggerSourceTest.cs`: tag lifecycle from `MatchesChanged` events.
- All run in the shared `Test/VimCoreTest/` project → VS2019/2022/2026.
