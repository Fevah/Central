using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Person Importer", "Contact", "Imports Persons from a spreadsheet.")]
    public partial class PersonImporterView
    {
        public PersonImporterView()
        {
            InitializeComponent();
        }

        public PersonImporterView(PersonImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
