using System.Collections.ObjectModel;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel
{
    /// <summary>
    /// Base class for the MainViewModel of a TotalLink client application.
    /// </summary>
    public abstract class MainViewModelBase : ViewModelBase
    {
        #region Private Fields

        private readonly ObservableCollection<RibbonCategoryViewModelBase> _ribbonCategories = new ObservableCollection<RibbonCategoryViewModelBase>();
        private readonly ObservableCollection<BackstageItemViewModelBase> _backstageItems = new ObservableCollection<BackstageItemViewModelBase>();
        private bool _isBackstageOpen;

        #endregion


        #region Constructors

        protected MainViewModelBase()
        {
            // Initialize messages
            Messenger.Default.Register<ShowDocumentMessage>(this, OnShowDocumentMessage);

            // Initialize commands
            ActivateDocumentRibbonCommand = new DelegateCommand<DocumentViewModel>(OnActivateDocumentRibbonExecute);
        }

        #endregion


        #region Mvvm Services

        protected IDocumentManagerService DocumentManager { get { return GetService<IDocumentManagerService>(); } }

        #endregion


        #region Commands

        /// <summary>
        /// Command to activate the ribbon page for the active document.
        /// </summary>
        public ICommand ActivateDocumentRibbonCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// All categories to display on the ribbon.
        /// </summary>
        public ObservableCollection<RibbonCategoryViewModelBase> RibbonCategories
        {
            get { return _ribbonCategories; }
        }

        /// <summary>
        /// Indicates if the backstage view is currently open.
        /// </summary>
        public bool IsBackstageOpen
        {
            get { return _isBackstageOpen; }
            set { SetProperty(ref _isBackstageOpen, value, () => IsBackstageOpen); }
        }

        /// <summary>
        /// All items to display in the backstage view.
        /// </summary>
        public ObservableCollection<BackstageItemViewModelBase> BackstageItems
        {
            get { return _backstageItems; }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handler for the ShowDocumentMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        protected virtual void OnShowDocumentMessage(ShowDocumentMessage message)
        {
            // Generate a DocumentId based on the contents of the message
            object documentId;
            if (message.IsFixed)
            {
                documentId = message.Name.ToControlName();
            }
            else
            {
                if (message.IsInitializedWithItem)
                    documentId = string.Format("{0}_{1}", message.Id, message.Parameter).ToControlName();
                else
                    documentId = message.Id;
            }

            // Attempt to find an existing document with the generated id
            var document = DocumentManager.FindDocumentById(documentId);

            // If a document was not found, create a new one
            if (document == null)
            {
                document = DocumentManager.CreateDocument("DocumentView", message, this);
                document.DestroyOnClose = true;

                // If the document is new, and is not fixed, then collect the temporary Oid it generated
                if (message.IsNew && !message.IsFixed)
                {
                    var documentViewModel = document.Content as DocumentViewModel;
                    if (documentViewModel != null)
                        documentId = documentViewModel.Oid;
                }

                // Apply the DocumentId
                document.Id = documentId;
            }

            // Show the document
            document.Show();

            // Make sure the backstage is closed after showing a document
            IsBackstageOpen = false;
        }

        /// <summary>
        /// Execute method for the ActivateDocumentRibbonCommand.
        /// </summary>
        /// <param name="document">The currently selected document.</param>
        private void OnActivateDocumentRibbonExecute(DocumentViewModel document)
        {
            // Abort if the document is null, or the document doesn't have a ActivateDocumentRibbonCommand assigned
            if (document == null || document.ActivateDocumentRibbonCommand == null)
                return;

            // Execute the ActivateDocumentRibbonCommand on the document
            document.ActivateDocumentRibbonCommand.Execute(null);
        }

        #endregion
    }
}
