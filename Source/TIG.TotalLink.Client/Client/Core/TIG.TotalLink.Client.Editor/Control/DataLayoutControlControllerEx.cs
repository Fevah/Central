using DevExpress.Xpf.LayoutControl;

namespace TIG.TotalLink.Client.Editor.Control
{
    public class DataLayoutControlControllerEx : DataLayoutControlController
    {
        #region Private Fields

        private LayoutControlCustomizationControl _externalCustomizationControl;

        #endregion


        #region Constructors

        public DataLayoutControlControllerEx(ILayoutControl control)
            : base(control)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The external LayoutControlCustomizationControl that this controller will use for customization.
        /// </summary>
        public LayoutControlCustomizationControl ExternalCustomizationControl
        {
            get { return _externalCustomizationControl; }
            set
            {
                if (Equals(_externalCustomizationControl, value))
                    return;

                _externalCustomizationControl = value;

                var layoutControlCustomizationController = CustomizationController as LayoutControlCustomizationControllerEx;
                if (layoutControlCustomizationController == null)
                    return;

                layoutControlCustomizationController.ExternalCustomizationControl = ExternalCustomizationControl;
            }
        }

        #endregion


        #region Overrides

        protected override LayoutControlCustomizationController CreateCustomizationController()
        {
            //System.Diagnostics.Debug.WriteLine("CreateCustomizationController");

            return new LayoutControlCustomizationControllerEx(this) { ExternalCustomizationControl = ExternalCustomizationControl };
        }

        #endregion
    }
}
