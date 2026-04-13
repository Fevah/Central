using System.Collections;
using System.Linq;
using DevExpress.Data.Filtering;

namespace TIG.TotalLink.Client.Editor.Helper
{
    public class WidgetAutoFilter
    {
        #region Constructors

        public WidgetAutoFilter(WidgetFilter filter, IEnumerable sourceObjects)
        {
            Filter = filter;
            SourceObjects = sourceObjects;

            UpdateFilterCriteria();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The filter that is being applied.
        /// </summary>
        public WidgetFilter Filter { get; private set; }

        /// <summary>
        /// The objects that are being filtered on.
        /// </summary>
        public IEnumerable SourceObjects { get; private set; }

        /// <summary>
        /// A CriteriaOperator that represents the criteria to apply in order to filter on the SourceObjects.
        /// </summary>
        public CriteriaOperator FilterCriteria { get; private set; }

        /// <summary>
        /// A CriteriaOperator that represents the criteria using the display value, for presentation to the user.
        /// </summary>
        public CriteriaOperator DisplayFilterCriteria { get; private set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the filter criteria.
        /// </summary>
        private void UpdateFilterCriteria()
        {
            // Clear the filter criteria
            FilterCriteria = null;
            DisplayFilterCriteria = null;

            // Get a list of values to filter on, and abort if the list is empty
            var filterValues = SourceObjects.Cast<object>().Select(Filter.ValueSelector).ToList();
            if (filterValues.Count == 0)
                return;

            // Get a string containing enough placeholders to hold the parameters
            var parameterPlaceholders = string.Join(",", filterValues.Select(o => "?"));

            // Create and store the filter criteria
            FilterCriteria = CriteriaOperator.Parse(Filter.FilterString.Replace("?", parameterPlaceholders), filterValues.ToArray());

            // Abort if no DisplayValueSelector or DisplayFilterString have been set
            if (Filter.DisplayValueSelector == null || string.IsNullOrWhiteSpace(Filter.DisplayFilterString))
                return;

            // Get a list of display values for the display criteria
            var filterDisplayValues = SourceObjects.Cast<object>().Select(Filter.DisplayValueSelector).ToList();

            // Create and store the display filter criteria
            DisplayFilterCriteria = CriteriaOperator.Parse(Filter.DisplayFilterString.Replace("?", parameterPlaceholders), filterDisplayValues.Cast<object>().ToArray());
        }

        #endregion
    }
}
