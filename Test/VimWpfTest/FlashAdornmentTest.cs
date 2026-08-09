using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Vim.EditorHost;
using Vim.UnitTest;
using Vim.UI.Wpf.Implementation.FlashAdornment;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    /// <summary>
    /// End-to-end test of the flash label adornments: real IVimBuffer, real
    /// composition (FlashAdornmentFactory is MEF-composed from the test
    /// assembly), real adornment layer
    /// </summary>
    public sealed class FlashAdornmentTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IWpfTextView _textView;

        private void Create(params string[] lines)
        {
            _vimBuffer = CreateVimBuffer(lines);
            _textView = (IWpfTextView)_vimBuffer.TextView;
            _textView.DisplayTextLineContainingBufferPosition(
                _vimBuffer.TextBuffer.GetLine(0).Start, 0.0, ViewRelativePosition.Top);
            _textView.SetVisibleLineCount(lines.Length);
        }

        private IAdornmentLayer GetLayer()
        {
            return _textView.GetAdornmentLayer(FlashAdornmentFactory.FlashAdornmentLayerName);
        }

        [WpfFact]
        public void Labels_AppearOnMatches()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            Assert.NotEmpty(GetLayer().Elements);
        }

        [WpfFact]
        public void Labels_ClearOnSessionEnd()
        {
            Create("cat", "dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Flash, ModeArgument.NewFlash(FlashKind.Search));
            _vimBuffer.Process('c');
            Assert.NotEmpty(GetLayer().Elements);
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            Assert.Empty(GetLayer().Elements);
        }
    }
}
