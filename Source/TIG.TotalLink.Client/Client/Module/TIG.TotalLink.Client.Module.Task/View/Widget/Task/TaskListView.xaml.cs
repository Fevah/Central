using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Task.ViewModel.Widget.Task;

namespace TIG.TotalLink.Client.Module.Task.View.Widget.Task
{
    [Widget("Task List", "Task", "A flat list of Tasks.")]
    public partial class TaskListView
    {
        public TaskListView()
        {
            InitializeComponent();
        }

        public TaskListView(TaskListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
