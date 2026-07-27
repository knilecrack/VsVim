#light

namespace Vim

/// Tag carrying the label text for a flash match
type FlashLabelTag =
    new: chars: string -> FlashLabelTag
    member Chars: string
    interface Microsoft.VisualStudio.Text.Editor.IGlyphTag
