using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Person Uploader", "Contact", "Uploads persons that were imported from a spreadsheet.")]
    public partial class PersonUploaderView
    {
        public PersonUploaderView()
        {
            InitializeComponent();
        }

        public PersonUploaderView(PersonUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
