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
using TIG.TotalLink.Shared.DataModel.Workflow;
using TIG.TotalLink.Shared.Facade.Workflow;

namespace TIG.TotalLink.Client.Module.Workflow.ViewModel.Widget
{
    public class WorkflowActivityListViewModel : ListViewModelBase<WorkflowActivity>
    {
        #region Private Fields

        private readonly IWorkflowFacade _workflowFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public WorkflowActivityListViewModel() { }

        /// <summary>
        /// Constructor with workflow facade.
        /// </summary>
        /// <param name="workflowFacade">Workflow facade for invoke service.</param>
        public WorkflowActivityListViewModel(IWorkflowFacade workflowFacade)
        {
            // Store services.
            _workflowFacade = workflowFacade;

            // Initialize commands
            DesignActivityCommand = new DelegateCommand(OnDesignActivityExecute, OnDesignActivityCanExecute);
            PublishActivityCommand = new AsyncCommandEx(OnPublishActivityExecuteAsync, OnPublishActivityCanExecute);
            UnpublishActivityCommand = new AsyncCommandEx(OnUnpublishActivityExecuteAsync, OnUnpublishActivityCanExecute);
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
                ItemsSource = _workflowFacade.CreateInstantFeedbackSource<WorkflowActivity>();
            });
        }

        #endregion

        #region Commands

        /// <summary>
        /// Loads activity XAML into editor.
        /// </summary>
        [WidgetCommand("Design", "Workflow", RibbonItemType.ButtonItem, "Load the activity into the workflow designer for editing.")]
        public ICommand DesignActivityCommand { get; private set; }

        /// <summary>
        /// Publishes activity to Workflow Manager.
        /// </summary>
        [WidgetCommand("Publish", "Workflow", RibbonItemType.ButtonItem, "Publish activity to Workflow Manager.")]
        public ICommand PublishActivityCommand { get; private set; }

        /// <summary>
        /// Unublishes activity from Workflow Manager.
        /// </summary>
        [WidgetCommand("Unpublish", "Workflow", RibbonItemType.ButtonItem, "Unpublish activity from Workflow Manager.")]
        public ICommand UnpublishActivityCommand { get; private set; }

        #endregion


        #region Mvvm Services

        IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if an activity command can be executed, based on whether any related operations are in progress. 
        /// </summary>
        private bool CanExecuteActivityCommand
        {
            get
            {
                if (SelectedItems.Count == 0)
                    return false;

                return !(
                    ((AsyncCommandEx)PublishActivityCommand).IsExecuting
                    || ((AsyncCommandEx)UnpublishActivityCommand).IsExecuting
                );
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the DesignActivityCommand.
        /// </summary>
        protected virtual void OnDesignActivityExecute()
        {
            // Get the activity to edit
            var activity = SelectedItems.Single();

            // Notify the canvas to load the activity
            //var message = new EditWorkflowActivityMessage(this, activity);
            //DefaultMessenger.Send(message, DocumentViewModel.Oid);
        }

        /// <summary>
        /// CanExecute method for the DesignActivityCommand.
        /// </summary>
        protected virtual bool OnDesignActivityCanExecute()
        {
            // Return false if there is not only one activity selected
            if (SelectedItems == null || SelectedItems.Count != 1)
                return false;

            return CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the PublishActivityCommand.
        /// </summary>
        private async Task OnPublishActivityExecuteAsync()
        {
            // Prepare messages
            var selectedItems = SelectedItems.ToList();
            var selectedItemCount = selectedItems.Count;
            var itemTypeName = typeof(WorkflowActivity).Name.AddSpaces();
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
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish activity with Id {0}\r\nCannot publish an activity with an empty name.", item1.Oid)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if no Xaml has been created
                    if (item.Xaml == null)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish activity \"{0}\"!\r\nThe activity must be opened in the designer and saved before it can be published.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    try
                    {
                        // Publish the activity
                        //await _workflowFacade.PublishWorkflowActivityAsync(item.Oid);
                        item.PublishedDate = DateTime.UtcNow;
                        //await item.SaveAsync(false, false);

                        // Add a log entry to indicate success
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Published activity \"{0}\".", item1.Name)), DocumentViewModel.Oid)));
                    }
                    catch (Exception ex)
                    {
                        // Add a log entry to display the error
                        //var knownError = ExceptionHelper.ParseKnownErrors(ex);
                        //var errorMessage = (knownError != null ? knownError.Message : ex.Message);
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to publish activity \"{0}\"!\r\n{1}", item1.Name, errorMessage)), DocumentViewModel.Oid)));
                    }
                }
            }
        }

        /// <summary>
        /// CanExecute method for the PublishActivityCommand.
        /// </summary>
        private bool OnPublishActivityCanExecute()
        {
            return CanExecuteActivityCommand && CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the UnpublishActivityCommand.
        /// </summary>
        private async Task OnUnpublishActivityExecuteAsync()
        {
            // Prepare messages
            var selectedItems = SelectedItems.ToList();
            var selectedItemCount = selectedItems.Count;
            var itemTypeName = typeof(WorkflowActivity).Name.AddSpaces();
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
                title = string.Format("Unublish {0} {1}", selectedItemCount, itemTypeName);
                warningMessage = string.Format("{0} selected {1}", selectedItemCount, itemTypeName);
            }

            // Show a warning before unpublishing the items
            if (MessageBoxService.Show(string.Format("Warning: This will unpublish the {0}!\r\n\r\nAre you sure?", warningMessage), title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // If the user pressed Yes, unpublish the selected items
                foreach (var item in selectedItems)
                {
                    var item1 = item;

                    // Abort if the name is empty
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish activity with Id {0}\r\nCannot unpublish an activity with an empty name.", item1.Oid)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if no Xaml has been created
                    if (item.Xaml == null)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish activity \"{0}\"!\r\nThe activity must be opened in the designer and saved before it can be unpublished.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    // Abort if the item is not published
                    if (item.PublishedDate == null || item.UnpublishedDate > item.PublishedDate)
                    {
                        //Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish activity \"{0}\"!\r\nThis activity is not currently published.", item1.Name)), DocumentViewModel.Oid)));
                        continue;
                    }

                    //try
                    //{
                    //    // Unpublish the activity
                    //    await _workflowFacade.UnpublishWorkflowActivityAsync(item.Oid);
                    //    item.LastTimeUnpublished = DateTime.UtcNow;
                    //    await item.SaveAsync(false, false);

                    //    // Add a log entry to indicate success
                    //    Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Unpublished activity \"{0}\".", item1.Name)), DocumentViewModel.Oid)));
                    //}
                    //catch (Exception ex)
                    //{
                    //    // Add a log entry to display the error
                    //    var knownError = ExceptionHelper.ParseKnownErrors(ex);
                    //    var errorMessage = (knownError != null ? knownError.Message : ex.Message);
                    //    Application.Current.Dispatcher.BeginInvoke(new Action(() => DefaultMessenger.Send(new AppendLogMessage(this, string.Format("Failed to unpublish activity \"{0}\"!\r\n{1}", item1.Name, errorMessage)), DocumentViewModel.Oid)));
                    //}
                }
            }
        }

        /// <summary>
        /// CanExecute method for the UnpublishActivityCommand.
        /// </summary>
        private bool OnUnpublishActivityCanExecute()
        {
            return CanExecuteActivityCommand && CanExecuteWidgetCommand;
        }

        #endregion
    }
}
