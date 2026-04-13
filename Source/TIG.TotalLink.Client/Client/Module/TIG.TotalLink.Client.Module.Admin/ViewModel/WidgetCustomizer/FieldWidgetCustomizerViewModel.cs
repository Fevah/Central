using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.WidgetCustomizer
{
    [WidgetCustomizer("Fields", 100)]
    public class FieldWidgetCustomizerViewModel : WidgetCustomizerViewModelBase
    {
        #region Private Fields

        private readonly DataLayoutControlEx _dataLayoutControl;
        private bool _isEnabled;

        #endregion


        #region Constructors

        public FieldWidgetCustomizerViewModel()
        {
        }

        public FieldWidgetCustomizerViewModel(DataLayoutControlEx dataLayoutControl)
            : this()
        {
            // Initialize properties
            _dataLayoutControl = dataLayoutControl;

            // Handle DataLayoutControl events
            _dataLayoutControl.CurrentItemChanged += DataLayoutControl_CurrentItemChanged;

            // Initialize commands
            SetCustomizationControlCommand = new DelegateCommand<LayoutControlCustomizationControl>(OnSetCustomizationControlExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to set the ExternalCustomizationControl from the view.
        /// </summary>
        public ICommand SetCustomizationControlCommand { get; set; }


        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if this customizer is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
            private set { SetProperty(ref _isEnabled, value, () => IsEnabled); }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the SetCustomizationControlCommand.
        /// </summary>
        /// <param name="customizationControl">The LayoutControlCustomizationControl to assign.</param>
        private void OnSetCustomizationControlExecute(LayoutControlCustomizationControl customizationControl)
        {
            //System.Diagnostics.Debug.WriteLine("OnSetCustomizationControlExecute");

            // Make sure customization is disabled before assigning the ExternalCustomizationControl
            // This ensures that the customization control is re-initialized if the parent panel is docked or undocked
            _dataLayoutControl.IsCustomization = false;

            // Assign the ExternalCustomizationControl
            _dataLayoutControl.ExternalCustomizationControl = customizationControl;

            // Now that the ExternalCustomizationControl has been set, it's safe to apply the real IsCustomization value
            _dataLayoutControl.IsCustomization = IsCustomization;
        }

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

            // Return a new FieldWidgetCustomizerViewModel
            return new FieldWidgetCustomizerViewModel(dataLayoutControl);
        }

        public override void OnWidgetClosed()
        {
            base.OnWidgetClosed();

            // Stop handle DataLayoutControl events
            _dataLayoutControl.CurrentItemChanged -= DataLayoutControl_CurrentItemChanged;
        }

        protected override void OnIsCustomizationChanged()
        {
            base.OnIsCustomizationChanged();

            // Apply IsCustomization to the DataLayoutControl, only if the ExternalCustomizationControl has already been set
            // Otherwise IsCustomization will be applied in OnSetCustomizationControlExecute after the ExternalCustomizationControl has been set
            if (_dataLayoutControl.ExternalCustomizationControl != null)
                _dataLayoutControl.IsCustomization = IsCustomization;
        }

        #endregion
    }
}
