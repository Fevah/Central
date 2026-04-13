using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Enquiry;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Enquiry
{
    [Widget("Enquiry List", "Sales", "A list of Enquiries.")]
    public partial class EnquiryListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public EnquiryListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public EnquiryListView(EnquiryListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
