using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.Provider;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using DataObjectBase = TIG.TotalLink.Shared.DataModel.Core.DataObjectBase;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.WidgetCustomizer
{
    [WidgetCustomizer("Data", 300)]
    public class DataWidgetCustomizerViewModel : WidgetCustomizerViewModelBase
    {
        #region Private Fields

        private static IDataModelTypeProvider _dataModelTypeProvider;
        private readonly DataLayoutControlEx _dataLayoutControl;
        private readonly WidgetViewModelBase _widget;
        private object _currentItem;
        private DataModelTypeViewModelBase _selectedDataModelType;
        private WidgetModelInit _selectedModelInit;
        private PropertyViewModel _selectedProperty;
        private bool _populatingAvailableProperties;
        private ICommand _reInitializeDocumentCommand;

        #endregion


        #region Constructors

        public DataWidgetCustomizerViewModel()
        {
        }

        public DataWidgetCustomizerViewModel(WidgetViewModelBase widget)
            : this()
        {
            // Initialize collections
            AvailableProperties = new ObservableCollection<PropertyViewModel>();

            // Display this viewmodel in the DataLayoutControl
            CurrentItem = this;

            // Initialize properties
            _widget = widget;

            // Handle events
            _widget.ModelInitData.ModelInitializers.CollectionChanged += ModelInitializers_CollectionChanged;
        }

        public DataWidgetCustomizerViewModel(DataLayoutControlEx dataLayoutControl, WidgetViewModelBase widget)
            : this(widget)
        {
            // Initialize properties
            _dataLayoutControl = dataLayoutControl;

            // Handle DataLayoutControl events
            _dataLayoutControl.CurrentItemChanged += DataLayoutControl_CurrentItemChanged;
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to tell the parent document to re-send the InitializeDocumentMessage.
        /// </summary>
        [DoNotCopy]
        public ICommand ReInitializeDocumentCommand
        {
            get { return _reInitializeDocumentCommand ?? (_reInitializeDocumentCommand = new DelegateCommand(OnReInitializeDocumentExecute, OnReInitializeDocumentCanExecute)); }
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
        /// The data model type currently being configured.
        /// </summary>
        public DataModelTypeViewModelBase SelectedDataModelType
        {
            get { return _selectedDataModelType; }
            set
            {
                SetProperty(ref _selectedDataModelType, value, () => SelectedDataModelType, () =>
                    {
                        // Notify about related proeprty changes
                        RaisePropertyChanged(() => IsDataModelTypeSelected);

                        // Assign the SelectedModelInit based on the SelectedDataModelType
                        SelectedModelInit = (IsDataModelTypeSelected ? _widget.ModelInitData.GetModelInitializer(_selectedDataModelType.Type) : null);
                    });
            }
        }

        /// <summary>
        /// Indicates if a DataModelType is currently selected.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsDataModelTypeSelected
        {
            get { return (SelectedDataModelType != null); }
        }

        /// <summary>
        /// The model init currently being configured, based on the SelectedDataModelType.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public WidgetModelInit SelectedModelInit
        {
            get { return _selectedModelInit; }
            private set
            {
                SetProperty(ref _selectedModelInit, value, () => SelectedModelInit, () =>
                    {
                        RaisePropertiesChanged(() => IsModelInitSelected, () => InitMode);
                        PopulateAvailableProperties();
                    });
            }
        }

        /// <summary>
        /// Inidicates if a ModelInit is currently selected.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public bool IsModelInitSelected
        {
            get { return (SelectedModelInit != null); }
        }

        /// <summary>
        /// Indicates the InitMode of the SelectedDataModelType.
        /// </summary>
        public WidgetModelInit.InitModes? InitMode
        {
            get { return (IsModelInitSelected ? SelectedModelInit.InitMode : (WidgetModelInit.InitModes?)null); }
            set
            {
                if (IsModelInitSelected && value.HasValue)
                    SelectedModelInit.InitMode = value.Value;

                RaisePropertyChanged(() => InitMode);
            }
        }

        /// <summary>
        /// A list of all properties that are available on the SelectedDataModelType.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public ObservableCollection<PropertyViewModel> AvailableProperties { get; private set; }

        /// <summary>
        /// The property selected from AvailableProperties.
        /// </summary>
        public PropertyViewModel SelectedProperty
        {
            get { return _selectedProperty; }
            set
            {
                SetProperty(ref _selectedProperty, value, () => SelectedProperty, () =>
                {
                    // Abort if AvailableProperties is being populated
                    if (_populatingAvailableProperties)
                        return;

                    // Update the ChildPropertyName on the SelectedModelInit
                    SelectedModelInit.ChildPropertyName = (_selectedProperty != null ? _selectedProperty.Name : null);
                });
            }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// An instance of the DataModelTypeProvider.
        /// </summary>
        private static IDataModelTypeProvider DataModelTypeProvider
        {
            get { return _dataModelTypeProvider ?? (_dataModelTypeProvider = AutofacViewLocator.Default.Resolve<IDataModelTypeProvider>()); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Populates AvailableProperties with all properties on SelectedModelInit.DataModelType that reference a DataObjectBase or LocalDataObjectBase.
        /// </summary>
        private void PopulateAvailableProperties()
        {
            _populatingAvailableProperties = true;

            // Clear the AvailableProperties
            AvailableProperties.Clear();

            // Abort if no ModelInit is selected
            if (SelectedModelInit == null)
                return;

            // Get all visible properties on the selected type and add them to AvailableProperties
            foreach (var property in SelectedModelInit.DataModelType.GetSupportedProperties().Where(p => typeof(DataObjectBase).IsAssignableFrom(p.PropertyType) || typeof(LocalDataObjectBase).IsAssignableFrom(p.PropertyType)))
            {
                AvailableProperties.Add(new PropertyViewModel(property));
            }

            // Select the property specified by SelectedModelInit.ChildPropertyName
            SelectedProperty = AvailableProperties.FirstOrDefault(p => p.Name == SelectedModelInit.ChildPropertyName);

            _populatingAvailableProperties = false;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ReInitializeDocumentCommand.
        /// </summary>
        private void OnReInitializeDocumentExecute()
        {
            // Attempt to get the parent document of the widget
            var document = _widget.DocumentViewModel;
            if (document == null)
                return;

            // Re-initialize the document
            document.SendInitializeDocument();
        }

        /// <summary>
        /// CanExecute method for the ReInitializeDocumentCommand.
        /// </summary>
        private bool OnReInitializeDocumentCanExecute()
        {
            // Attempt to get the parent document of the widget
            var document = _widget.DocumentViewModel;
            if (document == null)
                return false;

            // Return whether the document was initialized with an item
            return document.IsInitializedWithItem;
        }

        /// <summary>
        /// Handles the DataLayoutControl.CurrentItemChanged event.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataLayoutControl_CurrentItemChanged(object sender, ValueChangedEventArgs<object> e)
        {
            // Abort if the DataLayoutControl does not have a CurrentItem
            if (_dataLayoutControl.CurrentItem == null)
                return;

            // Set the SelectedDataModelType based on the CurrentItem type
            var currentItemType = _dataLayoutControl.CurrentItem.GetType();
            SelectedDataModelType = DataModelTypeProvider.AllDataModels.FirstOrDefault(d => d.Type == currentItemType);
        }

        /// <summary>
        /// Handles the CollectionChanged event on the ModelInitializers collection.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ModelInitializers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // If the ModelInitializers collection was cleared, or the SelectedModelInit was removed from it, clear the SelectedDatModelType 
            if (e.Action == NotifyCollectionChangedAction.Reset ||
                e.Action == NotifyCollectionChangedAction.Remove && e.OldItems.Contains(SelectedModelInit))
                SelectedDataModelType = null;
        }

        #endregion


        #region Overrides

        public new static WidgetCustomizerViewModelBase CreateCustomizer(FrameworkElement content, WidgetViewModelBase widget)
        {
            // If the widget has model init data...
            if (widget.ModelInitData != null)
            {
                // Attempt to find a DataLayoutControlEx within the content
                // If one is found, add a DataWidgetCustomizerViewModel initialized with the DataLayoutControlEx
                var dataLayoutControl = LayoutHelper.FindElementByType<DataLayoutControlEx>(content);
                if (dataLayoutControl != null)
                    return new DataWidgetCustomizerViewModel(dataLayoutControl, widget);
            }

            // Otherwise, return null
            return null;
        }

        public override void OnWidgetClosed()
        {
            base.OnWidgetClosed();

            // Stop handling events
            if (_dataLayoutControl != null)
                _dataLayoutControl.CurrentItemChanged -= DataLayoutControl_CurrentItemChanged;

            _widget.ModelInitData.ModelInitializers.CollectionChanged -= ModelInitializers_CollectionChanged;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DataWidgetCustomizerViewModel> builder)
        {
            builder.DataFormLayout()
                .ContainsProperty(p => p.SelectedDataModelType)
                .GroupBox("Initialisation")
                    .ContainsProperty(p => p.InitMode)
                    .ContainsProperty(p => p.SelectedProperty);

            builder.Property(p => p.SelectedDataModelType)
                .DisplayName("Data Model")
                .Description("Select the model type to configure data intialisation for.");
            builder.Property(p => p.InitMode)
                .DisplayName("Mode")
                .Description("The mode to use when intialising this model type.");
            builder.Property(p => p.SelectedProperty)
                .DisplayName("Child Property")
                .Description("Select a child property to display when intialising this model type.");
            builder.Property(p => p.ReInitializeDocumentCommand)
                .DisplayName("Re-Initialize Document")
                .Description("Re-sends the item that was used to initialize the document to all widgets.");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DataWidgetCustomizerViewModel> builder)
        {
            builder.Condition(i => i != null && i.IsModelInitSelected)
                .ContainsProperty(p => p.IsModelInitSelected)
                .AffectsGroupVisibility("Initialisation");

            builder.Condition(i => i != null && i.InitMode == WidgetModelInit.InitModes.DisplayChild)
                .ContainsProperty(p => p.InitMode)
                .AffectsPropertyEnabled(p => p.SelectedProperty);

            var dataModelTypeProvider = DataModelTypeProvider;
            builder.Property(p => p.SelectedDataModelType).ReplaceEditor(new LookUpEditorDefinition()
            {
                ItemsSource = (dataModelTypeProvider != null ? dataModelTypeProvider.AllDataModels : null),
                EntityType = typeof(DataModelTypeViewModelBase)
            });

            builder.Property(p => p.InitMode)
                .ReplaceEditor(new OptionEditorDefinition(typeof(WidgetModelInit.InitModes)));

            builder.Property(p => p.SelectedProperty)
                .ReplaceEditor(new LookUpEditorDefinition()
                {
                    ItemsSourceMethod = context => ((DataWidgetCustomizerViewModel)context).AvailableProperties,
                    EntityType = typeof(PropertyViewModel)
                })
                .AllowNull();

            builder.Property(p => p.ReInitializeDocumentCommand).HideLabel();
        }

        #endregion
    }
}
