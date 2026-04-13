using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using DevExpress.Mvvm;
using Newtonsoft.Json.Linq;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Client.Editor.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class WidgetFilterData : BindableBase
    {
        #region Public Enums

        public enum FilterModes
        {
            AllAvailableTypes,
            SelectedTypesOnly
        }

        #endregion


        #region Private Fields

        private readonly DocumentViewModel _parentDocument;
        private FilterModes _filterMode;
        private bool _hasFilters;

        #endregion


        #region Constructors

        public WidgetFilterData(ISupportFilterData supportAutoFilter, DocumentViewModel parentDocument)
        {
            _parentDocument = parentDocument;

            // Initialize collections
            SelectedTypes = new ObservableCollection<WidgetFilter>();
            AvailableTypes = new ObservableCollection<WidgetFilter>();

            // Get the widget filters from the ISupportFilterData and add them to AvailableTypes
            foreach (var widgetFilter in supportAutoFilter.GetWidgetFilters())
            {
                AvailableTypes.Add(widgetFilter);
            }

            // If AvailableTypes is not empty, set the flag to indicate that this data contains some filters
            if (AvailableTypes.Count > 0)
                HasFilters = true;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if this filter data is currently managing any filter types.
        /// </summary>
        public bool HasFilters
        {
            get { return _hasFilters; }
            private set { SetProperty(ref _hasFilters, value, () => HasFilters); }
        }

        /// <summary>
        /// Specifies the mode to use when processing filters.
        /// </summary>
        public FilterModes FilterMode
        {
            get { return _filterMode; }
            set { SetProperty(ref _filterMode, value, () => FilterMode, () => _parentDocument.IsModified = true); }
        }

        /// <summary>
        /// A list of types that this filter data will filter by when FilterMode = SelectedTypesOnly.
        /// </summary>
        public ObservableCollection<WidgetFilter> SelectedTypes { get; private set; }

        /// <summary>
        /// A list of types that this widget can filter by.
        /// </summary>
        public ObservableCollection<WidgetFilter> AvailableTypes { get; private set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Initializes the filter data.
        /// </summary>
        public void Initialize()
        {
            // Handle events
            SelectedTypes.CollectionChanged += SelectedTypes_CollectionChanged;
        }

        /// <summary>
        /// Deinitializes the filter data.
        /// </summary>
        public void Deinitialize()
        {
            // Stop handling events
            SelectedTypes.CollectionChanged -= SelectedTypes_CollectionChanged;
        }

        /// <summary>
        /// Indicates if filters can be applied for the specified type based on the current filter settings.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>True if the filter should be allowed; otherwise false.</returns>
        public bool IsFilterAllowed(Type type)
        {
            // Don't allow the filter if the type is not included in AvailableTypes
            if (AvailableTypes.All(f => f.SourceType != type))
                return false;

            // Allow the filter if this widget is accepting all filters
            if (FilterMode == FilterModes.AllAvailableTypes)
                return true;

            // Allow the filter if the type is included in SelectedTypes
            return SelectedTypes.Any(f => f.SourceType == type);
        }

        /// <summary>
        /// Gets a WidgetFilter if filters can be applied for the specified type based on the current filter settings.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>A WidgetFilter if the filter should be allowed; otherwise null.</returns>
        public WidgetFilter GetFilterIfAllowed(Type type)
        {
            // Attempt to return the WidgetFilter from AvailableTypes if this widget is accepting all types
            if (FilterMode == FilterModes.AllAvailableTypes)
                return AvailableTypes.FirstOrDefault(f => f.SourceType == type);

            // Otherwise, attempt to return the WidgetFilter from SelectedTypes
            return SelectedTypes.FirstOrDefault(f => f.SourceType == type);
        }

        /// <summary>
        /// Returns a JObject which represents this filter data.
        /// </summary>
        /// <returns>A JObject which represents this filter data.</returns>
        public JObject SerializeToJsonObject()
        {
            // Create a json object that represents this filter data
            return new JObject(
                new JProperty("Type", "FilterData"),
                new JProperty("FilterMode", FilterMode),
                new JProperty("SelectedTypes",
                    new JArray(
                        SelectedTypes.Select(
                            t => new JObject(
                                new JProperty("Name", t.SourceType.FullName)
                            )
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Deserializes this filter data from a JObject.
        /// </summary>
        /// <param name="jobject">The JObject to deserialize from.</param>
        public void DeserializeFromJsonObject(JObject jobject)
        {
            // Abort if the jobject is null
            if (jobject == null)
                return;

            // Parse basic properties
            FilterMode = (FilterModes)((int)jobject["FilterMode"]);

            // Parse the SelectedTypes array
            SelectedTypes.Clear();
            foreach (var jsonType in jobject["SelectedTypes"].Children().Cast<JObject>())
            {
                var typeName = (string)jsonType["Name"];

                // Attempt to find the filter that the typeName refers to
                var filter = AvailableTypes.FirstOrDefault(t => t.SourceType.FullName == typeName);
                if (filter != null)
                    SelectedTypes.Add(filter);
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the CollectionChanged event for the SelectedTypes collection.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedTypes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _parentDocument.IsModified = true;
        }

        #endregion
    }
}
