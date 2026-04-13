using System;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class OptionEditorDefinition : EnumEditorDefinitionBase
    {
        #region Private Fields

        private bool _showBorder = true;

        #endregion


        #region Constructors

        public OptionEditorDefinition(Type enumType)
            : base(enumType)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates whether a border will be displayed around the options.
        /// </summary>
        public bool ShowBorder
        {
            get { return _showBorder; }
            set { SetProperty(ref _showBorder, value, () => ShowBorder); }
        }

        #endregion
    }
}
