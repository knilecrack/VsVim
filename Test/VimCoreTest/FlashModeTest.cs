using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.EditorHost;
using Vim.Modes.Flash;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class FlashModeTest : VimTestBase
    {
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private Mock<ICommonOperations> _operations;
        private FlashMode _modeRaw;
        private IFlashMode _mode;

        private void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _operations = new Mock<ICommonOperations>(MockBehavior.Loose);
            var vimBufferData = CreateVimBufferData(_textView);
            _modeRaw = new FlashMode(vimBufferData, _operations.Object);
            _mode = _modeRaw;
            _textView.DisplayTextLineContainingBufferPosition(
                _textBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
            _textView.SetVisibleLineCount(lines.Length);
        }

        [WpfFact]
        public void ModeKind_IsFlash()
        {
            Create("cat", "dog");
            Assert.Equal(ModeKind.Flash, _mode.ModeKind);
        }

        [WpfFact]
        public void Escape_SwitchesToNormal()
        {
            Create("cat", "dog");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            Assert.True(_mode.Process(KeyInputUtil.EscapeKey).IsSwitchMode(ModeKind.Normal));
        }

        [WpfFact]
        public void TypeChar_FindsMatchesInVisibleText()
        {
            Create("cat", "dog", "cat");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(_textBuffer.CurrentSnapshot.Length));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            Assert.Equal("c", _mode.SearchText);
            Assert.Equal(2, _mode.Matches.Length);
        }

        [WpfFact]
        public void TypeChar_ExcludesMatchAtCaret()
        {
            Create("cat cat");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(0));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            Assert.Single(_mode.Matches);
        }

        [WpfFact]
        public void MatchesChanged_RaisedOnType()
        {
            Create("cat", "dog");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            var count = 0;
            _mode.MatchesChanged += (sender, args) => count++;
            _mode.Process('c');
            Assert.Equal(1, count);
        }

        [WpfFact]
        public void IgnoreCase_MatchesUpperAndLower()
        {
            Create("Cat cat");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(_textBuffer.CurrentSnapshot.Length));
            Vim.GlobalSettings.IgnoreCase = true;
            Vim.GlobalSettings.SmartCase = false;
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            Assert.Equal(2, _mode.Matches.Length);
            _mode.Process('a');
            Assert.Equal(2, _mode.Matches.Length);
        }

        [WpfFact]
        public void SmartCase_UppercasePatternIsCaseSensitive()
        {
            Create("Cat cat");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(_textBuffer.CurrentSnapshot.Length));
            Vim.GlobalSettings.IgnoreCase = true;
            Vim.GlobalSettings.SmartCase = true;
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('C');
            Assert.Single(_mode.Matches);
        }
    }
}
