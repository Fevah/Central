using System.Reflection;
using DevExpress.Data.Filtering;
using DevExpress.Data.Filtering.Helpers;
using DevExpress.Xpf.Grid;

namespace TIG.TotalLink.Client.Editor.Helper
{
    /// <summary>
    /// A helper class for converting filteria criteria to a display format using the same method that is used by DataViewBase.
    /// </summary>
    public class DisplayCriteriaHelper
    {
        #region Private Fields

        private static ConstructorInfo _displayCriteriaHelperConstructor;
        private readonly IDisplayCriteriaGeneratorNamesSource _displayCriteriaHelper;

        #endregion


        #region Constructors

        public DisplayCriteriaHelper(DataViewBase dataView)
        {
            if (_displayCriteriaHelperConstructor == null)
            {
                // Attempt to get the DisplayCriteriaHelper type
                var displayCriteriaHelperType = typeof(DataViewBase).Assembly.GetType("DevExpress.Xpf.Grid.Native.DisplayCriteriaHelper");
                if (displayCriteriaHelperType == null)
                    return;

                // Attempt to get the DisplayCriteriaHelper constructor
                _displayCriteriaHelperConstructor = displayCriteriaHelperType.GetConstructor(new[] { typeof(DataViewBase) });
            }

            // Abort if we couldn't find the DisplayCriteriaHelper constructor
            if (_displayCriteriaHelperConstructor == null)
                return;

            // Create a new DisplayCriteriaHelper
            _displayCriteriaHelper = (IDisplayCriteriaGeneratorNamesSource)_displayCriteriaHelperConstructor.Invoke(new object[] { dataView });
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Converts filter criteria to a format for display.
        /// </summary>
        /// <param name="filterCriteria">The criteria to convert.</param>
        /// <returns>The converted criteria.</returns>
        public CriteriaOperator Process(CriteriaOperator filterCriteria)
        {
            // Abort if we were unable to create a DisplayCriteriaHelper
            if (_displayCriteriaHelper == null)
                return filterCriteria;

            // Return the re-formatted filter criteria
            return DisplayCriteriaGenerator.Process(_displayCriteriaHelper, filterCriteria);
        }

        #endregion
    }
}
