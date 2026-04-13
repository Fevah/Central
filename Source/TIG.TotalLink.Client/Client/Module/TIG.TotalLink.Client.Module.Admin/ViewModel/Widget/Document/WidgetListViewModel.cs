using System;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Command;
using TIG.TotalLink.Client.Module.Admin.Provider;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document
{
    public class WidgetListViewModel : ListViewModelBase<WidgetViewModel>
    {
        #region Private Fields

        private readonly IWidgetProvider _widgetProvider;
        private readonly ShowDocumentCommand _showDocumentCommand;

        #endregion


        #region Constructors

        public WidgetListViewModel()
        {
        }

        public WidgetListViewModel(IWidgetProvider widgetProvider)
            : this()
        {
            // Store services
            _widgetProvider = widgetProvider;

            // Initialize commands
            _showDocumentCommand = new ShowDocumentCommand();
            OpenCommand = new DelegateCommand(OnOpenExecute, OnOpenCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to open the selected documents.
        /// </summary>
        [WidgetCommand("Open", "Widget", RibbonItemType.ButtonItem, "Open a new document containing the selected widgets.")]
        public virtual ICommand OpenCommand { get; private set; }

        /// <summary>
        /// Override to hide the AddCommand.
        /// </summary>
        public override ICommand AddCommand { get { return null; } }

        /// <summary>
        /// Override to hide the DeleteCommand.
        /// </summary>
        public override ICommand DeleteCommand { get { return null; } }

        /// <summary>
        /// Override to hide the RefreshCommand.
        /// </summary>
        public override ICommand RefreshCommand { get { return null; } }

        #endregion



        #region Event Handlers

        /// <summary>
        /// Execute method for the OpenCommand.
        /// </summary>
        protected virtual void OnOpenExecute()
        {
            _showDocumentCommand.Execute(SelectedItems);
        }

        /// <summary>
        /// CanExecute method for the OpenCommand.
        /// </summary>
        protected virtual bool OnOpenCanExecute()
        {
            return CanExecuteWidgetCommand && SelectedItems.Count > 0;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Initialize the data source
                ItemsSource = _widgetProvider.Widgets;
            });
        }

        #endregion
    }
}
