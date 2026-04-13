using System.Collections;
using System.Linq;
using TIG.TotalLink.Client.Core.Extension;

namespace TIG.TotalLink.Client.Core.Helper
{
    public class ActionMessageHelper
    {
        /// <summary>
        /// Generates a title for an action being applied to a single object.
        /// </summary>
        /// <param name="item">The item the action is being applied to.</param>
        /// <param name="action">The action that is being applied to the item.</param>
        /// <returns>A string which can be used as a title.</returns>
        public static string GetTitle(object item, string action)
        {
            return string.Format("{0} {1} : {2}", action.ToTitleCase(), item.GetType().Name.AddSpaces(), item);
        }

        /// <summary>
        /// Generates a title for an action being applied to a list of objects.
        /// </summary>
        /// <param name="items">The items the action is being applied to.</param>
        /// <param name="action">The action that is being applied to the items.</param>
        /// <returns>A string which can be used as a title.</returns>
        public static string GetTitle(IList items, string action)
        {
            // Abort if the list is null or empty
            if (items == null || items.Count == 0)
                return null;

            // If there is only one item in the list, get a title for that item
            if (items.Count == 1)
                return GetTitle(items[0], action);

            // Generate a type name
            var itemTypeName = items.Cast<object>().AreSameType()
                ? items[0].GetType().Name.AddSpaces().Pluralize()
                : "items";

            // Generate a title for multiple items
            return string.Format("{0} {1} {2}", action.ToTitleCase(), items.Count, itemTypeName);
        }

        /// <summary>
        /// Generates a warning message for an action being applied to a single object.
        /// </summary>
        /// <param name="item">The item the action is being applied to.</param>
        /// <param name="action">The action that is being applied to the item.</param>
        /// <returns>A string which can be used as a warning message.</returns>
        public static string GetWarningMessage(object item, string action)
        {
            return string.Format("Warning: This will {0} the {1} \"{2}\"!\r\n\r\nAre you sure?", action.ToTitleCase(), item.GetType().Name.AddSpaces(), item);
        }

        /// <summary>
        /// Generates a warning message for an action being applied to a list of objects.
        /// </summary>
        /// <param name="items">The items the action is being applied to.</param>
        /// <param name="action">The action that is being applied to the items.</param>
        /// <returns>A string which can be used as a warning message.</returns>
        public static string GetWarningMessage(IList items, string action)
        {
            // Abort if the list is null or empty
            if (items == null || items.Count == 0)
                return null;

            // If there is only one item in the list, get a message for that item
            if (items.Count == 1)
                return GetWarningMessage(items[0], action);

            // Generate a type name
            var itemTypeName = items.Cast<object>().AreSameType()
                ? items[0].GetType().Name.AddSpaces().Pluralize()
                : "items";

            // Generate a message for multiple items
            return string.Format("Warning: This will {0} the {1} selected {2}!\r\n\r\nAre you sure?", action.ToTitleCase(), items.Count, itemTypeName);
        }

        /// <summary>
        /// Generates a description for a single object including the ToString value the type name.
        /// </summary>
        /// <param name="item">The item to generate a description for.</param>
        /// <returns>A string which can be used as a description.</returns>
        public static string GetDescription(object item)
        {
            // ABort if the item is null
            if (item == null)
                return null;

            // Generate a description for the item
            return string.Format("{0} ({1})", item, item.GetType().Name);
        }

        /// <summary>
        /// Generates a description for a list of objects including either the ToString value or count, and type name.
        /// </summary>
        /// <param name="items">The items to generate a description for.</param>
        /// <returns>A string which can be used as a description.</returns>
        public static string GetDescription(IList items)
        {
            // Abort if the list is null or empty
            if (items == null || items.Count == 0)
                return null;

            // If there is only one item in the list, get a description for that item
            if (items.Count == 1)
                return GetDescription(items[0]);

            // Generate a type name
            var itemTypeName = items.Cast<object>().AreSameType()
                ? items[0].GetType().Name.Pluralize()
                : "items";

            // Generate a description for multiple items
            return string.Format("({0} {1})", items.Count, itemTypeName);
        }
    }
}
