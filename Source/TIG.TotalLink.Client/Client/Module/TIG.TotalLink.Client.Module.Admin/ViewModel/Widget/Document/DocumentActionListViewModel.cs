using System;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document
{
    public class DocumentActionListViewModel : ListViewModelBase<Shared.DataModel.Admin.DocumentAction>
    {
        #region Private Fields

        private readonly IAdminFacade _adminFacade;

        #endregion


        #region Constructors

        public DocumentActionListViewModel()
        {
        }

        public DocumentActionListViewModel(IAdminFacade adminFacade)
            : this()
        {
            // Store services
            _adminFacade = adminFacade;

            // Initialize commands
            OpenCommand = new DelegateCommand(OnOpenExecute, OnOpenCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to open the selected documents.
        /// </summary>
        [WidgetCommand("Open", "Document", RibbonItemType.ButtonItem, "Open the selected documents.")]
        public virtual ICommand OpenCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the OpenCommand.
        /// </summary>
        protected virtual void OnOpenExecute()
        {
            foreach (var documentAction in SelectedItems)
            {
                ShowDocumentMessage.Send(this, documentAction.Document, null);
            }
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
                // Attempt to connect to the AdminFacade
                ConnectToFacade(_adminFacade);

                // Initialize the data source
                ItemsSource = _adminFacade.CreateInstantFeedbackSource<Shared.DataModel.Admin.DocumentAction>();
            });
        }

        #endregion
    }
}
