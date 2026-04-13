using System;
using TIG.TotalLink.Client.Core.Extension;

namespace TIG.TotalLink.Client.Editor.Helper
{
    public class WidgetFilter
    {
        #region Constructors

        public WidgetFilter(Type sourceType, string filterString, Func<object, object> valueSelector)
        {
            SourceType = sourceType;
            FilterString = filterString;
            ValueSelector = valueSelector;
        }

        public WidgetFilter(Type sourceType, string filterString, string displayFilterString, Func<object, object> valueSelector, Func<object, string> displayValueSelector)
            : this(sourceType, filterString, valueSelector)
        {
            DisplayFilterString = displayFilterString;
            DisplayValueSelector = displayValueSelector;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type that this entity can be filtered by.
        /// </summary>
        public Type SourceType { get; set; }

        /// <summary>
        /// The filter pattern to use in order to filter this entity by the EntityType.
        /// </summary>
        public string FilterString { get; set; }

        /// <summary>
        /// The filter pattern to use for display.
        /// </summary>
        public string DisplayFilterString { get; set; }

        /// <summary>
        /// An expression that can be used to get the filter value from the source objects.
        /// </summary>
        public Func<object, object> ValueSelector { get; set; }

        /// <summary>
        /// An expression that can be used to get the filter display value from the source objects.
        /// </summary>
        public Func<object, string> DisplayValueSelector { get; set; }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return SourceType.Name.AddSpaces();
        }

        #endregion
    }
}
