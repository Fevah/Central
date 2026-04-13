using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Workflow;

namespace TIG.TotalLink.Client.Module.Workflow.ViewModel.Widget
{
    public class WorkflowListViewModel : ListViewModelBase<Shared.DataModel.Workflow.Workflow>
    {
        #region Private Fields

        private readonly IWorkflowFacade _workflowFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public WorkflowListViewModel()
        {
        }

        /// <summary>
        /// Constructor with workflow facade.
        /// </summary>
        /// <param name="workflowFacade">Workflow facade for invoke service.</param>
        public WorkflowListViewModel(IWorkflowFacade workflowFacade)
        {
            // Store services.
            _workflowFacade = workflowFacade;

            // Initialize commands
            PublishWorkflowCommand = new AsyncCommandEx(OnPublishWorkflowExecuteAsync, OnPublishWorkflowCanExecute);
            UnpublishWorkflowCommand = new AsyncCommandEx(OnUnpublishWorkflowExecuteAsync, OnUnpublishWorkflowCanExecute);
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_workflowFacade);

                // Initialize the data source
                ItemsSource = _workflowFacade.CreateInstantFeedbackSource<Shared.DataModel.Workflow.Workflow>();
            });
        }

        #endregion


        #region Commands

        /// <summary>
        /// Publishes workflow to Workflow Manager.
        /// </summary>
        [WidgetCommand("Publish", "Workflow", RibbonItemType.ButtonItem, "Publish workflow to Workflow Manager.")]
        public ICommand PublishWorkflowCommand { get; private set; }

        /// <summary>
        /// Unpublishes workflow from Workflow Manager.
        /// </summary>
        [WidgetCommand("Unpublish", "Workflow", RibbonItemType.ButtonItem, "Unpublish workflow from Workflow Manager.")]
        public ICommand UnpublishWorkflowCommand { get; private set; }

        #endregion


        #region Mvvm Services

        IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if a workflow command can be executed, based on whether any related operations are in progress. 
        /// </summary>
        private bool CanExecuteWorkflowCommand
        {
            get
            {
                // Return false if no items are selected
                if (SelectedItems.Count == 0)
                    return false;

                return !(
                    ((AsyncCommandEx)PublishWorkflowCommand).IsExecuting
                    || ((AsyncCommandEx)UnpublishWorkflowCommand).IsExecuting
                );
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the PublishWorkflowCommand.
        /// </summary>
        private async Task OnPublishWorkflowExecuteAsync()
        {
            // Prepare messages
            var selectedItems = SelectedItems.ToList();
            var selectedItemCount = selectedItems.Count;
            var itemTypeName = typeof(Shared.DataModel.Workflow.Workflow).Name.AddSpaces();
            string title;
            string warningMessage;
            if (selectedItemCount == 1)
            {
                title = string.Format("Publish {0} : {1}", itemTypeName, selectedItems[0]);
                warningMessage = string.Format("{0} \"{1}\"", itemTypeName, selectedItems[0]);
            }
            else
            {
                itemTypeName = itemTypeName.Pluralize();
                title = string.Format("Publish {0} {1}", selectedItemCount, itemTypeName);
                warningMessage = string.Format("{0} selected {1}", selectedItemCount, itemTypeName);
            }

            // Show a warning before publishing the items
            if (MessageBoxService.Show(string.Format("Warning: This will publish the {0}!\r\n\r\nAre you sure?", warningMessage), title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // If the user pressed Yes, publish the selected items
                foreach (var item in selectedItems)
                {
                    var item1 = item;

                    // Abort if the name is empty
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish workflow with Id {0}\r\nCannot publish a workflow with an empty name.", item1.Oid)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if the workflow does not reference an activity
                    var activity = item.WorkflowActivity;
                    if (activity == null)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish workflow \"{0}\"!\r\nThis workflow is not associated with any activity.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if no Xaml has been created
                    if (activity.Xaml == null)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish workflow \"{0}\"!\r\nThe associated activity must be opened in the designer and saved before it can be published.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    //try
                    //{
                    //    // Publish the workflow
                    //    await _workflowFacade.PublishWorkflowAsync(item.Oid);
                    //    item.LastTimePublished = DateTime.UtcNow;
                    //    await item.SaveAsync(false, false);

                    //    // Add a log entry to indicate success
                    //    Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Published workflow \"{0}\".", item1.Name)), DocumentViewModel.Oid)));
                    //}
                    //catch (Exception ex)
                    //{
                    //    // Add a log entry to display the error
                    //    var knownError = ExceptionHelper.ParseKnownErrors(ex);
                    //    var errorMessage = (knownError != null ? knownError.Message : ex.Message);
                    //    Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish workflow \"{0}\"!\r\n{1}", item1.Name, errorMessage)), DocumentViewModel.Oid)));
                    //}
                }
            }
        }

        /// <summary>
        /// CanExecute method for the PublishWorkflowCommand.
        /// </summary>
        private bool OnPublishWorkflowCanExecute()
        {
            return CanExecuteWorkflowCommand && CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the UnpublishWorkflowCommand.
        /// </summary>
        private async Task OnUnpublishWorkflowExecuteAsync()
        {
            // Prepare messages
            var selectedItems = SelectedItems.ToList();
            var selectedItemCount = selectedItems.Count;
            var itemTypeName = typeof(Shared.DataModel.Workflow.Workflow).Name.AddSpaces();
            string title;
            string warningMessage;
            if (selectedItemCount == 1)
            {
                title = string.Format("Unpublish {0} : {1}", itemTypeName, selectedItems[0]);
                warningMessage = string.Format("{0} \"{1}\"", itemTypeName, selectedItems[0]);
            }
            else
            {
                itemTypeName = itemTypeName.Pluralize();
                title = string.Format("Unpublish {0} {1}", selectedItemCount, itemTypeName);
                warningMessage = string.Format("{0} selected {1}", selectedItemCount, itemTypeName);
            }

            // Show a warning before publishing the items
            if (MessageBoxService.Show(string.Format("Warning: This will unpublish the {0}!\r\n\r\nAre you sure?", warningMessage), title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // If the user pressed Yes, unpublish the selected items
                foreach (var item in selectedItems)
                {
                    var item1 = item;

                    // Abort if the name is empty
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish workflow with Id {0}\r\nCannot unpublish a workflow with an empty name.", item1.Oid)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if the workflow does not reference an activity
                    var activity = item.WorkflowActivity;
                    if (activity == null)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish workflow \"{0}\"!\r\nThis workflow is not associated with any activity.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if no Xaml has been created
                    if (activity.Xaml == null)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish workflow \"{0}\"!\r\nThe associated activity must be opened in the designer and saved before it can be unpublished.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if the item is not published
                    if (item.PublishedDate == null || item.UnpublishedDate > item.PublishedDate)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish workflow \"{0}\"!\r\nThis workflow is not currently published.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    //try
                    //{
                    //    // Unpublish the workflow
                    //    await _workflowFacade.UnpublishWorkflowActivityAsync(item.Oid);
                    //    item.LastTimeUnpublished = DateTime.UtcNow;
                    //    await item.SaveAsync(false, false);

                    //    // Add a log entry to indicate success
                    //    Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Unpublished workflow \"{0}\".", item1.Name)), DocumentViewModel.Oid)));
                    //}
                    //catch (Exception ex)
                    //{
                    //    // Add a log entry to display the error
                    //    var knownError = ExceptionHelper.ParseKnownErrors(ex);
                    //    var errorMessage = (knownError != null ? knownError.Message : ex.Message);
                    //    Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish workflow \"{0}\"!\r\n{1}", item1.Name, errorMessage)), DocumentViewModel.Oid)));
                    //}
                }
            }
        }

        /// <summary>
        /// CanExecute method for the UnpublishWorkflowCommand.
        /// </summary>
        private bool OnUnpublishWorkflowCanExecute()
        {
            return CanExecuteWorkflowCommand && CanExecuteWidgetCommand;
        }

        #endregion
    }
}
