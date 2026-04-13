using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Company Importer", "Contact", "Imports Chains, Companies and Branches from a spreadsheet.")]
    public partial class CompanyImporterView : UserControl
    {
        public CompanyImporterView()
        {
            InitializeComponent();
        }

        public CompanyImporterView(CompanyImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
