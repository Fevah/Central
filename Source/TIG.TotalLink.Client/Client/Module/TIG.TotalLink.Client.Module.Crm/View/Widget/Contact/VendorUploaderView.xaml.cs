using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Vendor Uploader", "Contact", "Uploads Vendors that were imported from a spreadsheet.")]
    public partial class VendorUploaderView
    {
        public VendorUploaderView()
        {
            InitializeComponent();
        }

        public VendorUploaderView(VendorUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
