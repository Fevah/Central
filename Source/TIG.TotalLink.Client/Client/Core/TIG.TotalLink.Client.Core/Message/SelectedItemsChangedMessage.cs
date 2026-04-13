using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Message.Core;

namespace TIG.TotalLink.Client.Core.Message
{
    /// <summary>
    /// Notifies widgets that the selected items have changed in the active widget.
    /// Can be handled by any widget that needs to be aware of which items are selected in other widgets.
    /// </summary>
    [DisplayName("Selected Items Changed")]
    public class SelectedItemsChangedMessage : MessageBase
    {
        #region Constructors

        public SelectedItemsChangedMessage(object sender, IEnumerable selectedItems)
            : base(sender)
        {
            SelectedItems = selectedItems;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// All items that are currently selected.
        /// </summary>
        public IEnumerable SelectedItems { get; private set; }

        /// <summary>
        /// Determines if this message includes entities of any of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to search for.</typeparam>
        /// <returns>True if the message includes entities of any of the specified type; otherwise false.</returns>
        public bool ContainsEntitiesOfType<T>()
        {
            return SelectedItems.OfType<T>().Any();
        }

        /// <summary>
        /// Determines if this message includes entities of any of the specified types.
        /// </summary>
        /// <param name="types">The types to search for.</param>
        /// <returns>True if the message includes entities of any of the specified types; otherwise false.</returns>
        public bool ContainsEntitiesOfType(params Type[] types)
        {
            return SelectedItems.Cast<object>().Any(c => types.Any(t => t.IsInstanceOfType(c)));
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns all entities of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to search for.</typeparam>
        /// <returns>A list containing all entities of the specified type.</returns>
        public List<T> GetEntitiesOfType<T>()
        {
            return SelectedItems.OfType<T>().ToList();
        }

        /// <summary>
        /// Returns all entities of the specified types.
        /// </summary>
        /// <param name="types">The types to search for.</param>
        /// <returns>A list containing all entities of the specified types.</returns>
        public List<object> GetEntitiesOfType(params Type[] types)
        {
            return SelectedItems.Cast<object>().Where(c => types.Any(t => t.IsInstanceOfType(c))).ToList();
        }

        /// <summary>
        /// Returns the primary type of the selected items.
        /// </summary>
        /// <returns>If the SelectedItems is a generic collection this will return the type that the collection contains; otherwise the type of the first object.</returns>
        public Type GetPrimaryType()
        {
            // Abort if the SelectedItems is null
            if (SelectedItems == null)
                return null;

            // If the SelectedItems is a generic collection, return the type that the collection contains
            var selectedItemListType = SelectedItems.GetType();
            if (typeof(List<>).IsAssignableFromGeneric(selectedItemListType))
                return selectedItemListType.GenericTypeArguments[0];

            // Abort if the SelectedItems is empty
            if (!SelectedItems.Cast<object>().Any())
                return null;

            // Return the type of the first item
            return SelectedItems.Cast<object>().First().GetType();
        }

        #endregion
    }
}
