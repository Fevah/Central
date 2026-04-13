using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class DecimalEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private int _decimals = 6;

        #endregion


        #region Public Properties

        /// <summary>
        /// The number of digits that will be allowed after the decimal point.
        /// Defaults to 6.
        /// </summary>
        public int Decimals
        {
            get { return _decimals; }
            set { SetProperty(ref _decimals, value, () => Decimals); }
        }

        /// <summary>
        /// Returns a numeric mask pattern based on the Decimals setting.
        /// </summary>
        public string Mask
        {
            get { return string.Format("N{0}", Decimals); }
        }

        #endregion


        #region Overrides

        public override double DefaultColumnWidth
        {
            get { return 100d; }
        }

        public override bool DefaultFixedWidth
        {
            get { return true; }
        }

        #endregion
    }
}
