using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.WidgetCustomizer
{
    [WidgetCustomizer("Form", 200)]
    public class FormWidgetCustomizerViewModel : WidgetCustomizerViewModelBase
    {
        #region Private Fields

        private readonly DataLayoutControlEx _dataLayoutControl;
        private readonly ISupportLayoutData _widget;
        private object _currentItem;
        private bool _isEnabled;

        #endregion


        #region Constructors

        public FormWidgetCustomizerViewModel()
        {
        }

        public FormWidgetCustomizerViewModel(DataLayoutControlEx dataLayoutControl, ISupportLayoutData widget)
            : this()
        {
            // Display this viewmodel in the DataLayoutControl
            CurrentItem = this;

            // Initialize properties
            _dataLayoutControl = dataLayoutControl;
            _widget = widget;

            // Initialize commands
            RestoreDefaultLayoutCommand = new DelegateCommand(OnRestoreDefaultLayoutExecute);
            RestoreSavedLayoutCommand = new DelegateCommand(OnRestoreSavedLayoutExecute);

            // Handle DataLayoutControl events
            _dataLayoutControl.CurrentItemChanged += DataLayoutControl_CurrentItemChanged;
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to restore the default form layout.
        /// </summary>
        public ICommand RestoreDefaultLayoutCommand { get; private set; }

        /// <summary>
        /// Command to restore the last saved form layout.
        /// </summary>
        public ICommand RestoreSavedLayoutCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if this customizer is enabled.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsEnabled
        {
            get { return _isEnabled; }
            private set { SetProperty(ref _isEnabled, value, () => IsEnabled); }
        }

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
        /// Indicates if the header is displayed at the top of the form.
        /// </summary>
        public bool ShowHeader
        {
            get { return _dataLayoutControl.ShowHeader; }
            set { _dataLayoutControl.ShowHeader = value; }
        }

        /// <summary>
        /// Indicates if colons are displayed after item labels.
        /// </summary>
        public bool ShowColonAfterLabels
        {
            get { return _dataLayoutControl.AddColonToItemLabels; }
            set { _dataLayoutControl.AddColonToItemLabels = value; }
        }

        /// <summary>
        /// Indicates if the user can move items.
        /// </summary>
        public bool AllowItemMoving
        {
            get { return _dataLayoutControl.AllowItemMovingDuringCustomization; }
            set { _dataLayoutControl.AllowItemMovingDuringCustomization = value; }
        }

        /// <summary>
        /// Indicates if the user can rename items.
        /// </summary>
        public bool AllowItemRenaming
        {
            get { return _dataLayoutControl.AllowItemRenamingDuringCustomization; }
            set { _dataLayoutControl.AllowItemRenamingDuringCustomization = value; }
        }

        /// <summary>
        /// Indicates if the user can resize items.
        /// </summary>
        public bool AllowItemSizing
        {
            get { return _dataLayoutControl.AllowItemSizingDuringCustomization; }
            set { _dataLayoutControl.AllowItemSizingDuringCustomization = value; }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the RestoreDefaultLayout command.
        /// </summary>
        private void OnRestoreDefaultLayoutExecute()
        {
            _widget.ApplyDefaultLayout();
            RefreshAllProperties();
        }

        /// <summary>
        /// Execute method for the RestoreSavedLayout command.
        /// </summary>
        private void OnRestoreSavedLayoutExecute()
        {
            _widget.ApplySavedLayout();
            RefreshAllProperties();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Refreshes all property bindings.
        /// </summary>
        private void RefreshAllProperties()
        {
            RaisePropertyChanged(() => ShowColonAfterLabels);
            RaisePropertyChanged(() => AllowItemMoving);
            RaisePropertyChanged(() => AllowItemRenaming);
            RaisePropertyChanged(() => AllowItemSizing);
            RaisePropertyChanged(() => ShowHeader);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the DataLayoutControl.CurrentItemChanged event.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataLayoutControl_CurrentItemChanged(object sender, ValueChangedEventArgs<object> e)
        {
            IsEnabled = (e.NewValue != null);
        }

        #endregion


        #region Overrides

        public new static WidgetCustomizerViewModelBase CreateCustomizer(FrameworkElement content, WidgetViewModelBase widget)
        {
            // Attempt to find a DataLayoutControlEx within the content
            var dataLayoutControl = LayoutHelper.FindElementByType<DataLayoutControlEx>(content);
            if (dataLayoutControl == null)
                return null;

            // Return a new FormWidgetCustomizerViewModel
            return new FormWidgetCustomizerViewModel(dataLayoutControl, widget as ISupportLayoutData);
        }

        public override void OnWidgetClosed()
        {
            base.OnWidgetClosed();

            // Stop handle DataLayoutControl events
            _dataLayoutControl.CurrentItemChanged -= DataLayoutControl_CurrentItemChanged;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<FormWidgetCustomizerViewModel> builder)
        {
            builder.DataFormLayout()
                .GroupBox("Layout")
                    .ContainsProperty(p => p.RestoreDefaultLayoutCommand)
                    .ContainsProperty(p => p.RestoreSavedLayoutCommand)
                .EndGroup()
                .GroupBox("Elements")
                    .ContainsProperty(p => p.ShowHeader)
                    .ContainsProperty(p => p.ShowColonAfterLabels)
                .EndGroup()
                .GroupBox("Features")
                    .ContainsProperty(p => p.AllowItemMoving)
                    .ContainsProperty(p => p.AllowItemSizing)
                    .ContainsProperty(p => p.AllowItemRenaming);

            builder.Property(p => p.RestoreDefaultLayoutCommand)
                .DisplayName("Restore Default")
                .Description("Restore the default layout for this form.");
            builder.Property(p => p.RestoreSavedLayoutCommand)
                .DisplayName("Restore Saved")
                .Description("Restore the last saved layout for this form.");

            builder.Property(p => p.ShowHeader)
                .DisplayName("Header")
                .Description("Show the header at the top of the form.");
            builder.Property(p => p.ShowColonAfterLabels)
                .DisplayName("Colon After Labels")
                .Description("Show colons after labels on the form.");

            builder.Property(p => p.AllowItemMoving)
                .DisplayName("Allow Item Moving")
                .Description("Allow items to be moved.");
            builder.Property(p => p.AllowItemSizing)
                .DisplayName("Allow Item Resizing")
                .Description("Allow items to be resized.");
            builder.Property(p => p.AllowItemRenaming)
                .DisplayName("Allow Item Renaming")
                .Description("Allow items to be renamed.");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<FormWidgetCustomizerViewModel> builder)
        {
            builder.Property(p => p.RestoreDefaultLayoutCommand)
                .HideLabel();
            builder.Property(p => p.RestoreSavedLayoutCommand)
                .HideLabel();
        }

        #endregion
    }
}
