using System.Collections.Generic;
using System.Linq;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class EnumerableExtension
    {
        /// <summary>
        /// Returns the first element of a sequence if all elements are equal, or a default value if any element is different.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">The IEnumerable&lt;T&gt; to return elements from.</param>
        /// <returns>default(TSource) if any element is different than the others; otherwise, the first element in <paramref name="source"/>.</returns>
        public static TSource ValueIfEqualOrDefault<TSource>(this IEnumerable<TSource> source)
        {
            // If source is empty, just return the default value
            if (!source.Any())
                return default(TSource);

            // Compare all items to the first item, and return the first item if they are all equal, otherwise return the default value
            TSource firstValue = source.First();
            return (source.Skip(1).All(v => (v == null && firstValue == null) || (firstValue != null && firstValue.Equals(v)) || (v != null && v.Equals(firstValue))) ? firstValue : default(TSource));
        }

        /// <summary>
        /// Returns true if all elements are the same type.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
        /// <param name="source">The IEnumerable&lt;T&gt; to test elements of.</param>
        /// <returns>True if all elements are the same type; otherwise false.</returns>
        public static bool AreSameType<TSource>(this IEnumerable<TSource> source)
        {
            // Abort if source is empty
            if (!source.Any())
                return false;

            var firstType = source.First().GetType();
            return source.Skip(1).All(t => t.GetType() == firstType);
        }
    }
}
