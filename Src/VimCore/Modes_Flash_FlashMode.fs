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
