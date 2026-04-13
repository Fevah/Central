using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AutoMapper;
using DevExpress.Mvvm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Editor.Interface;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum.KeyedData;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    public class WidgetViewModelBase : ViewModelBase, IWidgetEvents
    {
        #region Public Events

        public event WidgetLoadedEventHandler WidgetLoaded;
        public event WidgetStartedEventHandler WidgetStarted;
        public event WidgetClosedEventHandler WidgetClosed;

        #endregion


        #region Private Fields

        private bool _isLoadingPanelVisible;
        private bool _widgetInitialized;
        private bool _contentLoaded;
        private readonly List<Task> _startupTasks = new List<Task>();
        private object _oldDocumentId;
        private readonly Dictionary<string, Action<MessageBase>> _messageHandlers = new Dictionary<string, Action<MessageBase>>();
        private bool _isHighlighted;

        #endregion


        #region Constructors

        protected WidgetViewModelBase()
        {
            // Initialize collections
            RibbonGroups = new ObservableCollection<RibbonGroupViewModel>();

            // Initialize commands
            DocumentModifiedCommand = new DelegateCommand(OnDocumentModifiedExecute);
        }

        #endregion


        #region Mvvm Services

        [Display(AutoGenerateField = false)]
        public IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        [Display(AutoGenerateField = false)]
        public IDetailDialogService DetailDialogService { get { return GetService<IDetailDialogService>(); } }

        #endregion


        #region Commands

        /// <summary>
        /// Command to flag that the document has been modified.
        /// This should be called when any change occurs to the widget layout that requires it to be re-saved.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public ICommand DocumentModifiedCommand { get; set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// Generated ribbon groups which contain items for all widget commands.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public ObservableCollection<RibbonGroupViewModel> RibbonGroups { get; private set; }

        /// <summary>
        /// Indicates if the loading panel is visible.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsLoadingPanelVisible
        {
            get { return _isLoadingPanelVisible; }
            set { SetProperty(ref _isLoadingPanelVisible, value, () => IsLoadingPanelVisible); }
        }

        /// <summary>
        /// Indicates if a widget command can be executed, based on whether any related operations are in progress.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public virtual bool CanExecuteWidgetCommand
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Indicates if this widget is currently highlighted.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set { SetProperty(ref _isHighlighted, value, () => IsHighlighted); }
        }

        /// <summary>
        /// Indicates if the parent document has been modified.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsDocumentModified
        {
            get
            {
                var document = DocumentViewModel;
                if (document == null)
                    return false;

                return document.IsModified;
            }
            set
            {
                if (IgnoreDocumentModifications)
                    return;

                var document = DocumentViewModel;
                if (document == null)
                    return;

                document.IsModified = value;
            }
        }

        /// <summary>
        /// Indicates if changes to the document should be ignored.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IgnoreDocumentModifications
        {
            get
            {
                var document = DocumentViewModel;
                if (document == null)
                    return false;

                return document.IgnoreModifications;
            }
            set
            {
                var document = DocumentViewModel;
                if (document == null)
                    return;

                document.IgnoreModifications = value;
            }
        }

        /// <summary>
        /// Returns the Oid for the panel that this widget is contained within.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public Guid PanelOid
        {
            get
            {
                var panel = PanelViewModel;
                if (panel == null)
                    return Guid.Empty;

                return panel.Oid;
            }
        }

        /// <summary>
        /// The root document that contains this widget.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public DocumentViewModel DocumentViewModel
        {
            get
            {
                // Find the parent PanelViewModel
                var panelViewModel = PanelViewModel as ISupportParentViewModel;
                if (panelViewModel == null)
                    return null;

                // Return the parent DocumentViewModel
                return panelViewModel.ParentViewModel as DocumentViewModel;
            }
        }

        /// <summary>
        /// The panel that contains this widget.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public PanelViewModel PanelViewModel
        {
            get { return ((ISupportParentViewModel)this).ParentViewModel as PanelViewModel; }
        }

        /// <summary>
        /// Link data that manages the messages this widget sends.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public WidgetLinkData SendLinkData { get; private set; }

        /// <summary>
        /// Link data that manages the messages this widget receives.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public WidgetLinkData ReceiveLinkData { get; private set; }

        /// <summary>
        /// Filter data that manages the messages this widget receives.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public WidgetFilterData FilterData { get; private set; }

        /// <summary>
        /// Init data that manages how this widget initializes its data.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public WidgetModelInitData ModelInitData { get; private set; }

        #endregion


        #region Protected Properties

        /// <summary>
        /// Common message bus.
        /// </summary>
        protected IMessenger DefaultMessenger
        {
            get { return Messenger.Default; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates a WidgetCommandData object containing additional information for dynamic widget commands.
        /// Inheriters can override this to add information to the WidgetCommandData.
        /// </summary>
        /// <returns>A WidgetCommandData object containing additional information for dynamic widget commands.</returns>
        public virtual WidgetCommandData GetWidgetCommandData()
        {
            return new WidgetCommandData();
        }

        /// <summary>
        /// Inheriters can override this method to supply user data that should be saved for the widget.
        /// New data should be supplied by calling SetKeyedData.
        /// </summary>
        public virtual void StoreKeyedData()
        {
            // Create a JArray to contain the combined widget data
            var combinedObject = new JArray();

            // Store SendLinkData if it is not empty
            if (SendLinkData != null && (SendLinkData.HasMessageTypes || SendLinkData.HasSelectedLinks))
                combinedObject.Add(SendLinkData.SerializeToJsonObject());

            // Store ReceiveLinkData if it is not empty
            if (ReceiveLinkData != null && (ReceiveLinkData.HasMessageTypes || ReceiveLinkData.HasSelectedLinks))
                combinedObject.Add(ReceiveLinkData.SerializeToJsonObject());

            // Store FilterData if it is not empty
            if (FilterData != null && FilterData.HasFilters)
                combinedObject.Add(FilterData.SerializeToJsonObject());

            // Store ModelInitData if it is not empty
            if (ModelInitData != null && ModelInitData.HasModelInitializers)
                combinedObject.Add(ModelInitData.SerializeToJsonObject());

            // If the JArray is not empty, convert it to a stream and save it
            if (combinedObject.Count > 0)
            {
                var combinedStream = new MemoryStream(Encoding.UTF8.GetBytes(combinedObject.ToString(Formatting.None)));
                SetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.WidgetData, GetType().FullName, combinedStream);
            }
        }

        /// <summary>
        /// Adds or updates keyed data.
        /// </summary>
        /// <param name="scope">The scope that the data will be stored in.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <param name="data">A Stream containing the data.</param>
        /// <param name="closeStream">Indicates if the stream should be closed.</param>
        public void SetKeyedData(KeyedDataScopes scope, KeyedDataGroupKeys groupKey, string itemKey, Stream data, bool closeStream = true)
        {
            switch (scope)
            {
                case KeyedDataScopes.Document:
                    var documentViewModel = DocumentViewModel;
                    if (documentViewModel != null)
                        documentViewModel.SetKeyedData(groupKey, itemKey, data, closeStream);
                    break;

                case KeyedDataScopes.Panel:
                    var panelViewModel = PanelViewModel;
                    if (panelViewModel != null)
                        panelViewModel.SetKeyedData(groupKey, itemKey, data, closeStream);
                    break;
            }
        }

        /// <summary>
        /// Gets keyed data.
        /// </summary>
        /// <param name="scope">The scope that the data should be collected from.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>
        /// A new MemoryStream containing the data.
        /// After the stream has been used, it should be disposed.
        /// </returns>
        public MemoryStream GetKeyedData(KeyedDataScopes scope, KeyedDataGroupKeys groupKey, string itemKey)
        {
            switch (scope)
            {
                case KeyedDataScopes.Document:
                    var documentViewModel = DocumentViewModel;
                    if (documentViewModel != null)
                        return documentViewModel.GetKeyedData(groupKey, itemKey);
                    break;

                case KeyedDataScopes.Panel:
                    var panelViewModel = PanelViewModel;
                    if (panelViewModel != null)
                        return panelViewModel.GetKeyedData(groupKey, itemKey);
                    break;
            }

            return null;
        }

        /// <summary>
        /// Removes keyed data.
        /// </summary>
        /// <param name="scope">The scope that the data should be removed from.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>True if the widget data was found and removed successfully; otherwise false.</returns>
        public bool RemoveKeyedData(KeyedDataScopes scope, KeyedDataGroupKeys groupKey, string itemKey)
        {
            switch (scope)
            {
                case KeyedDataScopes.Document:
                    var documentViewModel = DocumentViewModel;
                    if (documentViewModel != null)
                        return documentViewModel.RemoveKeyedData(groupKey, itemKey);
                    break;

                case KeyedDataScopes.Panel:
                    var panelViewModel = PanelViewModel;
                    if (panelViewModel != null)
                        return panelViewModel.RemoveKeyedData(groupKey, itemKey);
                    break;
            }

            return false;
        }

        /// <summary>
        /// Sends a document message.
        /// This overload allows any custom message token to be used.
        /// </summary>
        /// <typeparam name="TMessage">The message type being sent.</typeparam>
        /// <param name="token">An object that can be used to identify a specific message.</param>
        /// <param name="message">The message to send.</param>
        public void SendDocumentMessage<TMessage>(object token, TMessage message)
            where TMessage : MessageBase
        {
            // If the send LinkMode is SelectedWidgetsOnly, then add the valid target widgets to the message
            if (SendLinkData.LinkMode == WidgetLinkData.LinkModes.SelectedWidgetsOnly)
                message.Receivers = SendLinkData.SelectedLinks.Where(l => !l.IsBroken).Select(l => (object)l.GetWidget()).ToList();

            // Send the message
            DefaultMessenger.Send(message, token);
        }

        /// <summary>
        /// Sends a document message.
        /// This overload will always use the current DocumentId as the message token.
        /// Therefore the message will only be received by widgets in the same document.
        /// </summary>
        /// <typeparam name="TMessage">The message type being sent.</typeparam>
        /// <param name="message">The message to send.</param>
        public void SendDocumentMessage<TMessage>(TMessage message)
            where TMessage : MessageBase
        {
            var documentViewModel = DocumentViewModel;
            if (documentViewModel != null)
                SendDocumentMessage(documentViewModel.DocumentId, message);
        }

        /// <summary>
        /// Adds a message type to the SendLinkData after the widget has been initialized.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that this widget can send.</typeparam>
        public void AddSendMessageType<TMessage>()
            where TMessage : MessageBase
        {
            AddSendMessageType(typeof(TMessage));
        }

        /// <summary>
        /// Removes a message type from the SendLinkData after the widget has been initialized.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that this widget can send.</typeparam>
        public void RemoveSendMessageType<TMessage>()
            where TMessage : MessageBase
        {
            RemoveSendMessageType(typeof(TMessage));
        }

        /// <summary>
        /// Adds a message type to the SendLinkData after the widget has been initialized.
        /// </summary>
        /// <param name="type">The type of message that this widget can send.</param>
        public void AddSendMessageType(Type type)
        {
            SendLinkData.AddMessageType(type);

            var documentViewModel = DocumentViewModel;
            if (documentViewModel != null)
                documentViewModel.RefreshWidgetLinkData();
        }

        /// <summary>
        /// Removes a message type from the SendLinkData after the widget has been initialized.
        /// </summary>
        /// <param name="type">The type of message that this widget can no longer send.</param>
        public void RemoveSendMessageType(Type type)
        {
            SendLinkData.RemoveMessageType(type);

            var documentViewModel = DocumentViewModel;
            if (documentViewModel != null)
                documentViewModel.RefreshWidgetLinkData();
        }

        /// <summary>
        /// Refreshes all WidgetLinkData in this widget.
        /// </summary>
        public void RefreshWidgetLinkData()
        {
            if (SendLinkData != null)
                SendLinkData.Refresh();

            if (ReceiveLinkData != null)
                ReceiveLinkData.Refresh();
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when the root document, or it's id, changes.
        /// </summary>
        /// <param name="document">The root document.</param>
        protected virtual void OnDocumentChanged(DocumentViewModel document)
        {
            //System.Diagnostics.Debug.WriteLine("OnDocumentChanged {0}", new object[] { GetType().Name });

            // If document specific messages have already been registered, they must be unregistered before registering with the new document
            if (_oldDocumentId != null)
                OnUnregisterDocumentMessages(_oldDocumentId);

            // Register document specific messages
            if (document != null)
            {
                OnRegisterDocumentMessages(document.DocumentId);
            }
        }

        /// <summary>
        /// Called when document specific messages should be registered.
        /// </summary>
        /// <param name="documentId">The id of the document to register messages to.</param>
        protected virtual void OnRegisterDocumentMessages(object documentId)
        {
            // Store the document id so we can unregister messages from it later
            _oldDocumentId = documentId;
        }

        /// <summary>
        /// Called when document specific messages should be unregistered.
        /// </summary>
        /// <param name="documentId">The id of the document to unregister messages from.</param>
        protected virtual void OnUnregisterDocumentMessages(object documentId)
        {
        }

        /// <summary>
        /// Called when the widget content has been loaded.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnWidgetLoaded(EventArgs e)
        {
            if (WidgetLoaded != null)
                WidgetLoaded(this, e);
        }

        /// <summary>
        /// Called when the widget has successfully completed all startup tasks.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnWidgetStarted(EventArgs e)
        {
            if (WidgetStarted != null)
                WidgetStarted(this, e);
        }

        /// <summary>
        /// Called when the widget is closed.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnWidgetClosed(EventArgs e)
        {
            if (WidgetClosed != null)
                WidgetClosed(this, e);
        }

        /// <summary>
        /// Adds a task to the list of startup tasks that this widget will process.
        /// To be effective, this must be called in or before the WidgetLoaded event.
        /// </summary>
        protected void AddStartupTask(Action action)
        {
            _startupTasks.Add(Task.Run(action));
        }

        /// <summary>
        /// Helper function for connecting to a facade.
        /// </summary>
        /// <param name="facade">The facade to connect to.</param>
        /// <param name="serviceTypes">ServiceTypes to indicate which service need to be connect</param>
        protected virtual void ConnectToFacade(IFacadeBase facade, ServiceTypes serviceTypes = ServiceTypes.Data | ServiceTypes.Method)
        {
            if (facade != null)
                facade.Connect(serviceTypes);
        }

        /// <summary>
        /// Registers to receive a document message.
        /// </summary>
        /// <typeparam name="TMessage">The message type to register.</typeparam>
        /// <param name="token">An object that can be used to identify a specific message.</param>
        /// <param name="action">The action to call when the message occurs.</param>
        protected void RegisterDocumentMessage<TMessage>(object token, Action<TMessage> action)
            where TMessage : MessageBase
        {
            // It doesn't make sense to register multiple handlers for one message type
            // So throw an error if this message type is already handled for this widget
            var messageTypeName = typeof(TMessage).FullName;
            if (_messageHandlers.ContainsKey(messageTypeName))
                throw new ArgumentException(string.Format("A handler has already been registered for {0}.", messageTypeName), "TMessage");

            // Create a new message handler and add it to the list
            Action<MessageBase> messageHandler = message =>
            {
                var action1 = action;
                OnDocumentMessage(message, action1);
            };
            _messageHandlers.Add(messageTypeName, messageHandler);

            // Register the message handler
            DefaultMessenger.Register<TMessage>(this, token, true, messageHandler);

            // Add the message type to the ReceiveLinkData
            if (ReceiveLinkData != null)
                ReceiveLinkData.AddMessageType<TMessage>();
        }

        /// <summary>
        /// Unregisters receiving of a document message.
        /// </summary>
        /// <typeparam name="TMessage">The message type to unregister.</typeparam>
        /// <param name="token">An object that can be used to identify a specific message.</param>
        protected void UnregisterDocumentMessage<TMessage>(object token)
            where TMessage : MessageBase
        {
            // Attempt to remove the handler from the list, and throw an error if it didn't exist
            var messageTypeName = typeof(TMessage).FullName;
            if (!_messageHandlers.Remove(messageTypeName))
                throw new ArgumentException(string.Format("There is no handler registered for {0}.", messageTypeName), "TMessage");

            // Unregister the message
            DefaultMessenger.Unregister<TMessage>(this, token);

            // Remove the message type from the ReceiveLinkData
            if (ReceiveLinkData != null)
                ReceiveLinkData.RemoveMessageType<TMessage>();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Handles all document messages.
        /// </summary>
        /// <typeparam name="TMessage">The type of message being handled.</typeparam>
        /// <param name="message">The message that triggered this method.</param>
        /// <param name="action">The action to invoke on the derived class.</param>
        private void OnDocumentMessage<TMessage>(MessageBase message, Action<TMessage> action)
            where TMessage : MessageBase
        {
            // Abort if the widget is not allowing this message
            if (ReceiveLinkData == null || !ReceiveLinkData.IsMessageAllowed(message))
                return;

            // Abort if the message has limited receivers and this widget is not one of them
            if (message.Receivers != null && !message.Receivers.Any(o => Equals(o, this)))
                return;

            // Execute the action that was registered for this message
            action((TMessage)message);
        }

        /// <summary>
        /// Raises the WidgetLoaded event.
        /// </summary>
        private void RaiseWidgetLoaded()
        {
            // Abort if the widget hasn't been initialized or loaded
            if (!_widgetInitialized || !_contentLoaded)
                return;

            // Raise the WidgetLoaded event
            OnWidgetLoaded(new EventArgs());

            Task.Run(() =>
            {
                try
                {
                    // Wait for all startup tasks to complete
                    Task.WaitAll(_startupTasks.ToArray());
                }
                catch (AggregateException ex)
                {
                    if (PanelViewModel != null)
                    {
                        // Extract the error messages from the AggregateException
                        var errorMessage = string.Join("\r\n\r\n", ex.Flatten().InnerExceptions.Select(e => e.Message));

                        // Replace the widget with an error view
                        Application.Current.Dispatcher.Invoke(() =>
                            PanelViewModel.SetError("initializing", errorMessage)
                        );

                        // Hide the loading panel
                        IsLoadingPanelVisible = false;
                    }
                }

                // Notify that the startup tasks are complete
                Application.Current.Dispatcher.InvokeAsync(RaiseWidgetStarted, DispatcherPriority.ContextIdle);
            });
        }

        /// <summary>
        /// Raises the WidgetStarted event.
        /// </summary>
        private void RaiseWidgetStarted()
        {
            // Hide the loading panel
            IsLoadingPanelVisible = false;

            // Start listening for changes to the document
            IgnoreDocumentModifications = false;

            // Force the command manager to update commands, because the CanExecute methods will not be called after services connect
            CommandManager.InvalidateRequerySuggested();

            OnWidgetStarted(new EventArgs());
        }

        /// <summary>
        /// Raises the WidgetClosed event.
        /// </summary>
        private void RaiseWidgetClosed()
        {
            OnUnregisterDocumentMessages(_oldDocumentId);
            DeinitializeWidget();

            OnWidgetClosed(new EventArgs());
        }

        /// <summary>
        /// initializes the widget.
        /// </summary>
        private void InitializeWidget()
        {
            //System.Diagnostics.Debug.WriteLine("InitializeWidget {0}", new object[] { GetType().Name });

            // Attempt to get the PanelViewModel
            var panelViewModel = PanelViewModel;
            if (panelViewModel == null)
                return;

            // Attempt to get the DocumentViewModel
            var documentViewModel = ((ISupportParentViewModel)panelViewModel).ParentViewModel as DocumentViewModel;
            if (documentViewModel == null)
                return;

            // Create link data
            SendLinkData = new WidgetLinkData(WidgetLinkData.LinkDirections.Send, documentViewModel, panelViewModel);
            ReceiveLinkData = new WidgetLinkData(WidgetLinkData.LinkDirections.Receive, documentViewModel, panelViewModel);

            // Populate the SendLinkData with all message types that are sent by this widget
            var sendsDocumentMessageAttributes = GetType().GetCustomAttributes(typeof(SendsDocumentMessageAttribute), true);
            foreach (var sendsDocumentMessageAttribute in sendsDocumentMessageAttributes.Cast<SendsDocumentMessageAttribute>())
            {
                SendLinkData.AddMessageType(sendsDocumentMessageAttribute.MessageType);
            }

            // Create filter data if this widget supports it
            var supportAutoFilter = this as ISupportFilterData;
            if (supportAutoFilter != null)
            {
                FilterData = new WidgetFilterData(supportAutoFilter, documentViewModel);
                FilterData.Initialize();
            }

            // Create init data if this widget supports it
            var supportModelInit = this as ISupportModelIinitialization;
            if (supportModelInit != null)
            {
                ModelInitData = new WidgetModelInitData(documentViewModel);
            }

            // Handle events on the PanelViewModel
            panelViewModel.PanelClosed += PanelViewModel_PanelClosed;
            panelViewModel.ContentLoaded += PanelViewModel_ContentLoaded;

            // Handle events on the DocumentViewModel
            documentViewModel.DocumentLoaded += DocumentViewModel_DocumentLoaded;
            documentViewModel.DocumentClosing += DocumentViewModel_DocumentClosing;
            documentViewModel.PropertyChanged += DocumentViewModel_PropertyChanged;

            // Initialize the widget commands
            InitializeWidgetCommands();

            // Allow derived classes to register document messages
            OnDocumentChanged(documentViewModel);

            // If this widget was loaded into an existing document, then initialize the link data immediately
            // Otherwise it will get initialized later in the DocumentLoaded event
            if (!documentViewModel.IsLoadingPanelVisible)
                InitializeWidgetData();

            // Notify that the widget is loaded
            _widgetInitialized = true;
            RaiseWidgetLoaded();
        }

        /// <summary>
        /// Deinitializes the widget.
        /// </summary>
        private void DeinitializeWidget()
        {
            // Attempt to get the PanelViewModel
            var panelViewModel = PanelViewModel;
            if (panelViewModel == null)
                return;

            // Attempt to get the DocumentViewModel
            var documentViewModel = ((ISupportParentViewModel)panelViewModel).ParentViewModel as DocumentViewModel;
            if (documentViewModel == null)
                return;

            // Stop handling events on the PanelViewModel
            panelViewModel.PanelClosed -= PanelViewModel_PanelClosed;
            panelViewModel.ContentLoaded -= PanelViewModel_ContentLoaded;

            // Stop handling events on the DocumentViewModel
            documentViewModel.DocumentLoaded -= DocumentViewModel_DocumentLoaded;
            documentViewModel.DocumentClosing -= DocumentViewModel_DocumentClosing;
            documentViewModel.PropertyChanged -= DocumentViewModel_PropertyChanged;

            // Deinitialize widget links
            if (SendLinkData != null)
                SendLinkData.Deinitialize();
            if (ReceiveLinkData != null)
                ReceiveLinkData.Deinitialize();

            // Deinitialize widget filters
            if (FilterData != null)
                FilterData.Deinitialize();
        }

        /// <summary>
        /// Creates ribbon groups and items based for properties with a WidgetCommandAttribute.
        /// </summary>
        private void InitializeWidgetCommands()
        {
            // Get all properties and attributes that have a WidgetCommandAttribute
            var widgetCommands = GetType().GetProperties()
                .Where(p => p.IsDefined(typeof(WidgetCommandAttribute), true))
                .Select(p => new { Property = p, Attribute = (WidgetCommandAttribute)(p.GetCustomAttributes(typeof(WidgetCommandAttribute), true).Single()) })
                .ToList();

            // Process each widget command
            foreach (var widgetCommand in widgetCommands)
            {
                // Find or create a ribbon group to contain the command
                var groupViewModel = RibbonGroups.SingleOrDefault(g => g.Name == widgetCommand.Attribute.GroupName);
                if (groupViewModel == null)
                {
                    var groupDataModel = new RibbonGroup()
                    {
                        Oid = Guid.NewGuid(),
                        Name = widgetCommand.Attribute.GroupName
                    };
                    groupViewModel = Mapper.Map<RibbonGroupViewModel>(groupDataModel);
                    RibbonGroups.Add(groupViewModel);
                }

                // Create a ribbon item to represent the command
                var itemDataModel = new RibbonItem()
                {
                    Oid = Guid.NewGuid(),
                    Name = widgetCommand.Attribute.Name,
                    Description = widgetCommand.Attribute.Description,
                    ItemType = widgetCommand.Attribute.RibbonItemType,
                    CommandType = CommandType.WidgetCommand
                };
                groupViewModel.DataObject.RibbonItems.Add(itemDataModel);
                var itemViewModel = groupViewModel.RibbonItems.Single(i => ReferenceEquals(i.DataObject, itemDataModel));
                itemViewModel.Command = widgetCommand.Property.GetValue(this) as ICommand;
                itemViewModel.CommandParameter = widgetCommand.Attribute.CommandParameter;
            }
        }

        /// <summary>
        /// Initializes widget data.
        /// </summary>
        private void InitializeWidgetData()
        {
            // Initialize link data
            SendLinkData.Initialize();
            ReceiveLinkData.Initialize();

            // Attempt to get the combined data
            var combinedStream = GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.WidgetData, GetType().FullName);
            if (combinedStream == null)
                return;

            // Read the combined data as a JArray
            using (var reader = new StreamReader(combinedStream))
            {
                var combinedObject = JToken.ReadFrom(new JsonTextReader(reader)) as JArray;
                if (combinedObject == null)
                    return;

                // Deserialize each of the items in the JArray
                foreach (var jObject in combinedObject.Cast<JObject>())
                {
                    switch (jObject.Value<string>("Type"))
                    {
                        case "SendLinkData":
                            SendLinkData.DeserializeFromJsonObject(jObject);
                            break;

                        case "ReceiveLinkData":
                            ReceiveLinkData.DeserializeFromJsonObject(jObject);
                            break;

                        case "FilterData":
                            FilterData.DeserializeFromJsonObject(jObject);
                            break;

                        case "InitData":
                            ModelInitData.DeserializeFromJsonObject(jObject);
                            break;
                    }
                }
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the DocumentModifiedCommand.
        /// </summary>
        private void OnDocumentModifiedExecute()
        {
            IsDocumentModified = true;
        }

        /// <summary>
        /// Handles the ContentLoaded event for the Panel ViewModel.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void PanelViewModel_ContentLoaded(object sender, EventArgs e)
        {
            _contentLoaded = true;
            RaiseWidgetLoaded();
        }

        /// <summary>
        /// Handles the PanelClosed event for the Panel ViewModel.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void PanelViewModel_PanelClosed(object sender, EventArgs e)
        {
            RaiseWidgetClosed();
        }

        /// <summary>
        /// Handles the DocumentLoaded event for the Document ViewModel.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DocumentViewModel_DocumentLoaded(object sender, EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("DocumentViewModel_DocumentLoaded {0}", new object[] { GetType().Name });

            IsLoadingPanelVisible = true;
            InitializeWidgetData();
        }

        /// <summary>
        /// Handles the DocumentClosing event for the Document ViewModel.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DocumentViewModel_DocumentClosing(object sender, CancelEventArgs e)
        {
            RaiseWidgetClosed();
        }

        /// <summary>
        /// Handles the PropertyChanged event for the Document ViewModel.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DocumentViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If the Document Oid or DocumentId has changed, notify that the root document has changed
            if (e.PropertyName == "Oid" || e.PropertyName == "DocumentId")
                OnDocumentChanged(DocumentViewModel);
        }

        #endregion


        #region Overrides

        protected override void OnParentViewModelChanged(object parentViewModel)
        {
            base.OnParentViewModelChanged(parentViewModel);

            // If the parent viewmodel has changed, then notify that the PanelViewModel and DocumentViewModel are now available
            RaisePropertyChanged(() => PanelViewModel);
            RaisePropertyChanged(() => DocumentViewModel);

            // Set IgnoreDocumentModifications = true for each widget in the document, so that modifications won't be tracked until after each widget has started and called IgnoreDocumentModifications = false
            IgnoreDocumentModifications = true;

            // Once the widget has been placed in a document, we can attempt to initialize it
            InitializeWidget();
        }

        #endregion
    }
}
