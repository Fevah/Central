using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.Control;
using TIG.TotalLink.Client.Module.Admin.Enum.KeyedData;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using ViewHelper = TIG.TotalLink.Client.Core.Helper.ViewHelper;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Document
{
    public class PanelViewModel : EntityViewModelBase<Panel>
    {
        #region Public Events

        public delegate void ContentLoadedEventHandler(object sender, EventArgs e);
        public delegate void PanelClosedEventHandler(object sender, EventArgs e);

        public event ContentLoadedEventHandler ContentLoaded;
        public event PanelClosedEventHandler PanelClosed;

        #endregion


        #region Private Fields

        private readonly Guid _tempOid;
        private object _content;
        private WidgetViewModelBase _widgetViewModel;
        private bool _supportsCustomization;
        private bool _isCustomization;

        #endregion


        #region Constructors

        public PanelViewModel()
        {
            // Assign a temporary id for this panel until it is loaded or saved
            _tempOid = Guid.NewGuid();

            // Initialize commands
            EditPanelCommand = new DelegateCommand(OnEditPanelExecute);
            PanelClosedCommand = new DelegateCommand(OnPanelClosedExecute);
            ToggleCustomizationCommand = new DelegateCommand(OnToggleCustomizationExecute);
            
            // Initialize messages
            Messenger.Default.Register<EntityChangedMessage>(this, OnEntityChangedMessage);
        }

        public PanelViewModel(Panel dataModel)
            : this()
        {
            // Initialize the panel
            DataObject = dataModel;
            RefreshContent();
        }

        #endregion


        #region Mvvm Services

        private IDetailDialogService DetailDialogService { get { return GetService<IDetailDialogService>(); } }

        #endregion


        #region Commands

        /// <summary>
        /// Command to edit the panel.
        /// </summary>
        public ICommand EditPanelCommand { get; private set; }

        /// <summary>
        /// Command that is executed when this panel is closed.
        /// </summary>
        public ICommand PanelClosedCommand { get; private set; }

        /// <summary>
        /// Command which toggles the IsCustomization flag.
        /// </summary>
        public ICommand ToggleCustomizationCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the panel.
        /// </summary>
        public string Name
        {
            get { return DataObject.Name; }
        }

        /// <summary>
        /// View that this panel contains.
        /// </summary>
        public object Content
        {
            get { return _content; }
            private set { SetProperty(ref _content, value, () => Content); }
        }

        /// <summary>
        /// Indicates if this panel has been saved yet.
        /// </summary>
        public bool IsNew
        {
            get { return DataObject.Oid == Guid.Empty; }
        }

        /// <summary>
        /// The view model that this panel is parented to as a DocumentViewModel.
        /// </summary>
        public DocumentViewModel Document
        {
            get { return ((ISupportParentViewModel)this).ParentViewModel as DocumentViewModel; }
        }

        /// <summary>
        /// The PanelGroupViewModel that contains this panel.
        /// </summary>
        public PanelGroupViewModel PanelGroup
        {
            get
            {
                if (DataObject == null || DataObject.PanelGroup == null)
                    return null;

                var document = Document;
                if (document == null)
                    return null;

                return document.PanelGroups.FirstOrDefault(g => ReferenceEquals(g.DataObject, DataObject.PanelGroup));
            }
        }

        /// <summary>
        /// Indicates if the widget within this panel supports customization via the WidgetCustomizationControl.
        /// </summary>
        public bool SupportsCustomization
        {
            get { return _supportsCustomization; }
            private set { SetProperty(ref _supportsCustomization, value, () => SupportsCustomization); }
        }

        /// <summary>
        /// Indicates if the panel is currently in customization mode.
        /// </summary>
        public bool IsCustomization
        {
            get { return _isCustomization; }
            set { SetProperty(ref _isCustomization, value, () => IsCustomization); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Refreshes the content by loading the view specifed in ViewName.
        /// </summary>
        public void RefreshContent()
        {
            if (string.IsNullOrWhiteSpace(DataObject.ViewName))
            {
                SetError("creating view", "The View Name is empty!");
                return;
            }

            // Load the specified view
            Content = ViewLocator.Default.ResolveView(DataObject.ViewName);

            // Attempt to get the content as a FrameworkElement
            _widgetViewModel = null;
            var frameworkElement = Content as FrameworkElement;
            if (frameworkElement != null)
            {
                // Handle the Loaded event on the content
                frameworkElement.Loaded += Content_Loaded;

                // Store the widget viewmodel
                _widgetViewModel = frameworkElement.DataContext as WidgetViewModelBase;
            }

            // Set the parent of the widget viewmodel
            SetWidgetParent();
        }

        /// <summary>
        /// Replaces the panel content with an error message.
        /// </summary>
        /// <param name="action">The action that was being performed when the error occurred. (e.g. "creating", "initializing")</param>
        /// <param name="message">A string describing the error that occurred.</param>
        public void SetError(string action, string message)
        {
            Content = ViewHelper.CreateErrorView(DataObject.ViewName, action, message);
        }

        /// <summary>
        /// Allows the panel to disconnect from the UI without removing it from the parent document.
        /// </summary>
        public void Release()
        {
            RaisePanelClosed();
        }
        
        /// <summary>
        /// Adds or updates keyed data.
        /// </summary>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <param name="data">A Stream containing the data.</param>
        /// <param name="closeStream">Indicates if the stream should be closed.</param>
        public void SetKeyedData(KeyedDataGroupKeys groupKey, string itemKey, Stream data, bool closeStream = true)
        {
            KeyedDataHelper.SetData(DataObject.PanelDatas, groupKey, itemKey, data, closeStream);
        }

        /// <summary>
        /// Gets keyed data.
        /// </summary>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>
        /// A new MemoryStream containing the data.
        /// After the stream has been used, it should be disposed.
        /// </returns>
        public MemoryStream GetKeyedData(KeyedDataGroupKeys groupKey, string itemKey)
        {
            return KeyedDataHelper.GetData(DataObject.PanelDatas, groupKey, itemKey);
        }

        /// <summary>
        /// Removes keyed data.
        /// </summary>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>True if the keyed data was found and removed successfully; otherwise false.</returns>
        public bool RemoveKeyedData(KeyedDataGroupKeys groupKey, string itemKey)
        {
            return KeyedDataHelper.RemoveData(DataObject.PanelDatas, groupKey, itemKey);
        }

        /// <summary>
        /// Updates the keyed data on the contained widget.
        /// </summary>
        public void StoreKeyedData()
        {
            // Abort if the widget viewmodel was not found
            if (_widgetViewModel == null)
                return;

            // Call StoreKeyedData on the WidgetViewModel
            _widgetViewModel.StoreKeyedData();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sets the parent of the widget to be this panel.
        /// </summary>
        private void SetWidgetParent()
        {
            // Abort if the parent of this panel hasn't been set yet, or the widget viewmodel wasn't found
            if (((ISupportParentViewModel)this).ParentViewModel == null || _widgetViewModel == null)
                return;

            // Set the parent of the widget viewmodel to be this panel
            ((ISupportParentViewModel)_widgetViewModel).ParentViewModel = this;
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Raises the ContentLoaded event.
        /// </summary>
        protected void RaiseContentLoaded()
        {
            OnContentLoaded(new EventArgs());
        }

        /// <summary>
        /// Raises the PanelClosed event.
        /// </summary>
        protected void RaisePanelClosed()
        {
            OnPanelClosed(new EventArgs());
        }

        /// <summary>
        /// Raises the ContentLoaded event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnContentLoaded(EventArgs e)
        {
            if (ContentLoaded != null)
                ContentLoaded(this, e);
        }

        /// <summary>
        /// Raises the PanelClosed event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnPanelClosed(EventArgs e)
        {
            if (PanelClosed != null)
                PanelClosed(this, e);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the PanelClosedCommand.
        /// </summary>
        private void OnPanelClosedExecute()
        {
            // Notify widgets that the panel is closed
            RaisePanelClosed();

            // Remove the panel data object from the document data object panels list
            Document.DataObject.Panels.Remove(DataObject);

            // Delete the panel
            DataObject.Delete();
        }

        /// <summary>
        /// Execute method for the EditPanelCommand.
        /// </summary>
        private void OnEditPanelExecute()
        {
            var oldName = Name;
            var oldPanelGroup = PanelGroup;

            // Create a NestedUnitOfWork to track the edit
            DataObjectBase nestedDataObject = null;
            Document.UnitOfWork.ExecuteNestedUnitOfWork(nuow =>
            {
                // Create another NestedUnitOfWork containing a clone to edit
                var cloneNuow = nuow.BeginNestedUnitOfWork();
                var clone = DataObject.Clone(cloneNuow);

                // Show a dialog to edit the clone
                var result = DetailDialogService.ShowDialog(DetailEditMode.Edit, clone, "Widget");

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
                DataObject = nuow.GetParentObject(nestedDataObject) as Panel;

                // If the Name has changed, notify the UI
                if (Name != oldName)
                    RaisePropertyChanged(() => Name);

                // If the PanelGroup has changed, move this PanelViewModel to the correct FilteredPanels collection and notify the UI
                var newPanelGroup = PanelGroup;
                if (!ReferenceEquals(oldPanelGroup, newPanelGroup))
                {
                    if (oldPanelGroup == null)
                        Document.FilteredPanels.Remove(this);
                    else
                        oldPanelGroup.FilteredPanels.Remove(this);

                    if (newPanelGroup == null)
                        Document.FilteredPanels.Add(this);
                    else
                        newPanelGroup.FilteredPanels.Add(this);

                    RaisePropertyChanged(() => PanelGroup);
                }
            });
        }

        /// <summary>
        /// Execute method for the ToggleCustomizationCommand.
        /// </summary>
        private void OnToggleCustomizationExecute()
        {
            IsCustomization = !IsCustomization;
        }

        /// <summary>
        /// Handler for the EntityChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // Abort if the message was sent by this panel or the parent document, or the message doesn't contain a modify
            var document = Document;
            if (ReferenceEquals(message.Sender, this) || (document != null && ReferenceEquals(message.Sender, document)) || !message.ContainsChangeTypes(EntityChange.ChangeTypes.Modify))
                return;

            // If the message is a change for this panel, refresh the data object
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

        /// <summary>
        /// Handles the Loaded event on the panel Content.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Content_Loaded(object sender, RoutedEventArgs e)
        {
            // Stop handling the event
            var frameworkElement = (FrameworkElement)sender;
            frameworkElement.Loaded -= Content_Loaded;

            // Set SupportsCustomization based on whether the widget view contains a WidgetCustomizationControl
            var customizationControl = LayoutHelper.FindElementByType<WidgetCustomizationControl>(frameworkElement);
            SupportsCustomization = (customizationControl != null);

            // Notify others that the content is loaded
            RaiseContentLoaded();
        }

        #endregion


        #region Overrides

        public override Guid Oid
        {
            get { return IsNew ? _tempOid : DataObject.Oid; }
        }

        protected override void OnParentViewModelChanged(object parentViewModel)
        {
            SetWidgetParent();
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
