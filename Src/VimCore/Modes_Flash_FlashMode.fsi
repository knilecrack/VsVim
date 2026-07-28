#light

namespace Vim.Modes.Flash
open Vim
open Vim.Modes

type internal FlashMode =
    new: IVimBufferData * ICommonOperations -> FlashMode

    interface IFlashMode
