using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class TextEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private string _regexMask;
        private MaskType _maskType = MaskType.None;

        #endregion


        #region Public Properties

        /// <summary>
        /// A regular expression to use as a mask.
        /// See https://documentation.devexpress.com/#WindowsForms/CustomDocument1501
        /// </summary>
        public string RegexMask
        {
            get { return _regexMask; }
            set { SetProperty(ref _regexMask, value, () => RegexMask, () => MaskType = (RegexMask == null ? MaskType.None : MaskType.RegEx)); }
        }

        /// <summary>
        /// The type of mask currently applied.
        /// </summary>
        public MaskType MaskType
        {
            get { return _maskType; }
            protected set { SetProperty(ref _maskType, value, () => MaskType); }
        }

        #endregion
    }
}
