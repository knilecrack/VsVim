using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.IncrementalSearchTagName)]
    [UserVisible(true)]
    internal sealed class IncrementalSearchMarkerDefinition : MarkerFormatDefinition
    {
        internal IncrementalSearchMarkerDefinition()
        {
            DisplayName = "VsVim Incremental Search";
            BackgroundColor = Colors.LightBlue;
            ForegroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.HighlightIncrementalSearchTagName)]
    [UserVisible(true)]
    internal sealed class HighlightIncrementalSearchMarkerDefinition : MarkerFormatDefinition
    {
        internal HighlightIncrementalSearchMarkerDefinition()
        {
            DisplayName = "VsVim Highlight Incremental Search";
            BackgroundColor = Colors.LightBlue;
            ForegroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.YankTagName)]
    [UserVisible(true)]
    internal sealed class YankHighlightMarkerDefinition : MarkerFormatDefinition
    {
        internal YankHighlightMarkerDefinition()
        {
            DisplayName = "VsVim Yank Highlight";
            BackgroundColor = Color.FromRgb(255, 255, 150); // Light yellow
            ForegroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.FlashDimTagName)]
    [UserVisible(true)]
    internal sealed class FlashDimMarkerDefinition : MarkerFormatDefinition
    {
        internal FlashDimMarkerDefinition()
        {
            DisplayName = "VsVim Flash Dim";
            // Semi-transparent mid gray dims the text on both light and
            // dark themes while keeping a hint of the original colors
            ForegroundColor = Color.FromArgb(110, 128, 128, 128);
            BackgroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.FlashMatchTagName)]
    [UserVisible(true)]
    internal sealed class FlashMatchMarkerDefinition : MarkerFormatDefinition
    {
        internal FlashMatchMarkerDefinition()
        {
            DisplayName = "VsVim Flash Match";
            BackgroundColor = Color.FromRgb(255, 193, 99); // Warm highlight
            ForegroundCustomizable = false;
        }
    }
}
