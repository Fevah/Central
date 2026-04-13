using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Core.Message
{
    /// <summary>
    /// Notifies widgets that an entity has updated values.
    /// Can be handled by any widget that needs to be aware of changes to entities.
    /// This message is always sent with no token.
    /// </summary>
    public class EntityChangedMessage : MessageBase
    {
        #region Private Fields

        private readonly List<EntityChange> _changes = new List<EntityChange>();

        #endregion


        #region Constructors

        public EntityChangedMessage(object sender)
            : base(sender)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a change to the message.
        /// </summary>
        /// <param name="entity">The entity that has changed.</param>
        /// <param name="changeType">The type of change that occurred.</param>
        public void AddChange(object entity, EntityChange.ChangeTypes changeType)
        {
            // Abort if the entity is null
            if (entity == null)
                return;

            // Add the change
            _changes.Add(new EntityChange(entity, changeType));
        }

        /// <summary>
        /// Adds a list of changes to the message.
        /// </summary>
        /// <param name="entities">The entities that have changed.</param>
        /// <param name="changeType">The type of change that occurred.</param>
        public void AddChanges(IEnumerable entities, EntityChange.ChangeTypes changeType)
        {
            // Abort if the entity list is null
            if (entities == null)
                return;

            // Add the changes
            foreach (var entity in entities)
            {
                _changes.Add(new EntityChange(entity, changeType));
            }
        }

        /// <summary>
        /// Adds a list of changes to the message.
        /// </summary>
        /// <param name="changes">An array of changes that occurred.</param>
        public void AddChanges(params EntityChange[] changes)
        {
            // Abort if the change array is null or empty
            if (changes == null || changes.Length == 0)
                return;

            // Add the changes
            _changes.AddRange(changes);
        }

        /// <summary>
        /// Sends this message.
        /// </summary>
        public void Send()
        {
            // Abort if there are no changes to send
            if (_changes.Count == 0)
                return;

            // Send the message
            Application.Current.Dispatcher.Invoke(() =>
                Messenger.Default.Send(this)
            );
        }

        /// <summary>
        /// Determines if this message includes entities of any of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to search for.</typeparam>
        /// <returns>True if the message includes entities of any of the specified type; otherwise false.</returns>
        public bool ContainsEntitiesOfType<T>()
        {
            return ContainsEntitiesOfType(typeof(T));
        }

        /// <summary>
        /// Determines if this message includes entities of any of the specified types.
        /// </summary>
        /// <param name="types">The types to search for.</param>
        /// <returns>True if the message includes entities of any of the specified types; otherwise false.</returns>
        public bool ContainsEntitiesOfType(params Type[] types)
        {
            return _changes.Any(c => types.Any(t => t.IsAssignableFrom(c.EntityType)));
        }

        /// <summary>
        /// Determines if this message includes data objects with any of the specified oids.
        /// </summary>
        /// <param name="oids">The oids to search for.</param>
        /// <returns>True if the message includes data objects with any of the specified oids; otherwise false.</returns>
        public bool ContainsEntitiesWithOid(params Guid[] oids)
        {
            return _changes
                .Where(c => typeof(DataObjectBase).IsAssignableFrom(c.EntityType))
                .Select(c => c.Oid)
                .Intersect(oids)
                .Any();
        }

        /// <summary>
        /// Determines if this message includes changes of any of the specified types.
        /// </summary>
        /// <param name="changeTypes">The change types to search for.</param>
        /// <returns>True if the message includes changes of any of the specified types; otherwise false.</returns>
        public bool ContainsChangeTypes(params EntityChange.ChangeTypes[] changeTypes)
        {
            return _changes
                .Select(c => c.ChangeType)
                .Intersect(changeTypes)
                .Any();
        }

        /// <summary>
        /// Returns all changes which contain data objects with any of the specified oids.
        /// </summary>
        /// <param name="oids">The oids to search for.</param>
        /// <returns>A list of changes that contain data objects with any of the specified oids.</returns>
        public IEnumerable<EntityChange> GetChangesWithOids(params Guid[] oids)
        {
            return _changes
                .Where(c => typeof(DataObjectBase).IsAssignableFrom(c.EntityType) && oids.Contains(c.Oid));
        }

        /// <summary>
        /// Returns all oids from changes which contain data objects.
        /// </summary>
        /// <returns>A list of oids from changes which contain data objects.</returns>
        public IEnumerable<Guid> GetAllOids()
        {
            return _changes
                .Where(c => typeof(DataObjectBase).IsAssignableFrom(c.EntityType))
                .Select(c => c.Oid);
        }

        #endregion


        #region Static Methods

        /// <summary>
        /// Sends a new EntityChangedMessage containing a single change.
        /// </summary>
        /// <param name="sender">The object that is sending the notification.</param>
        /// <param name="entity">The entity that has changed.</param>
        /// <param name="changeType">The type of change that occurred.</param>
        public static void Send(object sender, object entity, EntityChange.ChangeTypes changeType)
        {
            var message = new EntityChangedMessage(sender);
            message.AddChange(entity, changeType);
            message.Send();
        }

        /// <summary>
        /// Sends a new EntityChangedMessage containing a list of changes.
        /// </summary>
        /// <param name="sender">The object that is sending the notification.</param>
        /// <param name="entities">The entities that have changed.</param>
        /// <param name="changeType">The type of change that occurred.</param>
        public static void Send(object sender, IEnumerable entities, EntityChange.ChangeTypes changeType)
        {
            var message = new EntityChangedMessage(sender);
            message.AddChanges(entities, changeType);
            message.Send();
        }

        /// <summary>
        /// Sends a new EntityChangedMessage containing a list of changes.
        /// </summary>
        /// <param name="sender">The object that is sending the notification.</param>
        /// <param name="changes">An array of changes that occurred.</param>
        public static void Send(object sender, params EntityChange[] changes)
        {
            var message = new EntityChangedMessage(sender);
            message.AddChanges(changes);
            message.Send();
        }

        #endregion
    }
}
