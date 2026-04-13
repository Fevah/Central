using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using DevExpress.Mvvm;
using Newtonsoft.Json.Linq;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class WidgetLinkData : BindableBase
    {
        #region Public Enums

        public enum LinkModes
        {
            AllWidgetsInThisDocument,
            SelectedWidgetsOnly
        }

        public enum LinkDirections
        {
            Send,
            Receive
        }

        #endregion


        #region Private Fields

        private readonly DocumentViewModel _parentDocument;
        private readonly PanelViewModel _parentPanel;
        private bool _hasMessageTypes;
        private bool _hasSelectedLinks;
        private LinkModes _linkMode;

        #endregion


        #region Constructors

        public WidgetLinkData(LinkDirections linkDirection, DocumentViewModel parentDocument, PanelViewModel parentPanel)
        {
            LinkDirection = linkDirection;
            _parentDocument = parentDocument;
            _parentPanel = parentPanel;

            // Initialize collections
            MessageTypes = new ObservableCollection<MessageTypeWrapper>();
            SelectedLinks = new ObservableCollection<WidgetLink>();
            AvailableLinks = new ObservableCollection<WidgetLink>();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of the message types that this link data manages.
        /// </summary>
        public ObservableCollection<MessageTypeWrapper> MessageTypes { get; private set; }

        /// <summary>
        /// Indicates if this link data is currently managing any message types.
        /// </summary>
        public bool HasMessageTypes
        {
            get { return _hasMessageTypes; }
            private set { SetProperty(ref _hasMessageTypes, value, () => HasMessageTypes); }
        }

        /// <summary>
        /// Indicates if this link data contains any selected links.
        /// </summary>
        public bool HasSelectedLinks
        {
            get { return _hasSelectedLinks; }
            private set { SetProperty(ref _hasSelectedLinks, value, () => HasSelectedLinks); }
        }

        /// <summary>
        /// Specifies which direction this link data represents.
        /// </summary>
        public LinkDirections LinkDirection { get; private set; }

        /// <summary>
        /// Specifies the mode to use when processing messages.
        /// </summary>
        public LinkModes LinkMode
        {
            get { return _linkMode; }
            set { SetProperty(ref _linkMode, value, () => LinkMode, () => _parentDocument.IsModified = true); }
        }

        /// <summary>
        /// A list of panels that this link data will allow messages from when LinkMode = SelectedWidgetsOnly.
        /// </summary>
        public ObservableCollection<WidgetLink> SelectedLinks { get; private set; }

        /// <summary>
        /// A list of panels that manage the same messages as this widget.
        /// </summary>
        public ObservableCollection<WidgetLink> AvailableLinks { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the CollectionChanged event for the parent document Panels collection.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Panels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var newItem in e.NewItems)
                        AddLink((PanelViewModel)newItem);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var oldItem in e.OldItems)
                        RemoveLink((PanelViewModel)oldItem);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    ClearLinks();
                    break;
            }
        }

        /// <summary>
        /// Handles the CollectionChanged event for the SelectedLinks collection.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedLinks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _parentDocument.IsModified = true;
            HasSelectedLinks = (SelectedLinks.Count > 0);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Starts this link data managing a message type.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to start managing.</typeparam>
        public void AddMessageType<TMessage>()
            where TMessage : MessageBase
        {
            AddMessageType(typeof(TMessage));
        }

        /// <summary>
        /// Starts this link data managing a message type.
        /// </summary>
        /// <param name="messageType">The type of message to start managing.</param>
        public void AddMessageType(Type messageType)
        {
            // Add the message type to the list
            MessageTypes.Add(new MessageTypeWrapper(messageType));

            // Set the flag to indicate that this link data not contains some messages
            HasMessageTypes = true;
        }

        /// <summary>
        /// Stops this link data managing a message type.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to stop managing.</typeparam>
        public void RemoveMessageType<TMessage>()
            where TMessage : MessageBase
        {
            RemoveMessageType(typeof(TMessage));
        }

        /// <summary>
        /// Stops this link data managing a message type.
        /// </summary>
        /// <param name="messageType">The type of message to stop managing.</param>
        public void RemoveMessageType(Type messageType)
        {
            // Remove the message type from the list
            var messageTypeWrapper = MessageTypes.FirstOrDefault(m => m.MessageType == messageType);
            if (messageTypeWrapper != null)
                MessageTypes.Remove(messageTypeWrapper);

            // If there are no more messages, set the flag to indicate that this link data no longer contains any messages
            if (MessageTypes.Count == 0)
                HasMessageTypes = false;
        }

        /// <summary>
        /// Initializes the link data.
        /// </summary>
        public void Initialize()
        {
            // Start managing all existing panels
            foreach (var panel in _parentDocument.Panels)
                AddLink(panel);

            // Handle events
            _parentDocument.Panels.CollectionChanged += Panels_CollectionChanged;
            SelectedLinks.CollectionChanged += SelectedLinks_CollectionChanged;
        }

        /// <summary>
        /// Deinitializes the link data.
        /// </summary>
        public void Deinitialize()
        {
            // Stop handling events
            _parentDocument.Panels.CollectionChanged -= Panels_CollectionChanged;
            SelectedLinks.CollectionChanged -= SelectedLinks_CollectionChanged;
        }

        /// <summary>
        /// Indicates if the specified message should be allowed based on the current linking settings.
        /// </summary>
        /// <param name="message">The message to test.</param>
        /// <returns>True if the message should be allowed; otherwise false.</returns>
        public bool IsMessageAllowed(MessageBase message)
        {
            // Allow the message if this widget is accepting all messages
            if (LinkMode == LinkModes.AllWidgetsInThisDocument)
                return true;

            // If the message didn't come from a WidgetViewModelBase then we can't filter it, so just accept it
            var widget = message.Sender as WidgetViewModelBase;
            if (widget == null)
                return true;

            // Allow the message if the source is included in SelectedLinks
            return SelectedLinks.Any(l => l.IsLinkedTo(widget.PanelOid));
        }

        /// <summary>
        /// Returns a JObject which represents this link data.
        /// </summary>
        /// <returns>A JObject which represents this link data.</returns>
        public JObject SerializeToJsonObject()
        {
            Clean();

            // Create a json object that represents this link data
            return new JObject(
                new JProperty("Type", string.Format("{0}LinkData", LinkDirection)),
                new JProperty("LinkMode", LinkMode),
                new JProperty("SelectedLinks",
                    new JArray(
                        SelectedLinks.Select(
                            l => new JObject(
                                new JProperty("Id", l.LinkId),
                                new JProperty("Name", l.Name)
                            )
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Deserializes this link data from a JObject.
        /// </summary>
        /// <param name="jObject">The JObject to deserialize from.</param>
        public void DeserializeFromJsonObject(JObject jObject)
        {
            // Abort if the jobject is null
            if (jObject == null)
                return;

            // Parse basic properties
            LinkMode = (LinkModes)((int)jObject["LinkMode"]);

            // Parse the SelectedLinks array
            SelectedLinks.Clear();
            foreach (var jsonLink in jObject["SelectedLinks"].Children().Cast<JObject>())
            {
                var idString = (string)jsonLink["Id"];
                WidgetLink link = null;

                // If the jsonLink contains an id, attempt to find the link that it refers to
                var id = Guid.Empty;
                if (idString != null)
                {
                    if (Guid.TryParseExact(idString, "B", out id))
                    {
                        link = AvailableLinks.FirstOrDefault(l => l.IsLinkedTo(id));
                        if (link != null)
                            SelectedLinks.Add(link);
                    }
                }

                // If we found a link, move on to the next item
                if (link != null)
                    continue;

                // If the link could not be found for any reason, add a broken link
                var name = (string)jsonLink["Name"];
                link = new WidgetLink(name, id);
                AvailableLinks.Add(link);
                SelectedLinks.Add(link);
            }
        }

        /// <summary>
        /// Cleans the link data by removing any broken links that are no longer selected.
        /// </summary>
        public void Clean()
        {
            foreach (var brokenLink in AvailableLinks.Where(l => l.IsBroken && !SelectedLinks.Contains(l)).ToList())
            {
                AvailableLinks.Remove(brokenLink);
            }
        }

        /// <summary>
        /// Refreshes the link data.
        /// </summary>
        public void Refresh()
        {
            // Make sure all relevant panels are being managed
            foreach (var panel in _parentDocument.Panels)
                AddLink(panel);

            // Remove any panels that are no longer relevant
            for (var i = AvailableLinks.Count - 1; i > -1; i--)
            {
                var availableLink = AvailableLinks[i];
                var widget = availableLink.GetWidget();

                if (widget != null)
                {
                    // If this link represents Send messages, then abort if the target widget has matching Receive messages
                    if (LinkDirection == LinkDirections.Send && widget.ReceiveLinkData.MessageTypes.Intersect(MessageTypes).Any())
                        continue;

                    // If this link represents Receive messages, then abort if the target widget has matching Send messages
                    if (LinkDirection == LinkDirections.Receive && widget.SendLinkData.MessageTypes.Intersect(MessageTypes).Any())
                        continue;
                }

                // If we get this far then the target widget does not have matching message types, so the link should be removed

                // If the link is in SelectedLinks, then just break the link
                // (Removing and re-adding the item ensures that the list entry label is updated)
                if (SelectedLinks.Contains(availableLink))
                {
                    AvailableLinks.RemoveAt(i);
                    availableLink.Break();
                    AvailableLinks.Insert(i, availableLink);
                    SelectedLinks.Add(availableLink);
                    continue;
                }

                // Remove the panel from AvailableLinks
                AvailableLinks.RemoveAt(i);
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Adds a panel that can be linked to.
        /// </summary>
        /// <param name="panel">The panel that can be linked to.</param>
        private void AddLink(PanelViewModel panel)
        {
            // Abort if the panel is the one that contains this widget
            if (Equals(panel, _parentPanel))
                return;

            // If the panel is already in the AvailableLinks, and is broken, re-attach it
            // (Removing and re-adding the item ensures that the list entry label is updated)
            var existingLink = AvailableLinks.FirstOrDefault(l => l.IsLinkedTo(panel));
            if (existingLink != null)
            {
                if (existingLink.IsBroken)
                {
                    var existingIndex = AvailableLinks.IndexOf(existingLink);
                    AvailableLinks.RemoveAt(existingIndex);
                    existingLink.ReAttach(panel);
                    AvailableLinks.Insert(existingIndex, existingLink);
                    SelectedLinks.Add(existingLink);
                }

                return;
            }

            // Attempt to get the panel content as a FrameworkElement
            var panelContent = panel.Content as FrameworkElement;
            if (panelContent == null)
                return;

            // Attempt to get the panel DataContext as a WidgetViewModelBase
            var widget = panelContent.DataContext as WidgetViewModelBase;
            if (widget == null)
                return;

            // If this link represents Send messages, then abort if the target widget doesn't have any matching Receive messages
            if (LinkDirection == LinkDirections.Send && !widget.ReceiveLinkData.MessageTypes.Intersect(MessageTypes).Any())
                return;

            // If this link represents Receive messages, then abort if the target widget doesn't have any matching Send messages
            if (LinkDirection == LinkDirections.Receive && !widget.SendLinkData.MessageTypes.Intersect(MessageTypes).Any())
                return;

            // If we get this far then the target panel contains a valid widget, and that widget has some matching messages, so add the panel to AvailableLinks
            AvailableLinks.Add(new WidgetLink(panel));
        }

        /// <summary>
        /// Removes a panel that can be linked to.
        /// </summary>
        /// <param name="panel">The panel that can be linked to.</param>
        private void RemoveLink(PanelViewModel panel)
        {
            // If the link is in SelectedLinks, then just break the link instead of removing it
            var selectedLink = SelectedLinks.FirstOrDefault(l => l.IsLinkedTo(panel));
            if (selectedLink != null)
            {
                selectedLink.Break();
                return;
            }

            // Remove the panel from AvailableLinks
            var availableLink = AvailableLinks.FirstOrDefault(l => l.IsLinkedTo(panel));
            if (availableLink != null)
                AvailableLinks.Remove(availableLink);
        }

        /// <summary>
        /// Clears all panels that can be linked to.
        /// </summary>
        private void ClearLinks()
        {
            AvailableLinks.Clear();
        }

        #endregion
    }
}
