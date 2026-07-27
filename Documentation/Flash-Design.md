# Flash-Style Jump Navigation — Design

Status: implemented (2026-07-27). See `Documentation/Flash-Implementation-Plan.md` for the task-by-task implementation plan.

Implements flash.nvim-style jump navigation for VsVim: press a (user-mapped)
trigger, type 1-2 search chars, all matches in the visible window get inline
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

- `CoreInterfaces.fs` — add `ModeKind.Flash`; add `IFlashService` /
  `IFlashSession` interfaces (next to `IIncrementalSearch`, ~line 4517).
  Session exposes current `SearchText`, a `FlashMatch list` (span + label
  string), and `Updated` / `SessionComplete` events.
- `Modes_Flash_FlashMode.fs` (new) — the `IMode` implementation, shaped like
  `SubstituteConfirmMode` (`Modes_SubstituteConfirm_SubstituteConfirmMode.fs`).
  Registered in `Vim.fs` modeList (~line 296).
- `Tagger.fs` — `FlashLabelTag : IGlyphTag` (carries the label string),
  `FlashTaggerSource : IBasicTaggerSource<FlashLabelTag>` + MEF provider
  (`IViewTaggerProvider`), cloned from the `MarkGlyphTaggerSource` /
  `IncrementalSearchTaggerProvider` patterns.
- `Modes_Normal_NormalMode.fs` / `CommandUtil.fs` — new `NormalCommand`s
  (`FlashSearch`, `FlashFindChar` with direction/till flag) so the feature is
  bindable through the standard `:map` command machinery.

C# (`Src/VimWpf/`):

- `Implementation/FlashGlyph/` — `FlashGlyphFactoryProvider :
  IGlyphFactoryProvider` + `FlashGlyphFactory` rendering the label text with
  a colored background (clone of `MarkGlyph/MarkGlyphFactoryProvider.cs`),
  plus an `EditorFormatDefinition` for label colors.

Tests:

- `Test/VimCoreTest/FlashModeTest.fs` — mode logic and label assignment.
- `FlashTaggerSourceTest.cs` modeled on `IncrementalSearchTaggerSourceTest.cs`.

### State machine

Entry: mapped key triggers `NormalCommand.FlashSearch` (or `FlashFindChar`)
→ `CommandUtil` switches to `ModeKind.Flash` via
`ModeSwitch.SwitchModeWithArgument` with a `ModeArgument` distinguishing
Search vs FindChar(forward/backward, till flag).

While active, `FlashMode.Process` per keystroke:

1. `<Esc>` — cancel: session-complete, `SwitchMode ModeKind.Normal`.
2. Printable char — append to `SearchText` (FindChar: first key sets the
   target char), recompute, `HandledNeedMoreInput`.
3. `<BS>` — remove last char, recompute.
4. Char matching an assigned label — execute jump, session-complete, switch
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
- Push via `IFlashSession.Updated`; `FlashTaggerSource` dirties affected
  spans → editor re-queries `GetTags` → glyph factory draws labels.

Jump: `ICommonOperations.MoveCaretToPoint` to match start (till variants
offset by one), then the session ends and labels disappear.

### Edge cases

- Zero matches → `IStatusUtil.OnError` message; stay in mode until `<Esc>`.
- More matches than labels → unlabeled matches; narrowing reveals them.
- Buffer changes mid-session → `ITrackingSpan` tracking like other taggers.
- Caret already on a match → that match is excluded from labeling.
- `ModeKind` exhaustive matches to audit: `VimBuffer.fs` KeyRemapMode switch,
  `SelectionChangeTracker.fs`, command-margin/status display in
  VimWpf/VsVimShared. New kind shows "FLASH" status, `KeyRemapMode.None`.

### Testing strategy

- `FlashModeTest.fs`: label ordering/stability, narrowing, jump, cancel,
  no-match, FindChar direction/till semantics, label-key precedence.
- `FlashTaggerSourceTest.cs`: tag lifecycle from session events.
- All run in the shared `Test/VimCoreTest/` project → VS2019/2022/2026.
