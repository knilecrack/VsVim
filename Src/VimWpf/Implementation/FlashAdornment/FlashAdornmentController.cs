using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.FlashAdornment
{
    /// <summary>
    /// Draws the jump labels of an active flash session directly over the
    /// matched text, flash.nvim style.  Driven entirely by the MatchesChanged
    /// event of the buffer's IFlashMode.
    /// </summary>
    internal sealed class FlashAdornmentController
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _layer;

        internal FlashAdornmentController(IVimBuffer vimBuffer, IWpfTextView textView, string adornmentLayerName)
        {
            _vimBuffer = vimBuffer;
            _textView = textView;
            _layer = _textView.GetAdornmentLayer(adornmentLayerName);

            _vimBuffer.FlashMode.MatchesChanged += OnMatchesChanged;
            _vimBuffer.Closed += OnBufferClosed;
        }

        private void OnBufferClosed(object sender, EventArgs e)
        {
            _vimBuffer.FlashMode.MatchesChanged -= OnMatchesChanged;
            _vimBuffer.Closed -= OnBufferClosed;
            _layer.RemoveAllAdornments();
        }

        private void OnMatchesChanged(object sender, EventArgs e)
        {
            _layer.RemoveAllAdornments();

            var snapshot = _textView.TextSnapshot;
            foreach (var flashMatch in _vimBuffer.FlashMode.Matches)
            {
                // The session computes matches on the snapshot which was
                // current at the keystroke; skip anything stale
                if (flashMatch.Span.Snapshot != snapshot)
                {
                    continue;
                }

                var element = CreateLabelElement(flashMatch.Label);
                _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, flashMatch.Span, null, element, null);
            }
        }

        private UIElement CreateLabelElement(string label)
        {
            var textProperties = _textView.FormattedLineSource.DefaultTextProperties;
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontFamily = textProperties.Typeface.FontFamily,
                    FontSize = textProperties.FontRenderingEmSize,
                    FontWeight = FontWeights.Bold,
                },
            };
        }
    }
}
