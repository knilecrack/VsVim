using System;
using System.Linq;
using Vim.EditorHost;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

namespace Vim.UnitTest
{
    public abstract class MotionUtilTest : VimTestBase
    {
        protected ITextBuffer _textBuffer;
        protected IWpfTextView _textView;
        protected ITextSnapshot _snapshot;
        protected IVimBufferData _vimBufferData;
        protected IVimTextBuffer _vimTextBuffer;
        protected IVimLocalSettings _localSettings;
        protected IVimGlobalSettings _globalSettings;
        internal MotionUtil _motionUtil;
        protected ISearchService _search;
        protected IVimData _vimData;
        protected IMarkMap _markMap;
        protected Mock<IStatusUtil> _statusUtil;
        protected LocalMark _localMarkA = LocalMark.NewLetter(Letter.A);
        protected Mark _markLocalA = Mark.NewLocalMark(LocalMark.NewLetter(Letter.A));

        protected virtual void Create(params string[] lines)
        {
            var textView = CreateTextView(lines);
            Create(textView);
        }

        protected void Create(int caretPosition, params string[] lines)
        {
            Create(lines);
            _textView.MoveCaretTo(caretPosition);
        }

        protected void Create(IWpfTextView textView)
        {
            _textView = textView;
            _textBuffer = textView.TextBuffer;
            _snapshot = _textBuffer.CurrentSnapshot;
            _textBuffer.Changed += delegate { _snapshot = _textBuffer.CurrentSnapshot; };

            _vimTextBuffer = Vim.CreateVimTextBuffer(_textBuffer);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _vimBufferData = CreateVimBufferData(_vimTextBuffer, _textView, statusUtil: _statusUtil.Object);
            _globalSettings = _vimBufferData.LocalSettings.GlobalSettings;
            _localSettings = _vimBufferData.LocalSettings;
            _markMap = _vimBufferData.Vim.MarkMap;
            _vimData = _vimBufferData.Vim.VimData;
            _search = _vimBufferData.Vim.SearchService;
            var operations = CommonOperationsFactory.GetCommonOperations(_vimBufferData);
            _motionUtil = new MotionUtil(_vimBufferData, operations);
        }

        public void AssertData(MotionResult data, SnapshotSpan? span, MotionKind motionKind = null, bool? isForward = null, CaretColumn caretColumn = null)
        {
            if (span != null)
            {
                Assert.Equal(span.Value, data.Span);
            }
            if (isForward != null)
            {
                Assert.Equal(isForward.Value, data.IsForward);
            }
            if (motionKind != null)
            {
                Assert.Equal(motionKind, data.MotionKind);
            }
            if (caretColumn != null)
            {
                Assert.Equal(caretColumn, data.CaretColumn);
            }
        }

        public void AssertData(FSharpOption<MotionResult> data, SnapshotSpan? span = null, MotionKind motionKind = null, bool? isForward = null, CaretColumn caretColumn = null)
        {
            Assert.True(data.IsSome());
            AssertData(data.value, span, motionKind, isForward, caretColumn);
        }

        public sealed class AdjustMotionResult : MotionUtilTest
        {
            /// <summary>
            /// Inclusive motion values shouldn't be adjusted
            /// </summary>
            [WpfFact]
            public void Inclusive()
            {
                Create("cat", "dog");
                var result1 = VimUtil.CreateMotionResult(_textView.GetLine(0).ExtentIncludingLineBreak, motionKind: MotionKind.CharacterWiseInclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(result1, result2);
            }

            /// <summary>
            /// Make sure adjusted ones become line wise if it meets the criteria
            /// </summary>
            [WpfFact]
            public void FullLine()
            {
                Create("  cat", "dog");
                var span = new SnapshotSpan(_textView.GetPoint(2), _textView.GetLine(1).Start);
                var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(OperationKind.LineWise, result2.OperationKind);
                Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak, result2.Span);
                Assert.Equal(span, result2.SpanBeforeExclusivePromotion.Value);
                Assert.True(result2.MotionKind.IsLineWise);
            }

            /// <summary>
            /// Don't make it full line if it doesn't start before the first real character
            /// </summary>
            [WpfFact]
            public void NotFullLine()
            {
                Create("  cat", "dog");
                var span = new SnapshotSpan(_textView.GetPoint(3), _textView.GetLine(1).Start);
                var result1 = VimUtil.CreateMotionResult(span, motionKind: MotionKind.CharacterWiseExclusive);
                var result2 = _motionUtil.AdjustMotionResult(Motion.CharLeft, result1);
                Assert.Equal(OperationKind.CharacterWise, result2.OperationKind);
                Assert.Equal("at", result2.Span.GetText());
                Assert.True(result2.MotionKind.IsCharacterWiseInclusive);
            }

            /// <summary>
            /// If the last word on the line is yanked and there is a space after it then the
            /// space should be included (basically the exclusive adjustment shouldn't leave
            /// it in)
            /// </summary>
            [WpfFact]
            public void WordWithSpaceBeforeEndOfLine()
            {
                Create("cat ", " dog");
                var motionResult = _motionUtil.GetMotion(Motion.NewWordForward(WordKind.NormalWord)).Value;
                Assert.Equal("cat ", motionResult.Span.GetText());
            }
        }

