using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Vim.EditorHost;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class FlashTaggerSourceTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private ITextView _textView;
        private FlashTaggerSource _taggerSourceRaw;
        private IBasicTaggerSource<FlashLabelTag> _taggerSource;

        private void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = _vimBuffer.TextView;
            _textView.DisplayTextLineContainingBufferPosition(
                _textView.TextBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
            ((IWpfTextView)_textView).SetVisibleLineCount(lines.Length);
            _taggerSourceRaw = new FlashTaggerSource(_vimBuffer);
            _taggerSource = _taggerSourceRaw;
        }

        private ITagSpan<FlashLabelTag>[] GetTags()
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
        public void Session_ProducesLabelTags()
        {
            // The char after the first match ("cot") is 'o' so the nearest
            // match gets label "a"; "cat" would skip "a" (kept free for
            // narrowing the search)
            Create("cot", "dog", "cat");
            _textView.MoveCaretToLine(1);
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            var tags = GetTags();
            Assert.Equal(2, tags.Length);
            Assert.Equal("a", tags[0].Tag.Chars);
        }

        [WpfFact]
        public void SessionEnd_ClearsTags()
        {
            Create("cat", "dog", "cat");
            _textView.MoveCaretToLine(1);
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            Assert.NotEmpty(GetTags());
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.Empty(GetTags());
        }
    }
}
