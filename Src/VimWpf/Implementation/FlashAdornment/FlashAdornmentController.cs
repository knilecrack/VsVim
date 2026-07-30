using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
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
            if (_layer == null)
            {
                VimTrace.TraceError($"FlashAdornment: GetAdornmentLayer('{adornmentLayerName}') returned null");
            }

            _vimBuffer.FlashMode.MatchesChanged += OnMatchesChanged;
            _vimBuffer.Closed += OnBufferClosed;
        }

        private void OnBufferClosed(object sender, EventArgs e)
        {
            _vimBuffer.FlashMode.MatchesChanged -= OnMatchesChanged;
            _vimBuffer.Closed -= OnBufferClosed;
            _layer?.RemoveAllAdornments();
        }

        private void OnMatchesChanged(object sender, EventArgs e)
        {
            try
            {
                if (_layer == null)
                {
                    return;
                }

                _layer.RemoveAllAdornments();

                var snapshot = _textView.TextSnapshot;
                var matches = _vimBuffer.FlashMode.Matches;
                var added = 0;
                foreach (var flashMatch in matches)
                {
                    // The session computes matches on the snapshot which was
                    // current at the keystroke; skip anything stale
                    if (flashMatch.Span.Snapshot != snapshot)
                    {
                        continue;
                    }

                    // Position the label explicitly on the match's first
                    // character (the PeasyMotion approach): automatic
                    // adornment positioning does not render reliably in
                    // all hosts
                    var firstCharSpan = new SnapshotSpan(
                        flashMatch.Span.Start,
                        Math.Min(1, snapshot.Length - flashMatch.Span.Start.Position));
                    var geometry = _textView.TextViewLines.GetTextMarkerGeometry(firstCharSpan);
                    if (geometry == null)
                    {
                        continue;
                    }

                    var element = CreateLabelElement(flashMatch.Label);
                    var border = (Border)element;
                    Canvas.SetLeft(element, geometry.Bounds.Left - border.Padding.Left);
                    Canvas.SetTop(element, geometry.Bounds.Top - border.Padding.Top);

                    if (_layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, firstCharSpan, null, element, null))
                    {
                        added++;
                    }
                    else
                    {
                        VimTrace.TraceInfo($"FlashAdornment: AddAdornment returned false for label '{flashMatch.Label}' at {flashMatch.Span.Start.Position}");
                    }
                }

                VimTrace.TraceInfo($"FlashAdornment: {matches.Length} matches, {added} adornments added");
            }
            catch (Exception ex)
            {
                VimTrace.TraceError(ex);
            }
        }

        private UIElement CreateLabelElement(string label)
        {
            var textProperties = _textView.FormattedLineSource?.DefaultTextProperties;
            var fontSize = textProperties != null ? textProperties.FontRenderingEmSize : 14.0;
            if (fontSize <= 0)
            {
                fontSize = 14.0;
            }

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                IsHitTestVisible = false,
                Padding = new Thickness(2, 0, 2, 0),
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontFamily = textProperties != null ? textProperties.Typeface.FontFamily : new FontFamily("Consolas"),
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                },
            };
        }
    }
}
