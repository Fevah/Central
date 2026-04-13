using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class ImageEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private bool _showBorder = true;

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates whether a border will be displayed around the image.
        /// </summary>
        public bool ShowBorder
        {
            get { return _showBorder; }
            set { SetProperty(ref _showBorder, value, () => ShowBorder); }
        }

        #endregion


        #region Overrides

        /// <summary>
        /// The parent EditorWrapperBase that contains this EditorDefinitionBase.
        /// </summary>
        public override EditorWrapperBase Wrapper
        {
            get { return base.Wrapper; }
            set
            {
                var wrapper = base.Wrapper;
                SetProperty(ref wrapper, value, (string)null, () =>
                {
                    base.Wrapper = value;

                    // TODO : ImageEdit can't save values, so for now it is forced to always be read-only
                    Wrapper.IsReadOnly = true;
                });
            }
        }

        public override double DefaultColumnWidth
        {
            get { return 50d; }
        }

        public override bool DefaultFixedWidth
        {
            get { return true; }
        }

        #endregion
    }
}
