using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Enquiry;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Enquiry
{
    [Widget("Enquiry Item List", "Sales", "A list of Enquiry Items.")]
    public partial class EnquiryItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public EnquiryItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public EnquiryItemListView(EnquiryItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
