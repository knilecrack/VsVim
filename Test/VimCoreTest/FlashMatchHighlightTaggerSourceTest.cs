using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Vim.EditorHost;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class FlashMatchHighlightTaggerSourceTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private FlashMatchHighlightTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<TextMarkerTag> _taggerSource;

        private void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textView.DisplayTextLineContainingBufferPosition(
                _textView.TextBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
            ((IWpfTextView)_textView).SetVisibleLineCount(lines.Length);
            _taggerSourceRaw = new FlashMatchHighlightTaggerSource(_vimBuffer);
            _taggerSource = _taggerSourceRaw;
        }

        private ITagSpan<TextMarkerTag>[] GetTags()
        {
            return _taggerSource.GetTags(_textView.TextSnapshot.GetExtent()).ToArray();
        }

        [WpfFact]
        public void NoSession_NoTags()
        {
            Create("cat", "dog");
            Assert.Empty(GetTags());
        }

        [WpfFact]
        public void SessionStart_NoMatchTags()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            Assert.Empty(GetTags());
        }

        [WpfFact]
        public void TypeChar_MatchesHighlighted()
        {
            Create("cat", "dog", "cat");
            _textView.MoveCaretToLine(1);
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');

            // Both 'c' matches are highlighted and sit at position 0 of
            // lines 0 and 2
            var tags = GetTags();
            Assert.Equal(2, tags.Length);
            Assert.All(tags, tag => Assert.Equal(VimConstants.FlashMatchTagName, tag.Tag.Type));
            Assert.All(tags, tag => Assert.Equal(1, tag.Span.Length));
            Assert.All(tags, tag => Assert.Equal(tag.Span.Start.GetContainingLine().Start, tag.Span.Start));
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
