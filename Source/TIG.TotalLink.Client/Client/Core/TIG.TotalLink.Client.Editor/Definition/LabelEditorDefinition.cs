using System.Windows;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class LabelEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private TextWrapping _textWrapping = TextWrapping.Wrap;
        private TextTrimming _textTrimming = TextTrimming.None;
        private TextAlignment _textAlignment = TextAlignment.Left;

        #endregion


        #region Public Properties

        /// <summary>
        /// Specifies whether text wraps when it reaches the edge of the container.
        /// Defaults to Wrap.
        /// </summary>
        public TextWrapping TextWrapping
        {
            get { return _textWrapping; }
            set { SetProperty(ref _textWrapping, value, () => TextWrapping); }
        }

        /// <summary>
        /// Specifies how text is trimmed when it overflows the edge of the container.
        /// Defaults to None.
        /// </summary>
        public TextTrimming TextTrimming
        {
            get { return _textTrimming; }
            set { SetProperty(ref _textTrimming, value, () => TextTrimming); }
        }

        /// <summary>
        /// Specifies how text is aligned.
        /// Defaults to Left.
        /// </summary>
        public TextAlignment TextAlignment
        {
            get { return _textAlignment; }
            set { SetProperty(ref _textAlignment, value, () => TextAlignment); }
        }

        #endregion
    }
}
