using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Vim.EditorHost;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class FlashDimTaggerSourceTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private FlashDimTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<TextMarkerTag> _taggerSource;

        private void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textView.DisplayTextLineContainingBufferPosition(
                _textView.TextBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
            ((IWpfTextView)_textView).SetVisibleLineCount(lines.Length);
            _taggerSourceRaw = new FlashDimTaggerSource(_vimBuffer);
            _taggerSource = _taggerSourceRaw;
        }

        private ITagSpan<TextMarkerTag>[] GetTags()
        {
            return _taggerSource.GetTags(_textView.TextSnapshot.GetExtent()).ToArray();
        }

        private ITagSpan<TextMarkerTag>[] GetDimTags()
        {
            return GetTags().Where(tag => tag.Tag.Type == VimConstants.FlashDimTagName).ToArray();
        }

        private ITagSpan<TextMarkerTag>[] GetMatchTags()
        {
            return GetTags().Where(tag => tag.Tag.Type == VimConstants.FlashMatchTagName).ToArray();
        }

        [WpfFact]
        public void NoSession_NoTags()
        {
            Create("cat", "dog");
            Assert.Empty(GetTags());
        }

        [WpfFact]
        public void SessionStart_DimsVisibleText()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            Assert.NotEmpty(GetDimTags());
            Assert.Empty(GetMatchTags());
        }

        [WpfFact]
        public void TypeChar_DimCoversGapsAndMatchesHighlighted()
        {
            Create("cat", "dog", "cat");
            _textView.MoveCaretToLine(1);
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');

            // Both 'c' matches are highlighted and sit at position 0 of
            // lines 0 and 2
            var matchTags = GetMatchTags();
            Assert.Equal(2, matchTags.Length);
            Assert.All(matchTags, tag => Assert.Equal(1, tag.Span.Length));
            Assert.All(matchTags, tag => Assert.Equal(tag.Span.Start.GetContainingLine().Start, tag.Span.Start));

            // The dim covers the gaps, so it must not overlap any match
            var dimTags = GetDimTags();
            Assert.NotEmpty(dimTags);
            Assert.All(dimTags, dim =>
                Assert.All(matchTags, match =>
                    Assert.False(dim.Span.OverlapsWith(match.Span))));

            // Dim + match coverage must be continuous over the visible text:
            // every line is covered by some tag
            var coveredLines = GetTags()
                .SelectMany(tag => Enumerable.Range(
                    tag.Span.Start.GetContainingLine().LineNumber,
                    tag.Span.End.GetContainingLine().LineNumber - tag.Span.Start.GetContainingLine().LineNumber + 1))
                .Distinct()
                .OrderBy(lineNumber => lineNumber)
                .ToArray();
            Assert.Equal(new[] { 0, 1, 2 }, coveredLines);
        }

        [WpfFact]
        public void SessionEnd_ClearsTags()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            Assert.NotEmpty(GetTags());
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.Empty(GetTags());
        }
    }
}
