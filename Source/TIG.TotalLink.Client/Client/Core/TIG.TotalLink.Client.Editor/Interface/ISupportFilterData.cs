using System.Collections.Generic;
using TIG.TotalLink.Client.Editor.Helper;

namespace TIG.TotalLink.Client.Editor.Interface
{
    public interface ISupportFilterData
    {
        /// <summary>
        /// Gets a list of available widget filters.
        /// </summary>
        /// <returns>A list of available widget filters.</returns>
        IEnumerable<WidgetFilter> GetWidgetFilters();
    }
}
