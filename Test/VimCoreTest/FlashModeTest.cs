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
        private ITextView _textView;
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
    }
}
