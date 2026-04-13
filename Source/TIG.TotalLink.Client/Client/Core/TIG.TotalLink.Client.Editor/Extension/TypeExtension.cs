using System;
using System.Collections.Generic;
using System.Linq;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Editor.Extension
{
    public static class TypeExtension
    {
        [Flags]
        public enum FilterTypes
        {
            Entity = 1,
            All = Entity, 
        }

        /// <summary>
        /// Gets WidgetFilters for attributes on the supplied type.
        /// </summary>
        /// <param name="type">The type to get widget filters for.</param>
        /// <param name="filterType">The type of attributes to get widget filters for.</param>
        /// <returns>A list of WidgetFilters created from the specified attributes.</returns>
        public static IEnumerable<WidgetFilter> GetWidgetFilters(this Type type, FilterTypes filterType)
        {
            var filters = new List<WidgetFilter>();

            // If the Entity filterType is set, get WidgetFilters for each EntityFilter defined on type
            if ((filterType & FilterTypes.Entity) == FilterTypes.Entity)
            {
                filters.AddRange(
                    type.GetCustomAttributes(typeof(EntityFilterAttribute), true)
                        .Cast<EntityFilterAttribute>()
                        .Select(a => new WidgetFilter(
                            a.EntityType,
                            a.FilterString,
                            a.DisplayFilterString,
                            o => ((DataObjectBase)o).Oid,
                            o => o.ToString()
                        )
                    )
                );
            }

            return filters;
        }
    }
}
