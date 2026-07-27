using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.FlashGlyph
{
    [Export(typeof(IGlyphFactoryProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [TagType(typeof(FlashLabelTag))]
    [Name("FlashGlyph")]
    [Order(Before = "VsTextMarker")]
    internal sealed class FlashGlyphFactoryProvider : IGlyphFactoryProvider
    {
        private readonly IVim _vim;
        private readonly IClassificationFormatMapService _classificationFormatMapService;

        [ImportingConstructor]
        internal FlashGlyphFactoryProvider(IVim vim, IClassificationFormatMapService classificationFormatMapService)
        {
            _vim = vim;
            _classificationFormatMapService = classificationFormatMapService;
        }

        public IGlyphFactory GetGlyphFactory(IWpfTextView textView, IWpfTextViewMargin margin)
        {
            var classificationFormatMap = _classificationFormatMapService.GetClassificationFormatMap(textView);
            return new FlashGlyphFactory(_vim, classificationFormatMap);
        }
    }
}
