using System.Windows;
using DevExpress.Xpf.Grid;

namespace TIG.TotalLink.Client.Module.Admin.Control
{
    public class StandaloneColumnChooserControl : System.Windows.Controls.Control
    {
        #region Dependency Properties

        public static readonly DependencyProperty ViewProperty = DependencyProperty.Register("View", typeof(GridDataViewBase), typeof(StandaloneColumnChooserControl), new UIPropertyMetadata(null));

        /// <summary>
        /// The grid view which we will display columns from.
        /// </summary>
        public GridDataViewBase View
        {
            get { return (GridDataViewBase)GetValue(ViewProperty); }
            set { SetValue(ViewProperty, value); }
        }

        #endregion


        #region Constructors

        public StandaloneColumnChooserControl()
        {
            DefaultStyleKey = typeof(StandaloneColumnChooserControl);
        }

        #endregion


        #region Internal Properties

        /// <summary>
        /// The ColumnChooserControl that will allow the user to manage columns.
        /// </summary>
        internal ColumnChooserControl ColunmChooserControl { get; private set; }

        #endregion


        #region Overrides

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            ColunmChooserControl = (ColumnChooserControl)GetTemplateChild("PART_ColumnChooserControl");
        }

        #endregion
    }
}
