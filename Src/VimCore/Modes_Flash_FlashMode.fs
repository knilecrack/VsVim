#light

namespace Vim.Modes.Flash
open System
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
