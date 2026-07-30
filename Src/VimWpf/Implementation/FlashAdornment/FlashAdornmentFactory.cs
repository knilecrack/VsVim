using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.FlashAdornment
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class FlashAdornmentFactory : IVimBufferCreationListener
    {
        internal const string FlashAdornmentLayerName = "VsVimFlashAdornmentLayer";

#pragma warning disable 169, IDE0044
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(FlashAdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        private AdornmentLayerDefinition _flashAdornmentLayerDefinition;
#pragma warning restore 169

        #region IVimBufferCreationListener

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            if (vimBuffer.TextView is IWpfTextView textView)
            {
                var controller = new FlashAdornmentController(vimBuffer, textView, FlashAdornmentLayerName);
            }
        }

        #endregion
    }
}
