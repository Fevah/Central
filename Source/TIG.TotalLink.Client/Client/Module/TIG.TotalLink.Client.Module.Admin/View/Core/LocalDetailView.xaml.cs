using System.Windows;
using System.Windows.Controls;

namespace TIG.TotalLink.Client.Module.Admin.View.Core
{
    public partial class LocalDetailView : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty FormPaddingProperty = DependencyProperty.RegisterAttached(
            "FormPadding", typeof(Thickness), typeof(LocalDetailView), new PropertyMetadata(new Thickness(12)));

        /// <summary>
        /// The padding that should be applied to the DataLayoutControl that this view contains.
        /// </summary>
        public Thickness FormPadding
        {
            get { return (Thickness)GetValue(FormPaddingProperty); }
            set { SetValue(FormPaddingProperty, value); }
        }

        #endregion


        #region Constructors

        public LocalDetailView()
        {
            InitializeComponent();
        }

        #endregion
    }
}
