using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.Implementation.FlashAdornment
{
    /// <summary>
    /// While a flash session is active, dims the non-matching visible text
    /// with semi-transparent rectangles and draws the jump labels directly
    /// over the matched text, flash.nvim style.  Driven by the MatchesChanged
    /// and SwitchedMode events of the owning IVimBuffer.
    /// </summary>
    internal sealed class FlashAdornmentController
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IWpfTextView _textView;
        private readonly IAdornmentLayer _layer;
        private readonly Brush _dimBrush;

        internal FlashAdornmentController(IVimBuffer vimBuffer, IWpfTextView textView, string adornmentLayerName)
        {
            _vimBuffer = vimBuffer;
            _textView = textView;
            _layer = _textView.GetAdornmentLayer(adornmentLayerName);
            if (_layer == null)
            {
                VimTrace.TraceError($"FlashAdornment: GetAdornmentLayer('{adornmentLayerName}') returned null");
            }

            _dimBrush = CreateDimBrush();

            _vimBuffer.FlashMode.MatchesChanged += OnUpdate;
            _vimBuffer.SwitchedMode += OnSwitchedMode;
            _vimBuffer.Closed += OnBufferClosed;
        }

        /// <summary>
        /// The editor background color at partial opacity: covers syntax
        /// colors with a wash of the background, which reads as dimming on
        /// any theme
        /// </summary>
        private Brush CreateDimBrush()
        {
            var background = _textView.FormattedLineSource?.DefaultTextProperties?.BackgroundBrush as SolidColorBrush;
            var color = background != null ? background.Color : Colors.Black;
            var brush = new SolidColorBrush(Color.FromArgb(170, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        private void OnBufferClosed(object sender, EventArgs e)
        {
            _vimBuffer.FlashMode.MatchesChanged -= OnUpdate;
            _vimBuffer.SwitchedMode -= OnSwitchedMode;
            _vimBuffer.Closed -= OnBufferClosed;
            _layer?.RemoveAllAdornments();
        }

        private void OnSwitchedMode(object sender, EventArgs e)
        {
            OnUpdate(sender, e);
        }

        private void OnUpdate(object sender, EventArgs e)
        {
            try
            {
                if (_layer == null)
                {
                    return;
                }

                _layer.RemoveAllAdornments();

                if (_vimBuffer.ModeKind != ModeKind.Flash || _textView.TextViewLines == null)
                {
                    return;
                }

                var snapshot = _textView.TextSnapshot;
                var matches = _vimBuffer.FlashMode.Matches
                    .Where(flashMatch => flashMatch.Span.Snapshot == snapshot)
                    .ToList();

                AddDimRectangles(snapshot, matches);
                AddLabels(snapshot, matches);
            }
            catch (Exception ex)
            {
                VimTrace.TraceError(ex);
            }
        }

        /// <summary>
        /// Cover every segment of the visible lines which is not a match
        /// with a semi-transparent rectangle
        /// </summary>
        private void AddDimRectangles(ITextSnapshot snapshot, List<FlashMatch> matches)
        {
            foreach (var textViewLine in _textView.TextViewLines)
            {
                var lineExtent = textViewLine.ExtentIncludingLineBreak;
                var current = lineExtent.Start.Position;
                var matchSpans = matches
                    .Select(flashMatch => flashMatch.Span)
                    .Where(span => span.Start.Position >= lineExtent.Start.Position && span.Start.Position <= lineExtent.End.Position)
                    .OrderBy(span => span.Start.Position);

                foreach (var span in matchSpans)
                {
                    if (span.Start.Position > current)
                    {
                        AddDimRectangle(new SnapshotSpan(new SnapshotPoint(snapshot, current), span.Start));
                    }

                    current = Math.Max(current, span.End.Position);
                }

                if (lineExtent.End.Position > current)
                {
                    AddDimRectangle(new SnapshotSpan(new SnapshotPoint(snapshot, current), lineExtent.End));
                }
            }
        }

        private void AddDimRectangle(SnapshotSpan span)
        {
            var geometry = _textView.TextViewLines.GetTextMarkerGeometry(span);
            if (geometry == null)
            {
                return;
            }

            var rectangle = new Border
            {
                Background = _dimBrush,
                IsHitTestVisible = false,
                Width = geometry.Bounds.Width,
                Height = geometry.Bounds.Height,
            };
            Canvas.SetLeft(rectangle, geometry.Bounds.Left);
            Canvas.SetTop(rectangle, geometry.Bounds.Top);

            _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, rectangle, null);
        }

        private void AddLabels(ITextSnapshot snapshot, List<FlashMatch> matches)
        {
            foreach (var flashMatch in matches)
            {
                // Position the label explicitly on the match's first
                // character (the PeasyMotion approach): automatic adornment
                // positioning does not render reliably in all hosts
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
                Panel.SetZIndex(element, 1);

                _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, firstCharSpan, null, element, null);
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
