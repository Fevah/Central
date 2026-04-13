using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.MvvmService;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Document
{
    public class PanelGroupViewModel : EntityViewModelBase<PanelGroup>
    {
        #region Private Fields

        private readonly Guid _tempOid;
        private readonly ObservableCollection<PanelViewModel> _filteredPanels = new ObservableCollection<PanelViewModel>();

        #endregion


        #region Constructors

        public PanelGroupViewModel()
        {
            // Assign a temporary id for this panel until it is loaded or saved
            _tempOid = Guid.NewGuid();

            // Initialize messages
            Messenger.Default.Register<EntityChangedMessage>(this, OnEntityChangedMessage);

            // Initialize commands
            DocumentModifiedCommand = new DelegateCommand(OnDocumentModifiedExecute);
            AddPanelCommand = new DelegateCommand(OnAddPanelExecute);
            EditPanelGroupCommand = new DelegateCommand(OnEditPanelGroupExecute);
            DeletePanelGroupCommand = new DelegateCommand(OnDeletePanelGroupExecute);
        }

        public PanelGroupViewModel(PanelGroup dataModel)
            : this()
        {
            // Initialize the panel group
            DataObject = dataModel;
        }

        #endregion


        #region Mvvm Services

        private IDetailDialogService DetailDialogService { get { return GetService<IDetailDialogService>(); } }

        #endregion


        #region Public Properties

        /// <summary>
        /// Command to add a panel.
        /// </summary>
        public ICommand AddPanelCommand { get; private set; }

        /// <summary>
        /// Command to edit the panel group.
        /// </summary>
        public ICommand EditPanelGroupCommand { get; private set; }

        /// <summary>
        /// Command to delete the panel group.
        /// </summary>
        public ICommand DeletePanelGroupCommand { get; private set; }

        /// <summary>
        /// Command to flag that the document has been modified.
        /// </summary>
        public ICommand DocumentModifiedCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the panel group.
        /// </summary>
        public string Name
        {
            get { return DataObject.Name; }
        }

        /// <summary>
        /// Indicates if this panel group has been saved yet.
        /// </summary>
        public bool IsNew
        {
            get { return DataObject.Oid == Guid.Empty; }
        }

        /// <summary>
        /// The view model that this panel group is parented to as a DocumentViewModel.
        /// </summary>
        public DocumentViewModel Document
        {
            get { return ((ISupportParentViewModel)this).ParentViewModel as DocumentViewModel; }
        }

        /// <summary>
        /// The subset of panels that belong to this panel group.
        /// </summary>
        public ObservableCollection<PanelViewModel> FilteredPanels
        {
            get { return _filteredPanels; }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the AddPanelCommand.
        /// </summary>
        private void OnAddPanelExecute()
        {
            // Attempt to get the parent document
            var document = Document;
            if (document == null)
                return;

            // Make this PanelGroup active
            document.ActiveGroup = this;

            // Invoke the AddPanelCommand on the document
            document.AddPanelCommand.Execute(null);
        }

        /// <summary>
        /// Execute method for the EditPanelGroupCommand.
        /// </summary>
        private void OnEditPanelGroupExecute()
        {
            // Create a NestedUnitOfWork to track the edit
            DataObjectBase nestedDataObject = null;
            Document.UnitOfWork.ExecuteNestedUnitOfWork(nuow =>
            {
                // Create another NestedUnitOfWork containing a clone to edit
                var cloneNuow = nuow.BeginNestedUnitOfWork();
                var clone = DataObject.Clone(cloneNuow);

                // Show a dialog to edit the clone
                var result = DetailDialogService.ShowDialog(DetailEditMode.Edit, clone, "Group");

                // If the dialog was accepted, copy the clone values back to the original data object
                if (result)
                {
                    nestedDataObject = nuow.GetNestedObject(DataObject);
                    clone.CopyTo(nestedDataObject);
                }

                // Dispose the clone NestedUnitOfWork so the clone is not saved
                cloneNuow.Dispose();

                // Return the dialog result to commit or rollback the main edit NestedUnitOfWork
                return result;
            }, nuow =>
            {
                // Abort if we did not get a modified copy of the data object from within the NestedUnitOfWork
                if (nestedDataObject == null)
                    return;

                // Refresh the DataObject with the modified copy
                DataObject = nuow.GetParentObject(nestedDataObject) as PanelGroup;
            });
        }

        /// <summary>
        /// Execute method for the DeletePanelGroupCommand.
        /// </summary>
        private void OnDeletePanelGroupExecute()
        {
            // Close all panels within the group
            foreach (var panelViewModel in FilteredPanels)
            {
                panelViewModel.Release();
            }

            // Remove the panel group data object from the document data object panel groups list
            Document.DataObject.PanelGroups.Remove(DataObject);

            // Delete the panel group
            DataObject.Delete();
        }

        /// <summary>
        /// Execute method for the DocumentModifiedCommand.
        /// </summary>
        private void OnDocumentModifiedExecute()
        {
            var document = Document;
            if (document == null || document.DocumentModifiedCommand == null)
                return;

            document.DocumentModifiedCommand.Execute(null);
        }

        /// <summary>
        /// Handler for the EntityChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // Abort if the message was sent by this panel group or the parent document, or the message doesn't contain a modify
            var document = Document;
            if (ReferenceEquals(message.Sender, this) || (document != null && ReferenceEquals(message.Sender, document)) || !message.ContainsChangeTypes(EntityChange.ChangeTypes.Modify))
                return;

            // If the message is a change for this panel group, refresh the data object
            if (document != null && message.ContainsEntitiesWithOid(Oid))
            {
                document.IgnoreModifications = true;
                try
                {
                    DataObject.Reload();
                }
                catch
                {
                    // Ignore load errors
                }
                document.IgnoreModifications = false;
            }
        }

        #endregion


        #region Overrides

        public override Guid Oid
        {
            get { return IsNew ? _tempOid : DataObject.Oid; }
        }

        protected override void OnParentViewModelChanged(object parentViewModel)
        {
            // Notify that the parent document has changed
            RaisePropertiesChanged(() => Document, () => DocumentModifiedCommand);
        }

        protected override void OnDataObjectPropertyChanged(ObjectChangeEventArgs e)
        {
            base.OnDataObjectPropertyChanged(e);

            // Reset will be sent when the data object is reloaded
            if (e.Reason == ObjectChangeReason.Reset)
            {
                RaisePropertyChanged(() => Name);
                return;
            }

            switch (e.PropertyName)
            {
                case "Oid":
                    RaisePropertyChanged(() => Oid);
                    RaisePropertyChanged(() => IsNew);
                    break;

                case "Name":
                    RaisePropertyChanged(() => Name);
                    break;
            }
        }

        public override string ToString()
        {
            return DataObject.ToString();
        }

        #endregion

    }
}
