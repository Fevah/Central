using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.Facade.Task;

namespace TIG.TotalLink.Client.Module.Task.ViewModel.Widget.Task
{
    public class TaskListViewModel : ListViewModelBase<Shared.DataModel.Task.Task>
    {
        #region Private Fields

        private readonly ITaskFacade _taskFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public TaskListViewModel()
        {
        }

        /// <summary>
        /// Constructor with Task facade.
        /// </summary>
        /// <param name="taskFacade">Task facade for invoke service.</param>
        public TaskListViewModel(ITaskFacade taskFacade)
            : this()
        {
            UseAddDialog = false;

            // Store services.
            _taskFacade = taskFacade;
        }

        #endregion

        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_taskFacade);

                // Initialize the data source
                ItemsSource = _taskFacade.CreateInstantFeedbackSource<Shared.DataModel.Task.Task>();
            });
        }

        #endregion

    }
}