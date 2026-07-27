using System;
using System.Linq;
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

        [WpfFact]
        public void Labels_AssignedByDistanceFromCaret()
        {
            Create("x a x", "x a x");
            _textView.Caret.MoveTo(_textBuffer.GetLine(0).Start);
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('x');
            // Caret is on the first 'x'; nearest remaining match gets "a".
            Assert.Equal("a", _mode.Matches[0].Label);
            Assert.Equal("s", _mode.Matches[1].Label);
        }

        [WpfFact]
        public void Labels_StableWhileNarrowing()
        {
            Create("ab ac ad");
            // Move the caret off the "ab" match at position 0 so it is not
            // excluded, but keep it close so "ab" is ordered first.
            _textView.Caret.MoveTo(_textBuffer.GetPoint(1));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('a');
            var firstLabels = _mode.Matches.Select(m => Tuple.Create(m.Span.Start.Position, m.Label)).ToList();
            _mode.Process('b');
            Assert.Single(_mode.Matches);
            Assert.Equal(firstLabels[0].Item2, _mode.Matches[0].Label);
        }

        [WpfFact]
        public void Labels_CappedByAlphabet()
        {
            // 30 matches, 26 labels: 4 matches get no label and are dropped
            // from the labeled list.
            var line = string.Join(" ", Enumerable.Repeat("q", 30));
            Create(line);
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('q');
            Assert.True(_mode.Matches.Length <= 26);
        }
    }
}
