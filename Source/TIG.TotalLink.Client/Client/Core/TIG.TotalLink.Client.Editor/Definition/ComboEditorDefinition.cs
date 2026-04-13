using System;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class ComboEditorDefinition : EnumEditorDefinitionBase
    {
        #region Private Fields

        private bool _allowPopup = true;

        #endregion


        #region Constructors

        public ComboEditorDefinition(Type enumType)
            : base(enumType)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the combo box can be opened.
        /// If this is set to false, the combo box button will be hidden.
        /// Defaults to true.
        /// </summary>
        public bool AllowPopup
        {
            get { return _allowPopup; }
            set { SetProperty(ref _allowPopup, value, () => AllowPopup); }
        }

        #endregion
    }
}
