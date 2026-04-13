using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Vendor Importer", "Contact", "Imports Vendors from a spreadsheet.")]
    public partial class VendorImporterView
    {
        public VendorImporterView()
        {
            InitializeComponent();
        }

        public VendorImporterView(VendorImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
