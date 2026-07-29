using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.EditorHost;
using Vim.Extensions;
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
        public void TypeLabel_JumpsAndSwitchesToNormal()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            var target = _mode.Matches[0];
            var result = _mode.Process(target.Label[0]);
            Assert.True(result.IsSwitchMode(ModeKind.Normal));
            _operations.Verify(x => x.MoveCaretToPoint(target.Span.Start, ViewFlags.Standard), Times.Once);
            Assert.Empty(_mode.Matches);
        }

        [WpfFact]
        public void Enter_JumpsToNearestMatch()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            var target = _mode.Matches[0];
            var result = _mode.Process(KeyInputUtil.EnterKey);
            Assert.True(result.IsSwitchMode(ModeKind.Normal));
            _operations.Verify(x => x.MoveCaretToPoint(target.Span.Start, ViewFlags.Standard), Times.Once);
        }

        [WpfFact]
        public void Backspace_ShrinksSearch()
        {
            Create("cab", "cat");
            // Move the caret off position 0 so the "cab" match is not excluded.
            _textView.Caret.MoveTo(_textBuffer.GetPoint(_textBuffer.CurrentSnapshot.Length));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            _mode.Process('a');
            Assert.Equal(2, _mode.Matches.Length);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
            Assert.Equal("c", _mode.SearchText);
        }

        [WpfFact]
        public void Labels_RemainDistinctAfterCaretMove()
        {
            Create("xy xy xy");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(0));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('x');
            _mode.Process('y');
            // Moving the caret reorders the matches by distance; a fresh match
            // can then be ordered before a stable-labeled one and must not
            // steal its label.
            _textView.Caret.MoveTo(_textBuffer.GetPoint(1));
            _mode.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
            var labels = _mode.Matches.Select(m => m.Label).ToList();
            Assert.Equal(labels.Count, labels.Distinct().Count());
        }

        [WpfFact]
        public void NoMatches_StaysInModeWithEmptyLabels()
        {
            Create("cat", "dog");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            var result = _mode.Process('z');
            Assert.Empty(_mode.Matches);
            Assert.Equal("z", _mode.SearchText);
            Assert.True(result.IsHandledNoSwitch());
            // Still in flash mode: Escape is handled and switches to Normal
            Assert.True(_mode.Process(KeyInputUtil.EscapeKey).IsSwitchMode(ModeKind.Normal));
        }

        [WpfFact]
        public void Labels_RemainDistinctAfterBackspaceWiden()
        {
            Create("ab ac ad ae");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(_textBuffer.CurrentSnapshot.Length));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('a');
            Assert.Equal(4, _mode.Matches.Length);
            _mode.Process('b');
            Assert.Single(_mode.Matches);
            _mode.Process(KeyNotationUtil.StringToKeyInput("<BS>"));
            Assert.Equal(4, _mode.Matches.Length);
            var labels = _mode.Matches.Select(m => m.Label).ToList();
            Assert.Equal(labels.Count, labels.Distinct().Count());
        }

        [WpfFact]
        public void Jump_StaleSnapshot_EndsSessionWithoutMoving()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            var target = _mode.Matches[0];
            // An external edit invalidates the snapshot the matches were
            // computed against; the jump must not move the caret
            _textBuffer.Replace(new Span(0, 0), "insert ");
            var result = _mode.Process(target.Label[0]);
            Assert.True(result.IsSwitchMode(ModeKind.Normal));
            _operations.Verify(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), It.IsAny<ViewFlags>()), Times.Never);
            Assert.Empty(_mode.Matches);
        }

        [WpfFact]
        public void TillCharBackward_JumpsAfterMatch()
        {
            Create("x a x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(4));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.TillCharBackward));
            _mode.Process('x');
            var target = _mode.Matches[0];
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), ViewFlags.Standard));
            _mode.Process(target.Label[0]);
            _operations.Verify(x => x.MoveCaretToPoint(
                It.Is<SnapshotPoint>(p => p.Position == target.Span.Start.Position + 1), ViewFlags.Standard), Times.Once);
        }

        [WpfFact]
        public void Labels_CappedByAlphabet()
        {
            // 30 matches, 26 labels: 4 matches get no label and are dropped
            // from the labeled list.  The caret sits on the first match so it
            // is excluded, leaving 29 candidates for the 26 labels.
            var line = string.Join(" ", Enumerable.Repeat("q", 30));
            Create(line);
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('q');
            Assert.Equal(26, _mode.Matches.Length);
        }

        [WpfFact]
        public void FindCharForward_OnlyForwardMatches()
        {
            Create("x mid x", "x end x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(2));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharForward));
            _mode.Process('x');
            Assert.All(_mode.Matches, m => Assert.True(m.Span.Start.Position > 2));
        }

        [WpfFact]
        public void FindCharBackward_OnlyBackwardMatches()
        {
            Create("x mid x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(6));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharBackward));
            _mode.Process('x');
            Assert.Single(_mode.Matches);
            Assert.Equal(0, _mode.Matches[0].Span.Start.Position);
        }

        [WpfFact]
        public void FindChar_ExtraCharsAfterTargetIgnored()
        {
            Create("x a x");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharForward));
            _mode.Process('x');
            var count = _mode.Matches.Length;
            _mode.Process('z'); // not a label, not the first char: ignored
            Assert.Equal(count, _mode.Matches.Length);
        }

        [WpfFact]
        public void TillCharForward_JumpsBeforeMatch()
        {
            Create("a x b x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(0));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.TillCharForward));
            _mode.Process('x');
            var target = _mode.Matches[0];
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), ViewFlags.Standard));
            _mode.Process(target.Label[0]);
            _operations.Verify(x => x.MoveCaretToPoint(
                It.Is<SnapshotPoint>(p => p.Position == target.Span.Start.Position - 1), ViewFlags.Standard), Times.Once);
        }

        [WpfFact]
        public void SearchJump_SetsLastSearchData()
        {
            Create("cat", "dog", "cat");
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            var target = _mode.Matches[0];
            _mode.Process(target.Label[0]);
            Assert.Equal("c", VimData.LastSearchData.Pattern);
        }

        [WpfFact]
        public void FindCharJump_SetsLastCharSearch()
        {
            Create("x mid x", "x end x");
            _textView.Caret.MoveTo(_textBuffer.GetPoint(2));
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.FindCharForward));
            _mode.Process('x');
            var target = _mode.Matches[0];
            _mode.Process(target.Label[0]);
            var lastCharSearch = VimData.LastCharSearch;
            Assert.True(lastCharSearch.IsSome());
            Assert.Equal(CharSearchKind.ToChar, lastCharSearch.Value.Item1);
            Assert.Equal(SearchPath.Forward, lastCharSearch.Value.Item2);
            Assert.Equal('x', lastCharSearch.Value.Item3);
        }

        [WpfFact]
        public void Escape_DoesNotSetSearchState()
        {
            Create("cat", "dog");
            var lastSearchPattern = VimData.LastSearchData.Pattern;
            var lastCharSearch = VimData.LastCharSearch;
            _mode.OnEnter(VimUtil.CreateFlashArgument(FlashKind.Search));
            _mode.Process('c');
            _mode.Process(KeyInputUtil.EscapeKey);
            Assert.Equal(lastSearchPattern, VimData.LastSearchData.Pattern);
            Assert.Equal(lastCharSearch, VimData.LastCharSearch);
        }
    }
}
