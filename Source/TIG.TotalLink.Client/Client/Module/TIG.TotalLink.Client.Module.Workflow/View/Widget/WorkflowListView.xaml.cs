using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Workflow.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Workflow.View.Widget
{
    [Widget("Workflow List", "Workflow", "A list of Workflows.")]
    public partial class WorkflowListView
    {
        #region Constructors

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public WorkflowListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public WorkflowListView(WorkflowListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