        public sealed class AllSentenceTest : MotionUtilTest
        {
            /// <summary>
            /// Should take the trailing white space
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                Create("dog. cat. bear.");
                var data = _motionUtil.AllSentence(1);
                Assert.Equal("dog. ", data.Span.GetText());
            }

            /// <summary>
            /// Take the leading white space when there is a preceding sentence and no trailing 
            /// white space
            /// </summary>
            [WpfFact]
            public void NoTrailingWhiteSpace()
            {
                Create("dog. cat.");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.AllSentence(1);
                Assert.Equal(" cat.", data.Span.GetText());
            }

            /// <summary>
            /// When starting in the white space include it in the motion instead of the trailing
            /// white space
            /// </summary>
            [WpfFact]
            public void FromWhiteSpace()
            {
                Create("dog. cat. bear.");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.AllSentence(1);
                Assert.Equal(" cat.", data.Span.GetText());
            }

            /// <summary>
            /// When the trailing white space goes across new lines then we should still be including
            /// that 
            /// </summary>
            [WpfFact]
            public void WhiteSpaceAcrossNewLine()
            {
                Create("dog.  ", "  cat");
                var data = _motionUtil.AllSentence(1);
                Assert.Equal("dog.  " + Environment.NewLine + "  ", data.Span.GetText());
            }

            /// <summary>
            /// This is intended to make sure that 'as' goes through the standard exclusive adjustment
            /// operation.  Even though it technically extends into the next line ':help exclusive-linewise'
            /// dictates it should be changed into a line wise motion
            /// </summary>
            [WpfFact]
            public void OneSentencePerLine()
            {
                Create("dog.", "cat.");
                var data = _motionUtil.GetMotion(Motion.AllSentence).Value;
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
                Assert.Equal("dog." + Environment.NewLine, data.Span.GetText());
            }

            /// <summary>
            /// Blank lines are sentences so don't include them as white space.  The gap between the '.'
            /// and the blank line is white space though and should be included in the motion
            /// </summary>
            [WpfFact]
            public void DontJumpBlankLinesAsWhiteSpace()
            {
                Create("dog.", "", "cat.");
                var data = _motionUtil.GetMotion(Motion.AllSentence).Value;
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
                Assert.Equal("dog." + Environment.NewLine, data.Span.GetText());
            }

            /// <summary>
            /// Blank lines are sentences so treat them as such.  Note: A blank line includes the entire blank
            /// line.  But when operating on a blank line it often appears that it doesn't due to the 
            /// rules around motion adjustments spelled out in ':help exclusive'.  Namely if an exclusive motion
            /// ends in column 0 then it gets moved back to the end of the previous line and becomes inclusive
            /// </summary>
            [WpfFact]
            public void BlankLinesAreSentences()
            {
                Create("dog.  ", "", "cat.");
                _textView.MoveCaretToLine(1);
                var data = _motionUtil.AllSentence(1);
                Assert.Equal("  " + Environment.NewLine + Environment.NewLine, data.Span.GetText());

                // Make sure it's adjusted properly for the exclusive exception
                data = _motionUtil.GetMotion(Motion.AllSentence).Value;
                Assert.Equal("  " + Environment.NewLine, data.Span.GetText());
            }
        }

        public sealed class ForcedCharacterWiseTest : MotionUtilTest
        {
            [WpfFact]
            public void LineDown()
            {
                Create("the", "dog");
                AssertData(
                    _motionUtil.ForceCharacterWise(Motion.LineDown, new MotionArgument(MotionContext.AfterOperator)),
                    span: _textBuffer.GetLine(0).ExtentIncludingLineBreak,
                    motionKind: MotionKind.CharacterWiseExclusive);
            }

            [WpfFact]
            public void FlipExclusiveToInclusive()
            {
                Create("dog");
                AssertData(
                    _motionUtil.ForceCharacterWise(Motion.CharRight, new MotionArgument(MotionContext.AfterOperator)),
                    span: _textBuffer.GetLineSpan(lineNumber: 0, length: 2),
                    motionKind: MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void FlipInclusiveToExclusive()
            {
                Create("dog");
                AssertData(
                    _motionUtil.ForceCharacterWise(Motion.NewCharSearch(CharSearchKind.ToChar, SearchPath.Forward, 'o'), new MotionArgument(MotionContext.AfterOperator)),
                    span: _textBuffer.GetLineSpan(lineNumber: 0, length: 1),
                    motionKind: MotionKind.CharacterWiseExclusive);
            }
        }

        public sealed class GetBlockTest : MotionUtilTest
        {
            private SnapshotSpan GetBlockSpan(BlockKind blockKind, SnapshotPoint point)
            {
                var option = _motionUtil.GetBlock(blockKind, point);
                Assert.True(option.IsSome());
                var tuple = option.Value;
                return new SnapshotSpan(tuple.Item1, tuple.Item2.Add(1));
            }

            /// <summary>
            /// Simple matched bracket test
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                Create("[cat] dog");
                var span = GetBlockSpan(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.Equal(_textBuffer.GetSpan(0, 5), span);
            }

            /// <summary>
            /// Simple matched bracket test from the middle
            /// </summary>
            [WpfFact]
            public void Simple_FromMiddle()
            {
                Create("[cat] dog");
                var span = GetBlockSpan(BlockKind.Bracket, _textBuffer.GetPoint(2));
                Assert.Equal(_textBuffer.GetSpan(0, 5), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is before it
            /// </summary>
            [WpfFact]
            public void Nested_Before()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(6));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is after it
            /// </summary>
            [WpfFact]
            public void Nested_After()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is at the end
            /// </summary>
            [WpfFact]
            public void Nested_FromLastChar()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(12));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }

            /// <summary>
            /// Make sure that we can process the nested block when the caret is at the start
            /// </summary>
            [WpfFact]
            public void Nested_FromFirstChar()
            {
                Create("cat (fo(a)od) dog");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(4));
                Assert.Equal(_textBuffer.GetSpan(4, 9), span);
            }
            /// <summary>
            /// Bad match because of no start char
            /// </summary>
            [WpfFact]
            public void Bad_NoStartChar()
            {
                Create("cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.True(span.IsNone());
            }

            /// <summary>
            /// Bad match because of no end char
            /// </summary>
            [WpfFact]
            public void Bad_NoEndChar()
            {
                Create("[cat dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(0));
                Assert.True(span.IsNone());
            }

            [WpfFact]
            public void Bad_EscapedStartChar()
            {
                Create(@"\[cat] dog");
                var span = _motionUtil.GetBlock(BlockKind.Bracket, _textBuffer.GetPoint(1));
                Assert.True(span.IsNone());
            }

            [WpfFact]
            public void StringTrap_BeforeString()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(3));
                Assert.Equal(_textBuffer.GetSpan(3, 12), span);
            }

