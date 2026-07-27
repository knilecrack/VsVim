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
