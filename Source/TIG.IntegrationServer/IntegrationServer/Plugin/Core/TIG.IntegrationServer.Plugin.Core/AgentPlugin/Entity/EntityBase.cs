using System.Collections.Generic;
using System.Linq;
using TIG.IntegrationServer.Common.Extension;

namespace TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity
{
    public class EntityBase : Dictionary<string, object>, IEntity
    {
        #region Public Properties

        /// <summary>
        /// Entity unique identifier. Should be set by the agent. Sync engine can only read it.
        /// </summary>
        public virtual object Id { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// SetValue method for set property value.
        /// </summary>
        /// <param name="propertyName">Property name</param>
        /// <param name="value">Object value</param>
        public void SetValue(string propertyName, object value)
        {
            var field = this.FirstOrDefault(p => propertyName.SmartCompare(p.Key));
            if (field.Key == null)
            {
                this[propertyName] = value;
                return;
            }
            this[field.Key] = value;
        }

        /// <summary>
        /// Get value from property name.
        /// </summary>
        /// <param name="propertyname">Property Name</param>
        /// <returns>Property value</returns>
        public object GetValue(string propertyname)
        {
            object value;
            return TryGetValue(propertyname, out value) ? value : null;
        }

        /// <summary>
        /// Get Entity Property names
        /// </summary>
        /// <returns>Name of properties</returns>
        public string[] EntityPropertyNames()
        {
            return Keys.ToArray();
        }

        /// <summary>
        /// Check entity is empty
        /// </summary>
        /// <returns>True, indicate entity is empty.</returns>
        public bool IsEmpty()
        {
            return Values.All(p => p == null);
        }

        /// <summary>
        /// Map from entity which inherit from IDictionary.
        /// </summary>
        /// <param name="entity">entity wich inherit from IDictionary.</param>
        /// <returns>Return value which type is DataEntityBase.</returns>
        public static EntityBase Map(IDictionary<string, object> entity)
        {
            var dataEntity = new EntityBase();
            foreach (var pro in entity)
            {
                dataEntity.Add(pro.Key, pro.Value);
            }

            return dataEntity;
        }

        #endregion
    }
}