            [WpfFact]
            public void StringTrap_AtStartOfString()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(8));
                Assert.Equal(_textBuffer.GetSpan(3, 12), span);
            }

            [WpfFact]
            public void StringTrap_OnStartCharacter()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = _motionUtil.GetBlock(BlockKind.Paren, _textBuffer.GetPoint(9));
                Assert.True(span.IsNone());
            }

            [WpfFact]
            public void StringTrap_AfterString()
            {
                Create("fun(a, \" (\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(14));
                Assert.Equal(_textBuffer.GetSpan(3, 12), span);
            }

            [WpfFact]
            public void BeforeBalancedString()
            {
                Create("fun(a, \"(foo)\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(8));
                Assert.Equal(_textBuffer.GetSpan(8, 5), span);
            }

            [WpfFact]
            public void InBalancedString()
            {
                Create("fun(a, \"(foo)\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(8, 5), span);
            }

            [WpfFact]
            public void AfterBalancedString()
            {
                Create("fun(a, \"(foo)\", b) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(13));
                Assert.Equal(_textBuffer.GetSpan(3, 15), span);
            }

            [WpfFact]
            public void InSplitString()
            {
                Create("fun(a, \" ( \", b, \" ) \", c) # bar");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(9, 11), span);
            }

            [WpfFact]
            public void StrayApostrophe()
            {
                // Reported in issue #2566.
                Create("if (done) { /* we're done */ Done(); }");
                var span = GetBlockSpan(BlockKind.CurlyBracket, _textBuffer.GetPoint(10));
                Assert.Equal(_textBuffer.GetSpan(10, 28), span);
            }

            [WpfFact]
            public void VerbatimString_BackslashAtEnd()
            {
                // Requested in issue #2504.
                Create("if (Directory.Exists(myDirectory + @\"\\\" + mySubDirectory))");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(4));
                Assert.Equal(_textBuffer.GetSpan(3, 55), span);
            }

            [WpfFact]
            public void VerbatimString_DoubleDoubleQuotes()
            {
                Create("foo(@\"bar\"\"\\\") # baz");
                var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(4));
                Assert.Equal(_textBuffer.GetSpan(3, 11), span);
            }

            [WpfFact]
            public void TwoStringsAroundParenthesis()
            {
                // Reported in issue #2632.
                // Example text: "foo" ("", true)
                Create("\"foo\" (\"\", true)");
                var first = 6;
                var last = 15;
                for (var contextPosition = first; contextPosition <= last; contextPosition++)
                {
                    var span = GetBlockSpan(BlockKind.Paren, _textBuffer.GetPoint(contextPosition));
                    Assert.Equal(_textBuffer.GetSpan(first, last - first + 1), span);
                }
            }
        }

        public sealed class AllBlockTest : MotionUtilTest
        {
            /// <summary>
            /// If there is not text after the { then that is simply excluded from the span.
            /// </summary>
            [WpfFact]
            public void SingleNoTextAfterOpenBrace()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var lineRange = _textBuffer.GetLineRange(startLine: 1, endLine: 3);
                Assert.Equal(lineRange.Extent, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextBeforeOpenBrace()
            {
                // The key to this test is the text before the open brace
                Create("if (true)", "dog {", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 1, column: 4),
                    _textBuffer.GetLine(3).End);
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextAfterCloseBrace()
            {
                // The key to this test is the text after the close brace
                Create("if (true)", "{", "  statement;", "} dog", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetLine(1).Start,
                    _textBuffer.GetPointInLine(line: 3, column: 1));
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleLineWiseWithCount()
            {
                var text =
@"if (true)
{
  s1;
  if (false)
  {
    s2;
  }
}
more";
                Create(text.Split(new[] { Environment.NewLine }, StringSplitOptions.None));

                var line = _textBuffer.GetLineFromLineNumber(5);
                var motionResult = _motionUtil.AllBlock(line.Start, BlockKind.CurlyBracket, count: 2).Value;
                var lineRange = _textBuffer.GetLineRange(startLine: 1, endLine: 7);
                Assert.Equal(lineRange.Extent, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void ValidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.AllBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("(cat (dog))", motionResult.Span.GetText());
            }

            [WpfFact]
            public void InvalidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.AllBlock(point, BlockKind.Paren, count: 3);
                Assert.True(motionResult.IsNone());
            }
        }

        public sealed class InnerBlockTest : MotionUtilTest
        {
            /// <summary>
            /// If there is not text after the { then that is simply excluded from the span.
            /// </summary>
            [WpfFact]
            public void SingleNoTextAfterOpenBrace()
            {
                Create("if (true)", "{", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                Assert.Equal(line.ExtentIncludingLineBreak, motionResult.Span);

                // Definitely a linewise paste operation.  This can be verified by simply pasting the
                // result here. 
                Assert.Equal(OperationKind.LineWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleNoTextAfterOpenBraceSpaceBeforeClose()
            {
                // The key to this test is the space before the close brace. 
                Create("if (true)", "{", "  statement;", "  }", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                Assert.Equal(line.ExtentIncludingLineBreak, motionResult.Span);
                Assert.Equal(OperationKind.LineWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleSpaceAfterOpenBrace()
            {
                // The key to this test is the space after the { 
                Create("if (true)", "{ ", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 1, column: 1),
                    _textBuffer.GetLine(2).End);
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextAfterOpenBrace()
            {
                Create("if (true)", "{ // test", "  statement;", "}", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 1, column: 1),
                    _textBuffer.GetLine(2).End);
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleTextBeforeCloseBrace()
            {
                Create("if (true)", "{", "  statement;", "dog }", "// after");

                var line = _textBuffer.GetLineFromLineNumber(2);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 1).Value;

                var span = new SnapshotSpan(
                    _textBuffer.GetLine(2).Start,
                    _textBuffer.GetPointInLine(line: 3, column: 4));
                Assert.Equal(span, motionResult.Span);
                Assert.Equal(OperationKind.CharacterWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void SingleLineWiseWithCount()
            {
                var text =
@"if (true)
{
  s1;
  if (false)
  {
    s2;
  }
}";
                Create(text.Split(new[] { Environment.NewLine }, StringSplitOptions.None));

                var line = _textBuffer.GetLineFromLineNumber(5);
                var motionResult = _motionUtil.InnerBlock(line.Start, BlockKind.CurlyBracket, count: 2).Value;
                var lineRange = SnapshotLineRangeUtil.CreateForLineAndCount(
                    _textBuffer.GetLine(2),
                    count: 5).Value;
                Assert.Equal(lineRange.ExtentIncludingLineBreak, motionResult.Span);

                // Definitely a linewise paste operation.  This can be verified by simply pasting the
                // result here. 
                Assert.Equal(OperationKind.LineWise, motionResult.OperationKind);
            }

            [WpfFact]
            public void ValidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)", motionResult.Span.GetText());
            }

            [WpfFact]
            public void InvalidCount()
            {
                Create("a (cat (dog)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 3);
                Assert.True(motionResult.IsNone());
            }

            [WpfFact]
            public void CountWithSideBySideBlocks()
            {
                Create("a (cat (dog)(blah)) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)(blah)", motionResult.Span.GetText());
            }

            [WpfFact]
            public void CountWithSideBySideBlocksAlt()
            {
                Create("a (cat (dog)(blah)) fish");

                var point = _textBuffer.GetPoint(13);
                Assert.Equal('b', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)(blah)", motionResult.Span.GetText());
            }

            [WpfFact]
            public void CountWithSideBySideBlocksHarder()
            {
                Create("a (cat (dog)(blah)(again(deep))) fish");

                var point = _textBuffer.GetPoint(8);
                Assert.Equal('d', point.GetChar());
                var motionResult = _motionUtil.InnerBlock(point, BlockKind.Paren, count: 2).Value;

                Assert.Equal("cat (dog)(blah)(again(deep))", motionResult.Span.GetText());
            }

            /// <summary>
            /// Single line inner block test should use inner block behavior
            /// </summary>
            [WpfFact]
            public void Simple()
            {
                Create("[cat]");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPoint(2), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal("cat", lines);
            }

            /// <summary>
            /// Multiline inner block test should use inner block behavior
            /// </summary>
            [WpfFact]
            public void Lines()
            {
                Create("[", "cat", "]");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(1, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(_textBuffer.GetLine(1).ExtentIncludingLineBreak.GetText(), lines);
            }

            /// <summary>
            /// Lines with whitespace inner block test
            /// </summary>
            [WpfFact]
            public void LinesAndWhitespace()
            {
                Create("", "    [", "      cat", "     ] ", "");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(2, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(_textBuffer.GetLine(2).ExtentIncludingLineBreak.GetText(), lines);
            }

            /// <summary>
            /// Inner block with content on line with start bracket
            /// </summary>
            [WpfFact]
            public void ContentOnLineWithOpeningBracket()
            {
                Create("[ dog", "  cat", "  ] ");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(1, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(" dog" + Environment.NewLine + "  cat", lines);
            }

            /// <summary>
            /// Inner block with content on line with start bracket
            /// </summary>
            [WpfFact]
            public void ContentOnLineWithClosingBracket()
            {
                Create("[ ", "  cat", "  dog ] ");
                var lines = _motionUtil.InnerBlock(_textBuffer.GetPointInLine(1, 1), BlockKind.Bracket, 1).Value.Span.GetText();
                Assert.Equal(" " + Environment.NewLine + "  cat" + Environment.NewLine + "  dog ", lines);
            }

            [WpfFact]
            public void DisregardDoubleCommentedMatchType()
            {
                Create(@"OutlineFileNameRegex(DuplicateBackslash(L""^OutlineFileName:(.*\\.*\\\\).* $""));");
                var motion = _motionUtil.InnerBlock(_textBuffer.GetPoint(22), BlockKind.Paren, 1);
                Assert.Equal(@"DuplicateBackslash(L""^OutlineFileName:(.*\\.*\\\\).* $"")", motion.Value.Span.GetText());
            }

            /// <summary>
            /// If the entire block is linewise empty, perform no motion
            /// </summary>
            [WpfFact]
            public void LinewiseEmpty()
            {
                // Reported in issue #1969.
                Create("    Main(", "    );", "");
                _textView.MoveCaretToLine(1);
                var caretPoint = _textView.Caret.Position.BufferPosition;
                var motion = _motionUtil.InnerBlock(caretPoint, BlockKind.Paren, 1);
                Assert.Equal(new SnapshotSpan(caretPoint, 0), motion.Value.Span);
            }
        }

        public sealed class QuotedStringTest : MotionUtilTest
        {
            [WpfFact]
            public void ItSelectsQuotesAlongWithInnerText()
            {
                Create(@"""foo""");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 5), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Include the leading whitespace
            /// </summary>
            [WpfFact]
            public void ItIncludesTheLeadingWhitespace()
            {
                Create(@"  ""foo""");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Include the trailing whitespace
            /// </summary>
            [WpfFact]
            public void ItIncludesTheTrailingWhitespace()
            {
                Create(@"""foo""  ");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Favor the trailing whitespace over leading
            /// </summary>
            [WpfFact]
            public void ItFavorsTrailingWhitespaceOverLeading()
            {
                Create(@"  ""foo""  ");

                var data = _motionUtil.QuotedString('"');

                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 2, 7), MotionKind.CharacterWiseInclusive);
                Assert.Equal(@"""foo""  ", data.Value.Span.GetText());
            }

            [WpfFact]
            public void WhenFavoringTrailingSpace_ItActuallyLooksAtTheFirstCharAfterTheEndQuote()
            {
                Create(@"  ""foo""X ");
                var start = _snapshot.GetText().IndexOf('f');
                _textView.MoveCaretTo(start);

                var data = _motionUtil.QuotedString('"');

                Assert.Equal(@"  ""foo""", data.Value.Span.GetText());
            }

            [WpfFact]
            public void ItFavorsTrailingWhitespaceOverLeading_WithOnlyOneTrailingSpace()
            {
                Create(@"  ""foo"" ");
                var start = _snapshot.GetText().IndexOf('f');
                _textView.MoveCaretTo(start);

                var data = _motionUtil.QuotedString('"');

                Assert.Equal(@"""foo"" ", data.Value.Span.GetText());
            }

            /// <summary>
            /// Ignore the escaped quotes
            /// </summary>
            [WpfFact]
            public void ItIgnoresEscapedQuotes()
            {
                Create(@"""foo\""""");

                var data = _motionUtil.QuotedString('"');

                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            /// <summary>
            /// Ignore the escaped quotes
            /// </summary>
            [WpfFact]
            public void ItIgnoresEscapedQuotes_AlternateEscape()
            {
                Create(@"""foo(""""");
                _localSettings.QuoteEscape = @"(";
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, 0, 7), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void NothingIsSelectedWhenThereAreNoQuotes()
            {
                Create(@"foo");
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsNone());
            }

            [WpfFact]
            public void ItSelectsTheInsideOfTheSecondQuotedWord()
            {
                Create(@"""foo"" ""bar""");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('"');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void SingleQuotesWork()
            {
                Create(@"""foo"" 'bar'");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('\'');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void BackquotesWork()
            {
                Create(@"""foo"" `bar`");
                var start = _snapshot.GetText().IndexOf('b');
                _textView.MoveCaretTo(start);
                var data = _motionUtil.QuotedString('`');
                Assert.True(data.IsSome());
                AssertData(data.Value, new SnapshotSpan(_snapshot, start - 2, 6), MotionKind.CharacterWiseInclusive);
            }

            [WpfFact]
            public void UnmatchedQuotesFirst()
            {
                Create(@"x 'cat'dog'");
                _textView.MoveCaretTo(3);
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("cat", data.Value.Span.GetText());
            }

            [WpfFact]
            public void UnmatchedQuotesSecond()
            {
                Create(@"x 'cat'dog'");
                _textView.MoveCaretTo(8);
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("dog", data.Value.Span.GetText());
            }

            /// <summary>
            /// When landing directly on a quote that has a preceding quote it is always considered the 
            /// second quote in a string
            /// </summary>
            [WpfFact]
            public void UnmatchedQuotesMiddleQuote()
            {
                Create(@"x 'cat'dog'");
                _textView.MoveCaretTo(6);
                Assert.Equal('\'', _textView.GetCaretPoint().GetChar());
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("cat", data.Value.Span.GetText());
            }

            /// <summary>
            /// Border between valid string pairs
            /// </summary>
            [WpfFact]
            public void Border()
            {
                Create(@"x 'cat'dog'fish'");
                _textView.MoveCaretTo(10);
                Assert.Equal('\'', _textView.GetCaretPoint().GetChar());
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal("fish", data.Value.Span.GetText());
            }

            [WpfFact]
            public void Issue1454()
            {
                Create(@"let x = '\\'");
                _textView.MoveCaretTo(9);
                var data = _motionUtil.QuotedStringContentsWithCount('\'', 1);
                Assert.True(data.IsSome());
                Assert.Equal(@"\\", data.Value.Span.GetText());
            }
        }

        public sealed class Word : MotionUtilTest
        {
            /// <summary>
            /// If the word motion crosses a new line and it's a moveement then we keep it. The 
            /// caret should move to the next line
            /// </summary>
            [WpfFact]
            public void AcrossLineBreakMovement()
            {
                Create("cat", " dog");
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.Movement);
                Assert.Equal(_textBuffer.GetLine(1).Start.Add(1), motionResult.Span.End);
            }

            /// <summary>
            /// If the word motion crosses a line rbeak and it's an operator then we back it up 
            /// because we don't want the new line in the operator
            /// </summary>
            [WpfFact]
            public void AcrossLineBreakOperator()
            {
                Create("cat  ", " dog");
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("cat  ", motionResult.Span.GetText());
            }

            /// <summary>
            /// Blank lines don't factor into an operator.  Make sure we back up over it completel
            /// </summary>
            [WpfFact]
            public void AcrossBlankLineOperator()
            {
                Create("dog", "cat", " ", " ", "  fish");
                _textView.MoveCaretToLine(1);
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("cat", motionResult.Span.GetText());
            }

            /// <summary>
            /// Empty lines are different because they are actual words so we don't back over them
            /// </summary>
            [WpfFact]
            public void AcrossEmptyLineOperator()
            {
                Create("dog", "cat", "", "  fish");
                _textView.MoveCaretToLine(1);
                var motionResult = _motionUtil.WordForward(WordKind.NormalWord, 2, MotionContext.AfterOperator);
                Assert.Equal("cat" + Environment.NewLine + Environment.NewLine, motionResult.Span.GetText());
            }

            [WpfFact]
            public void Forward1()
            {
                Create("foo bar");
                _textView.MoveCaretTo(0);
                var res = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.Movement);
                var span = res.Span;
                Assert.Equal(4, span.Length);
                Assert.Equal("foo ", span.GetText());
                Assert.True(res.IsAnyWordMotion);
                Assert.Equal(OperationKind.CharacterWise, res.OperationKind);
                Assert.True(res.IsAnyWordMotion);
            }

            [WpfFact]
            public void Forward2()
            {
                Create("foo bar");
                _textView.MoveCaretTo(1);
                var res = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.Movement);
                var span = res.Span;
                Assert.Equal(3, span.Length);
                Assert.Equal("oo ", span.GetText());
            }

            /// <summary>
            /// Word motion with a count
            /// </summary>
            [WpfFact]
            public void Forward3()
            {
                Create("foo bar baz");
                _textView.MoveCaretTo(0);
                var res = _motionUtil.WordForward(WordKind.NormalWord, 2, MotionContext.Movement);
                Assert.Equal("foo bar ", res.Span.GetText());
            }

            /// <summary>
            /// Count across lines
            /// </summary>
            [WpfFact]
            public void Forward4()
            {
                Create("foo bar", "baz jaz");
                var res = _motionUtil.WordForward(WordKind.NormalWord, 3, MotionContext.Movement);
                Assert.Equal("foo bar" + Environment.NewLine + "baz ", res.Span.GetText());
            }

            /// <summary>
            /// Count off the end of the buffer
            /// </summary>
            [WpfFact]
            public void Forward5()
            {
                Create("foo bar");
                var res = _motionUtil.WordForward(WordKind.NormalWord, 10, MotionContext.Movement);
                Assert.Equal("foo bar", res.Span.GetText());
            }

            [WpfFact]
            public void ForwardBigWordIsAnyWord()
            {
                Create("foo bar");
                var res = _motionUtil.WordForward(WordKind.BigWord, 1, MotionContext.Movement);
                Assert.True(res.IsAnyWordMotion);
            }

            [WpfFact]
            public void BackwardBothAreAnyWord()
            {
                Create("foo bar");
                Assert.True(_motionUtil.WordBackward(WordKind.NormalWord, 1).IsAnyWordMotion);
                Assert.True(_motionUtil.WordBackward(WordKind.BigWord, 1).IsAnyWordMotion);
            }

            /// <summary>
            /// Make sure we handle the case where the motion ends on the start of the next line
            /// but also begins in a blank line
            /// </summary>
            [WpfFact]
            public void ForwardFromBlankLineEnd()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal(" ", result.Span.GetText());
            }

            [WpfFact]
            public void ForwardFromBlankLineMiddle()
            {
                Create("cat", "   ", "dog");
                _textView.MoveCaretToLine(1, 1);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal("  ", result.Span.GetText());
            }

            [WpfFact]
            public void ForwardFromDoubleBlankLineEnd()
            {
                Create("cat", "   ", "   ", "dog");
                _textView.MoveCaretToLine(1, 2);
                var result = _motionUtil.WordForward(WordKind.NormalWord, 1, MotionContext.AfterOperator);
                Assert.Equal(" ", result.Span.GetText());
            }
        }

        public sealed class VisibleWindow : MotionUtilTest
        {
            [WpfFact]
            public void LineFromTopOfVisibleWindow1()
            {
                Create("foo", "bar", "baz");
                _textView.SetVisibleLineRange(start: 0, length: 1);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(_textBuffer.GetLineRange(0).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void LineFromTopOfVisibleWindow2()
            {
                Create("foo", "bar", "baz", "jazz");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            /// <summary>
            /// From visible line not caret point
            /// </summary>
            [WpfFact]
            public void LineFromTopOfVisibleWindow3()
            {
                Create("foo", "bar", "baz", "jazz");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                _textView.MoveCaretTo(_textBuffer.GetLine(1).Start.Position);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(1, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void LineFromTopOfVisibleWindow4()
            {
                Create("  foo", "bar");
                _textView.SetVisibleLineRange(start: 0, length: 1);
                _textView.MoveCaretTo(_textBuffer.GetLine(1).End);
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromTopOfVisibleWindow5()
            {
                Create("  foo", "bar");
                _textView.SetVisibleLineRange(start: 0, length: 1);
                _textView.MoveCaretTo(_textBuffer.GetLine(1).End);
                _globalSettings.StartOfLine = false;
                var data = _motionUtil.LineFromTopOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(3, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow1()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow2()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow3()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                _textView.MoveCaretTo(_textBuffer.GetLine(2).End);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption.Create(2)).Value;
                Assert.Equal(_textBuffer.GetLineRange(1, 2).ExtentIncludingLineBreak, data.Span);
                Assert.False(data.IsForward);
                Assert.Equal(OperationKind.LineWise, data.OperationKind);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow4()
            {
                Create("a", "b", "  c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 3);
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(2, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromBottomOfVisibleWindow5()
            {
                Create("a", "b", "  c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                _globalSettings.StartOfLine = false;
                var data = _motionUtil.LineFromBottomOfVisibleWindow(FSharpOption<int>.None).Value;
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
            }

            [WpfFact]
            public void LineFromMiddleOfWindow1()
            {
                Create("a", "b", "c", "d");
                _textView.SetVisibleLineRange(start: 0, length: 2);
                var data = _motionUtil.LineInMiddleOfVisibleWindow();
                Assert.Equal(new SnapshotSpan(_textBuffer.GetPoint(0), _textBuffer.GetLine(1).EndIncludingLineBreak), data.Value.Span);
                Assert.Equal(OperationKind.LineWise, data.Value.OperationKind);
            }
        }

        public sealed class GoToLine : MotionUtilTest
        {
            [WpfFact]
            public void WithStartOfLine()
            {
                // Reported in issue #2224.
                Create("aaa xxx", "bbb yyy", "ccc zzz", "");
                _globalSettings.StartOfLine = true;
                _textView.MoveCaretToLine(2, 4);
                _vimBufferData.MaintainCaretColumn = MaintainCaretColumn.NewSpaces(4);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(1));
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(!data.IsForward);
                Assert.Equal(0, data.CaretColumn.AsInLastLine().ColumnNumber);
                Assert.True(!data.MotionResultFlags.HasFlag(MotionResultFlags.MaintainCaretColumn));
            }

            [WpfFact]
            public void WithouthStartOfLine()
            {
                Create("aaa xxx", "bbb yyy", "ccc zzz", "");
                _globalSettings.StartOfLine = false;
                _textView.MoveCaretToLine(2, 4);
                _vimBufferData.MaintainCaretColumn = MaintainCaretColumn.NewSpaces(4);
                var data = _motionUtil.LineOrLastToFirstNonBlank(FSharpOption.Create(1));
                Assert.Equal(_textBuffer.GetLineRange(0, 2).ExtentIncludingLineBreak, data.Span);
                Assert.True(data.MotionKind.IsLineWise);
                Assert.True(!data.IsForward);
                Assert.Equal(4, data.CaretColumn.AsInLastLine().ColumnNumber);
                Assert.True(data.MotionResultFlags.HasFlag(MotionResultFlags.MaintainCaretColumn));
            }
        }

        public sealed class MatchingTokenTest : MotionUtilTest
        {
            [WpfFact]
            public void SimpleParens()
            {
                Create("( )");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( )", data.Span.GetText());
                Assert.True(data.IsForward);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
                Assert.Equal(OperationKind.CharacterWise, data.OperationKind);
            }

            [WpfFact]
            public void SimpleParensWithPrefix()
            {
                Create("cat( )");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("cat( )", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void TooManyOpenOnSameLine()
            {
                Create("cat(( )");
                Assert.True(_motionUtil.MatchingToken().IsNone());
            }

            [WpfFact]
            public void AcrossLines()
            {
                Create("cat(", ")");
                var span = new SnapshotSpan(
                    _textView.GetLine(0).Start,
                    _textView.GetLine(1).Start.Add(1));
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal(span, data.Span);
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void ParensFromEnd()
            {
                Create("cat( )");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( )", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            [WpfFact]
            public void ParensFromMiddle()
            {
                Create("cat( )");
                _textView.MoveCaretTo(4);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("( ", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Make sure we function properly with nested parens.
            /// </summary>
            [WpfFact]
            public void ParensNestedFromEnd()
            {
                Create("(((a)))");
                _textView.MoveCaretTo(5);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("((a))", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Make sure we function properly with consecutive sets of parens
            /// </summary>
            [WpfFact]
            public void ParensConsecutiveSetsFromEnd()
            {
                Create("((a)) /* ((b))");
                _textView.MoveCaretTo(12);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("(b)", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            /// <summary>
            /// Make sure we function properly with consecutive sets of parens
            /// </summary>
            [WpfFact]
            public void ParensConsecutiveSetsFromEnd2()
            {
                Create("((a)) /* ((b))");
                _textView.MoveCaretTo(13);
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("((b))", data.Span.GetText());
                Assert.False(data.IsForward);
            }

            [WpfFact]
            public void CommentStartDoesNotNest()
            {
                Create("/* /* */");
                var data = _motionUtil.MatchingToken().Value;
                Assert.Equal("/* /* */", data.Span.GetText());
                Assert.True(data.IsForward);
            }

            [WpfFact]
            public void IfElsePreProc()
            {
                Create("#if foo #endif", "again", "#endif");
                var data = _motionUtil.MatchingToken().Value;
                var span = new SnapshotSpan(_textView.GetPoint(0), _textView.GetLine(2).Start.Add(1));
                Assert.Equal(span, data.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }

            /// <summary>
            /// In the case the caret is on the end position of a line the search should actually start
            /// on the last valid column. Yet the returned span should not include the start token.
            /// </summary>
            [WpfFact]
            public void EndOfLine()
            {
                Create("{", "}  ");
                _textView.MoveCaretTo(_textBuffer.GetLine(0).End);
                var data = _motionUtil.MatchingToken().Value;
                var span = new SnapshotSpan(
                    _textBuffer.GetPointInLine(line: 0, column: 1),
                    _textBuffer.GetPointInLine(line: 1, column: 1));
                Assert.Equal(span, data.Span);
                Assert.Equal(MotionKind.CharacterWiseInclusive, data.MotionKind);
            }
        }

        public sealed class InnerParagraph : MotionUtilTest
        {
            [WpfFact]
            public void Empty()
            {
                Create("");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void OneLiner()
            {
                Create("a");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilEnd()
            {
                Create("a", "b", "c");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveCount2FilledLinesUntilEndIsInvalid()
            {
                Create("a", "b", "c");
                Assert.True(_motionUtil.InnerParagraph(2).IsNone());
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilBlank()
            {
                Create("a", "b", "");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilBlankFromMiddle()
            {
                Create("a", "b", "");
                _textView.MoveCaretToLine(1);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void StartingAfterFirstBlockSelectFilledLinesUntilEnd()
            {
                Create("a", "b", "", "c", "d", "e");
                _textView.MoveCaretToLine(4);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(3, 5).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void StartingAfterFirstBlockSelectFilledLinesUntilBlankFromMiddle()
            {
                Create("a", "b", "", "c", "d", "e", " ");
                _textView.MoveCaretToLine(4);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(3, 5).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveFilledLinesUntilBlankOrWhitespace()
            {
                Create("a", " ", "");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesUntilFilled()
            {
                Create("", "", "a");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesUntilFilledFromMiddle()
            {
                Create("", "", "a");
                _textView.MoveCaretToLine(1);
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesOrWhitespaceUntilFilled()
            {
                Create("", " ", "a");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 1).ExtentIncludingLineBreak, span);
            }

            [WpfFact]
            public void SelectConsecutiveBlankLinesWithWhitespaceOrTab()
            {
                Create("", "\t", " ");
                var span = _motionUtil.InnerParagraph(1).Value.Span;
                Assert.Equal(_snapshot.GetLineRange(0, 2).ExtentIncludingLineBreak, span);
            }

            /// <summary>
            /// Ignore the brace not on first column
            /// </summary>
            [WpfFact]
            public void SectionBackwardOrOpenBrace7()
            {
                Create("dog", "\f{brace", "pig", "}fox");
                _textView.MoveCaretTo(_textView.GetLine(2).Start.Position);
                var data = _motionUtil.SectionBackwardOrOpenBrace(2);
                Assert.Equal(_textView.GetLineRange(0, 1).ExtentIncludingLineBreak, data.Span);
            }

            [WpfFact]
            public void CaretBeforeOpeningParenSelectsInnerBlockOnLine()
            {
                Create("public void Method(string arg1, string arg2)");

                var motion = _motionUtil.InnerBlock(_textBuffer.GetPoint(0), BlockKind.Paren, 1);

                Assert.True(motion.IsSome());
                Assert.Equal("string arg1, string arg2", motion.Value.Span.GetText());
            }
        }
    }
}
