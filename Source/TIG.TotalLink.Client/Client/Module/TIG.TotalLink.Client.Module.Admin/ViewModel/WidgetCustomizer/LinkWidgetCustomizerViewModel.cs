using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.WidgetCustomizer
{
    [WidgetCustomizer("Link", 400)]
    public class LinkWidgetCustomizerViewModel : WidgetCustomizerViewModelBase
    {
        #region Private Fields

        private static readonly ICommand _mouseEnterLinkCommand = new DelegateCommand<WidgetLink>(OnMouseEnterLinkExecute);
        private static readonly ICommand _mouseLeaveLinkCommand = new DelegateCommand<WidgetLink>(OnMouseLeaveLinkExecute);
        private readonly WidgetViewModelBase _widget;
        private object _currentItem;

        #endregion


        #region Constructors

        public LinkWidgetCustomizerViewModel()
        {
        }

        public LinkWidgetCustomizerViewModel(WidgetViewModelBase widget)
            : this()
        {
            // Display this viewmodel in the DataLayoutControl
            CurrentItem = this;

            // Initialize properties
            _widget = widget;

            // Handle events
            if (_widget.SendLinkData != null)
                _widget.SendLinkData.PropertyChanged += SendLinkData_PropertyChanged;
            if (_widget.ReceiveLinkData != null)
                _widget.ReceiveLinkData.PropertyChanged += ReceiveLinkData_PropertyChanged;
        }

        #endregion


        #region Commands
        
        /// <summary>
        /// Command that is executed when the mouse enters a link in either of the SelectedLinks lists.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public static ICommand MouseEnterLinkCommand
        {
            get { return _mouseEnterLinkCommand; }
        }

        /// <summary>
        /// Command that is executed when the mouse leaves a link in either of the SelectedLinks lists.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public static ICommand MouseLeaveLinkCommand
        {
            get { return _mouseLeaveLinkCommand; }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The object being displayed in the DataLayoutControl.
        /// This will automatically be initialized to contain a reference to this viewmodel.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public object CurrentItem
        {
            get { return _currentItem; }
            set { SetProperty(ref _currentItem, value, () => CurrentItem); }
        }

        /// <summary>
        /// Indicates if the loading panel is visible.  Always false.
        /// Since the LocalDetailView is usually used directly in a widget, it contains a WidgetLoadingPanelView which will attempt to bind to this property.
        /// Therefore we include this property definition to avoid binding errors.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsLoadingPanelVisible
        {
            get { return false; }
        }

        /// <summary>
        /// A list of the message types that this widget sends.
        /// </summary>
        public ObservableCollection<MessageTypeWrapper> SentMessageTypes
        {
            get { return _widget.SendLinkData.MessageTypes; }
        }

        /// <summary>
        /// Indicates if the widget sends any document messages.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool SendsMessages
        {
            get { return _widget.SendLinkData.HasMessageTypes || _widget.SendLinkData.HasSelectedLinks; }
        }

        /// <summary>
        /// Specifies the mode to use when sending document messages.
        /// </summary>
        public WidgetLinkData.LinkModes SendLinkMode
        {
            get { return _widget.SendLinkData.LinkMode; }
            set
            {
                _widget.SendLinkData.LinkMode = value;
                RaisePropertyChanged(() => SendLinkMode);
            }
        }

        /// <summary>
        /// A list of widgets that this widget will send document messages to when SendLinkMode = SelectedWidgetsOnly.
        /// </summary>
        public ObservableCollection<WidgetLink> SendSelectedLinks
        {
            get { return _widget.SendLinkData.SelectedLinks; }
        }

        /// <summary>
        /// A list of all widgets that receive document messages that this widget can send.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public ObservableCollection<WidgetLink> SendAvailableLinks
        {
            get { return _widget.SendLinkData.AvailableLinks; }
        }

        /// <summary>
        /// A list of the message types that this widget receives.
        /// </summary>
        public ObservableCollection<MessageTypeWrapper> ReceivedMessageTypes
        {
            get { return _widget.ReceiveLinkData.MessageTypes; }
        }

        /// <summary>
        /// Indicates if the widget receives any document messages.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool ReceivesMessages
        {
            get { return _widget.ReceiveLinkData.HasMessageTypes || _widget.ReceiveLinkData.HasSelectedLinks; }
        }

        /// <summary>
        /// Specifies the mode to use when receiving document messages.
        /// </summary>
        public WidgetLinkData.LinkModes ReceiveLinkMode
        {
            get { return _widget.ReceiveLinkData.LinkMode; }
            set
            {
                _widget.ReceiveLinkData.LinkMode = value;
                RaisePropertyChanged(() => ReceiveLinkMode);
            }
        }

        /// <summary>
        /// A list of widgets that this widget will accept document messages from when ReceiveLinkMode = SelectedWidgetsOnly.
        /// </summary>
        public ObservableCollection<WidgetLink> ReceiveSelectedLinks
        {
            get { return _widget.ReceiveLinkData.SelectedLinks; }
        }

        /// <summary>
        /// A list of all widgets that send document messages that this widget can receive.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public ObservableCollection<WidgetLink> ReceiveAvailableLinks
        {
            get { return _widget.ReceiveLinkData.AvailableLinks; }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sets the highlight state of a widget that a link points to.
        /// </summary>
        /// <param name="link">The link containing the widget to highlight.</param>
        /// <param name="isHighlighted">The highlighted state to set.</param>
        private static void SetWidgetHighlight(WidgetLink link, bool isHighlighted)
        {
            // Attempt to get the widget that the link points to
            var widget = link.GetWidget();
            if (widget == null)
                return;

            // Highlight the widget
            widget.IsHighlighted = isHighlighted;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the MouseEnterLinkCommand.
        /// </summary>
        /// <param name="link">The link that the mouse has entered.</param>
        private static void OnMouseEnterLinkExecute(WidgetLink link)
        {
            SetWidgetHighlight(link, true);
        }

        /// <summary>
        /// Execute method for the MouseLeaveLinkCommand.
        /// </summary>
        /// <param name="link">The link that the mouse has entered.</param>
        private static void OnMouseLeaveLinkExecute(WidgetLink link)
        {
            SetWidgetHighlight(link, false);
        }

        /// <summary>
        /// Handles the PropertyChanged event on the Widget.SendLinkData.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SendLinkData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If SendLinkData.HasMessageTypes or SendLinkData.HasSelectedLinks changes, notify that SendsMessages has also changed
            if (e.PropertyName == "HasMessageTypes" || e.PropertyName == "HasSelectedLinks")
                RaisePropertyChanged(() => SendsMessages);
        }

        /// <summary>
        /// Handles the PropertyChanged event on the Widget.ReceiveLinkData.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ReceiveLinkData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If ReceiveLinkData.HasMessageTypes or ReceiveLinkData.HasSelectedLinks changes, notify that ReceivesMessages has also changed
            if (e.PropertyName == "HasMessageTypes" || e.PropertyName == "HasSelectedLinks")
                RaisePropertyChanged(() => ReceivesMessages);
        }

        #endregion


        #region Overrides

        public new static WidgetCustomizerViewModelBase CreateCustomizer(FrameworkElement content, WidgetViewModelBase widget)
        {
            // If the widget sends or receives document messages...
            if ((widget.SendLinkData != null && (widget.SendLinkData.HasMessageTypes || widget.SendLinkData.HasSelectedLinks)) || (widget.ReceiveLinkData != null && (widget.ReceiveLinkData.HasMessageTypes || widget.ReceiveLinkData.HasSelectedLinks)))
            {
                // Add a LinkWidgetCustomizerViewModel
                return new LinkWidgetCustomizerViewModel(widget);
            }

            // Otherwise, return null
            return null;
        }

        public override void OnWidgetClosed()
        {
            base.OnWidgetClosed();

            // Stop handling events
            if (_widget.SendLinkData != null)
                _widget.SendLinkData.PropertyChanged -= SendLinkData_PropertyChanged;
            if (_widget.ReceiveLinkData != null)
                _widget.ReceiveLinkData.PropertyChanged -= ReceiveLinkData_PropertyChanged;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<LinkWidgetCustomizerViewModel> builder)
        {
            builder.DataFormLayout()
                .GroupBox("Send")
                    .ContainsProperty(p => p.SendLinkMode)
                    .ContainsProperty(p => p.SendSelectedLinks)
                    .ContainsProperty(p => p.SentMessageTypes)
                .EndGroup()
                .GroupBox("Receive")
                    .ContainsProperty(p => p.ReceiveLinkMode)
                    .ContainsProperty(p => p.ReceiveSelectedLinks)
                    .ContainsProperty(p => p.ReceivedMessageTypes);

            builder.Property(p => p.SendLinkMode)
                .DisplayName("Send Messages To")
                .Description("Specifies which widgets this widget will send messages to.");
            builder.Property(p => p.SendSelectedLinks)
                .AutoGenerated()
                .Description("A list of all widgets that receive messages this widget sends.");
            builder.Property(p => p.SentMessageTypes)
                .AutoGenerated()
                .DisplayName("Sent Message Types")
                .Description("A list of message types that this widget can send.");

            builder.Property(p => p.ReceiveLinkMode)
                .DisplayName("Receive Messages From")
                .Description("Specifies which widgets this widget will receive messages from.");
            builder.Property(p => p.ReceiveSelectedLinks)
                .AutoGenerated()
                .Description("A list of all widgets that send messages this widget receives.");
            builder.Property(p => p.ReceivedMessageTypes)
                .AutoGenerated()
                .DisplayName("Received Message Types")
                .Description("A list of message types that this widget can receive.");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<LinkWidgetCustomizerViewModel> builder)
        {
            builder.Condition(i => i != null && i.SendsMessages)
                .ContainsProperty(p => p.SendsMessages)
                .AffectsGroupVisibility("Send");

            builder.Condition(i => i != null && i.ReceivesMessages)
                .ContainsProperty(p => p.ReceivesMessages)
                .AffectsGroupVisibility("Receive");

            builder.Condition(i => i != null && i.SendLinkMode == WidgetLinkData.LinkModes.SelectedWidgetsOnly)
                .ContainsProperty(p => p.SendLinkMode)
                .AffectsPropertyEnabled(p => p.SendSelectedLinks);

            builder.Condition(i => i != null && i.ReceiveLinkMode == WidgetLinkData.LinkModes.SelectedWidgetsOnly)
                .ContainsProperty(p => p.ReceiveLinkMode)
                .AffectsPropertyEnabled(p => p.ReceiveSelectedLinks);

            builder.Property(p => p.SendLinkMode)
                .ReplaceEditor(new OptionEditorDefinition(typeof(WidgetLinkData.LinkModes)))
                .LabelPosition(LayoutItemLabelPosition.Top);
            builder.Property(p => p.SendSelectedLinks)
                .ReplaceEditor(new CheckedListBoxEditorDefinition()
                {
                    ItemsSourcePropertyName = "SendAvailableLinks",
                    MouseEnterItemCommand = MouseEnterLinkCommand,
                    MouseLeaveItemCommand = MouseLeaveLinkCommand
                })
                .HideLabel();
            builder.Property(p => p.SentMessageTypes)
                .LabelPosition(LayoutItemLabelPosition.Top);

            builder.Property(p => p.ReceiveLinkMode)
                .ReplaceEditor(new OptionEditorDefinition(typeof(WidgetLinkData.LinkModes)))
                .LabelPosition(LayoutItemLabelPosition.Top);
            builder.Property(p => p.ReceiveSelectedLinks)
                .ReplaceEditor(new CheckedListBoxEditorDefinition()
                {
                    ItemsSourcePropertyName = "ReceiveAvailableLinks",
                    MouseEnterItemCommand = MouseEnterLinkCommand,
                    MouseLeaveItemCommand = MouseLeaveLinkCommand
                })
                .HideLabel();
            builder.Property(p => p.ReceivedMessageTypes)
                .LabelPosition(LayoutItemLabelPosition.Top);
        }

        #endregion
    }
}
