using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Company Uploader", "Contact", "Uploads Chains, Companies and Branches that were imported from a spreadsheet.")]
    public partial class CompanyUploaderView : UserControl
    {
        public CompanyUploaderView()
        {
            InitializeComponent();
        }

        public CompanyUploaderView(CompanyUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
