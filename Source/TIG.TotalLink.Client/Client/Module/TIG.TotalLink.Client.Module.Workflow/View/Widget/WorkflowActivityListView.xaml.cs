using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Workflow.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Workflow.View.Widget
{
    [Widget("Workflow Activity List", "Workflow", "A list of Workflow Activities.")]
    public partial class WorkflowActivityListView
    {
        #region Constructors

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public WorkflowActivityListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public WorkflowActivityListView(WorkflowActivityListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
